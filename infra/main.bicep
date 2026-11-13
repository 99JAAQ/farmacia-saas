// The demo on Azure's free tiers (ADR 0020). Deployed into an existing resource group:
//   az deployment group create -g <group> -f infra/main.bicep -p infra/main.bicepparam
// Secrets are not parameters: the deployment workflow creates them in Key Vault (only if missing).

targetScope = 'resourceGroup'

@description('Region of every resource. It must accept new SQL servers for the subscription (free trials are restricted in some regions): Central US does.')
param location string = resourceGroup().location

@description('Short name used in every resource name.')
@maxLength(12)
param appName string = 'farmacia'

@description('The app image, e.g. ghcr.io/<owner>/farmacia-saas:<commit>. Public on GHCR, so no registry credentials.')
param containerImage string = ''

@description('False on the first pass: the app reads its secrets from Key Vault, which must exist (with them) before.')
param deployApp bool = true

@description('Who deploys (the service principal of the GitHub workflow): may create the secrets in Key Vault.')
param deployerObjectId string

@description('Microsoft Entra principal that administers the SQL server (runs the migrations). Ideally a group with you and the deployment identity.')
param sqlAdminObjectId string

@description('Display name of that principal.')
param sqlAdminLogin string

@allowed(['User', 'Group', 'Application'])
param sqlAdminPrincipalType string = 'Group'

@description('The first platform administrator, created at the first start with the password from Key Vault (must change it at login).')
param bootstrapAdminEmail string

@description('Who gets the budget alerts.')
param budgetEmail string

@description('Monthly budget in USD: an email at 50 %, 100 % and the forecast of 100 %. The demo is meant to cost 0.')
param monthlyBudgetUsd int = 5

@description('First day of the current month (yyyy-MM-01): budgets cannot start in the past.')
param budgetStartDate string = '${utcNow('yyyy-MM')}-01'

// The region is part of the suffix: a name stays tied to the region it was first created in (even by a failed
// attempt), so moving to another region must give new names.
var suffix = uniqueString(resourceGroup().id, location)
var tags = { app: appName, environment: 'demo' }

// --- Identity: what the app is to Key Vault and to SQL (no passwords anywhere) ---------------------------------------
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${appName}'
  location: location
  tags: tags
}

// --- Secrets -------------------------------------------------------------------------------------------------------
resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-${take(appName, 8)}-${take(suffix, 10)}'
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// Key Vault Secrets User: read secret values, nothing else.
resource vaultReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, identity.id, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets Officer for the deployment: it creates the secrets once (and never reads them back into logs).
resource vaultDeployer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, deployerObjectId, 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
    principalId: deployerObjectId
    principalType: 'ServicePrincipal'
  }
}

// --- Database: Azure SQL free offer (100k vCore-seconds and 32 GB a month, then it pauses until the next month) ------
resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: 'sql-${appName}-${suffix}'
  location: location
  tags: tags
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    // Microsoft Entra ID only: no SQL logins, no sa, no password to leak.
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: sqlAdminLogin
      sid: sqlAdminObjectId
      principalType: sqlAdminPrincipalType
      tenantId: subscription().tenantId
    }
  }
}

// "Allow Azure services": the Container App has no fixed outbound IP on the free plan. Anyone connecting still needs
// a Microsoft Entra identity the database knows. (A private endpoint would close it, at about USD 7 a month.)
resource sqlAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: appName
  location: location
  tags: tags
  sku: { name: 'GP_S_Gen5_2', tier: 'GeneralPurpose', family: 'Gen5', capacity: 2 }
  properties: {
    useFreeLimit: true
    // When the month's free allowance runs out, pause instead of billing.
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}

// --- Logs: a small daily cap keeps it inside the 5 GB a month of free ingestion -----------------------------------
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${appName}-${suffix}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    workspaceCapping: { dailyQuotaGb: json('0.15') }
  }
}

// --- The app: one container, API + React on the same origin (ADR 0019) --------------------------------------------
resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${appName}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource app 'Microsoft.App/containerApps@2024-03-01' = if (deployApp) {
  name: 'ca-${appName}'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  dependsOn: [vaultReader]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto' // HTTP/1.1, HTTP/2 and the SignalR WebSocket
        allowInsecure: false
      }
      // Read from Key Vault with the app's identity, at every new revision.
      secrets: [
        {
          name: 'jwt-signing-key'
          keyVaultUrl: '${vault.properties.vaultUri}secrets/jwt-signing-key'
          identity: identity.id
        }
        {
          name: 'bootstrap-admin-password'
          keyVaultUrl: '${vault.properties.vaultUri}secrets/bootstrap-admin-password'
          identity: identity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'app'
          image: containerImage
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            {
              // No password: the app is the managed identity. A paused free database takes up to a minute to resume.
              name: 'ConnectionStrings__Farmacia'
              value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${database.name};Authentication=Active Directory Managed Identity;User Id=${identity.properties.clientId};Encrypt=True;Connect Timeout=90'
            }
            { name: 'Jwt__SigningKey', secretRef: 'jwt-signing-key' }
            { name: 'Bootstrap__AdminEmail', value: bootstrapAdminEmail }
            { name: 'Bootstrap__AdminPassword', secretRef: 'bootstrap-admin-password' }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
          ]
        }
      ]
      scale: {
        // Free plan: it sleeps when nobody uses it (the first visit then takes a few seconds).
        minReplicas: 0
        // One replica on purpose: SignalR groups live in memory; more replicas need a backplane (ADR 0020).
        maxReplicas: 1
      }
    }
  }
}

// --- Safety net: an email before the demo costs anything ----------------------------------------------------------
resource budget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: 'budget-${appName}'
  properties: {
    category: 'Cost'
    amount: monthlyBudgetUsd
    timeGrain: 'Monthly'
    timePeriod: { startDate: budgetStartDate }
    notifications: {
      actual50: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 50, thresholdType: 'Actual', contactEmails: [budgetEmail] }
      actual100: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 100, thresholdType: 'Actual', contactEmails: [budgetEmail] }
      forecast100: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 100, thresholdType: 'Forecasted', contactEmails: [budgetEmail] }
    }
  }
}

output appUrl string = deployApp ? 'https://${app!.properties.configuration.ingress.fqdn}' : ''
output containerAppName string = 'ca-${appName}'
output keyVaultName string = vault.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
output appIdentityName string = identity.name
output appIdentityClientId string = identity.properties.clientId
