<#
.SYNOPSIS
  One-time setup so GitHub Actions can deploy to Azure without storing any secret (OIDC). Run after `az login`.

.DESCRIPTION
  Creates (or reuses):
    - the resource group;
    - a Microsoft Entra app registration for the deploy workflow, with a federated credential for the repository's
      main branch (GitHub proves who it is with a short-lived token; there is no client secret);
    - its roles on the resource group only: Contributor, and Role Based Access Control Administrator (the Bicep
      template assigns the app identity its Key Vault role);
    - a Microsoft Entra group that administers the SQL server, with you and the deploy workflow as members;
    - the resource providers the template uses;
    - the GitHub repository variables the deploy workflow reads (ids, not secrets).
  Safe to run again.

.EXAMPLE
  ./infra/setup-azure.ps1 -BootstrapAdminEmail admin@midominio.com -BudgetEmail yo@correo.com
#>
param(
  [Parameter(Mandatory)] [string] $BootstrapAdminEmail,
  [Parameter(Mandatory)] [string] $BudgetEmail,
  [string] $ResourceGroup = 'rg-farmacia-demo',
  [string] $Location = 'eastus2',
  [string] $Repository = '99JAAQ/farmacia-saas',
  [string] $AppRegistrationName = 'github-farmacia-saas',
  [string] $SqlAdminGroupName = 'farmacia-sql-admins'
)

$ErrorActionPreference = 'Stop'
# Not named "Az": PowerShell names are case-insensitive, so a function "Az" calling az would call itself forever.
function Invoke-AzCli {
  $output = & az.cmd @args
  if ($LASTEXITCODE -ne 0) { throw "az $args failed" }
  $output
}

# az is a .cmd on Windows: its arguments go through cmd.exe, which breaks on JMESPath's parentheses and pipes
# (length(@), "| ..."). So lists come back as JSON and are counted or filtered here, in PowerShell.
function Invoke-AzJson {
  $text = (Invoke-AzCli @args --output json) -join "`n"
  if ([string]::IsNullOrWhiteSpace($text)) { return @() }
  @($text | ConvertFrom-Json)
}

$account = Invoke-AzCli account show --output json | ConvertFrom-Json
Write-Host "Suscripción: $($account.name) ($($account.id))"

Write-Host 'Proveedores de recursos...'
foreach ($provider in 'Microsoft.App', 'Microsoft.OperationalInsights', 'Microsoft.Sql', 'Microsoft.KeyVault', 'Microsoft.ManagedIdentity', 'Microsoft.Consumption') {
  Invoke-AzCli provider register --namespace $provider --output none
}

Write-Host "Grupo de recursos $ResourceGroup en $Location..."
$group = Invoke-AzCli group create --name $ResourceGroup --location $Location --output json | ConvertFrom-Json

Write-Host "Registro de aplicación $AppRegistrationName (sin secreto: OIDC)..."
$appId = Invoke-AzCli ad app list --display-name $AppRegistrationName --query '[0].appId' --output tsv
if (-not $appId) { $appId = Invoke-AzCli ad app create --display-name $AppRegistrationName --query appId --output tsv }
$spId = Invoke-AzCli ad sp list --filter "appId eq '$appId'" --query '[0].id' --output tsv
if (-not $spId) { $spId = Invoke-AzCli ad sp create --id $appId --query id --output tsv }

$subject = "repo:${Repository}:ref:refs/heads/main"
$existing = @(Invoke-AzJson ad app federated-credential list --id $appId | Where-Object { $_.subject -eq $subject })
if ($existing.Count -eq 0) {
  $credential = @{ name = 'github-main'; issuer = 'https://token.actions.githubusercontent.com'; subject = $subject; audiences = @('api://AzureADTokenExchange') } | ConvertTo-Json -Compress
  $file = New-TemporaryFile
  Set-Content -Path $file -Value $credential -Encoding utf8
  Invoke-AzCli ad app federated-credential create --id $appId --parameters "@$file" --output none
  Remove-Item $file
}

Write-Host 'Roles del workflow, solo sobre el grupo de recursos...'
foreach ($role in 'Contributor', 'Role Based Access Control Administrator') {
  $assigned = Invoke-AzJson role assignment list --assignee $spId --scope $group.id --role $role
  if ($assigned.Count -eq 0) { Invoke-AzCli role assignment create --assignee-object-id $spId --assignee-principal-type ServicePrincipal --role $role --scope $group.id --output none }
}

Write-Host "Grupo $SqlAdminGroupName (administra el servidor SQL)..."
$groupId = Invoke-AzCli ad group list --display-name $SqlAdminGroupName --query '[0].id' --output tsv
if (-not $groupId) { $groupId = Invoke-AzCli ad group create --display-name $SqlAdminGroupName --mail-nickname $SqlAdminGroupName --query id --output tsv }
$me = Invoke-AzCli ad signed-in-user show --query id --output tsv
foreach ($member in $me, $spId) {
  if ((Invoke-AzCli ad group member check --group $groupId --member-id $member --query value --output tsv) -ne 'true') {
    Invoke-AzCli ad group member add --group $groupId --member-id $member --output none
  }
}

Write-Host "Variables del repositorio $Repository..."
$variables = [ordered]@{
  AZURE_CLIENT_ID       = $appId
  AZURE_TENANT_ID       = $account.tenantId
  AZURE_SUBSCRIPTION_ID = $account.id
  AZURE_RESOURCE_GROUP  = $ResourceGroup
  DEPLOYER_OBJECT_ID    = $spId
  SQL_ADMIN_GROUP_ID    = $groupId
  SQL_ADMIN_GROUP_NAME  = $SqlAdminGroupName
  BOOTSTRAP_ADMIN_EMAIL = $BootstrapAdminEmail
  BUDGET_EMAIL          = $BudgetEmail
}
foreach ($name in $variables.Keys) {
  gh variable set $name --repo $Repository --body $variables[$name]
  if ($LASTEXITCODE -ne 0) { throw "gh variable set $name failed" }
}

Write-Host ''
Write-Host 'Listo. El próximo CI exitoso en main despliega la demo (o ejecute el workflow "Deploy" a mano).'
