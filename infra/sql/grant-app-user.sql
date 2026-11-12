-- The app's database user: its managed identity, with read and write rights and nothing else (ADR 0014, 0020).
-- No DDL, no ALTER ANY SECURITY POLICY: even with the app's credentials nobody can switch Row-Level Security off.
-- Run by the deployment (as the Microsoft Entra admin) after the migrations; idempotent.
-- Variables:
--   $(AppIdentityName)     the user-assigned identity's name (id-farmacia)
--   $(AppIdentityClientId) its client (application) id
--
-- WITH SID, TYPE = E instead of FROM EXTERNAL PROVIDER: the SID of a managed identity is its client id, so the server
-- does not need to look it up in Microsoft Entra (which would require the "Directory Readers" role, a tenant admin's
-- decision).

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(AppIdentityName)')
BEGIN
    DECLARE @sid varbinary(16) = CAST(CAST(N'$(AppIdentityClientId)' AS uniqueidentifier) AS varbinary(16));
    DECLARE @sql nvarchar(400) =
        N'CREATE USER ' + QUOTENAME(N'$(AppIdentityName)') + N' WITH SID = ' + CONVERT(nvarchar(40), @sid, 1) + N', TYPE = E;';
    EXEC sys.sp_executesql @sql;
END;

IF IS_ROLEMEMBER('db_datareader', N'$(AppIdentityName)') = 0
    ALTER ROLE db_datareader ADD MEMBER [$(AppIdentityName)];

IF IS_ROLEMEMBER('db_datawriter', N'$(AppIdentityName)') = 0
    ALTER ROLE db_datawriter ADD MEMBER [$(AppIdentityName)];

-- Nothing else: the predicate function of Row-Level Security runs with its owner's rights (SCHEMABINDING), and
-- sp_set_session_context needs no permission.
