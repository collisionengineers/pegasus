[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]{0,252}\.database\.windows\.net$')]
    [string]$Server,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,127}$')]
    [string]$Database,

    [Parameter(Mandatory)]
    [guid]$WebClientId,

    [Parameter(Mandatory)]
    [guid]$WorkerClientId,

    [Parameter(Mandatory)]
    [ValidatePattern('^APPROVAL-[A-Za-z0-9._-]+$')]
    [string]$ApprovalReference,

    [Parameter(Mandatory)]
    [ValidatePattern('^EVIDENCE-[A-Za-z0-9._-]+$')]
    [string]$EvidenceReference,

    [string]$DeploymentMode = $env:DEPLOYMENT_MODE,

    [Parameter(Mandatory)]
    [switch]$ApprovedOperation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$webUserName = 'pegasus_web_runtime'
$workerUserName = 'pegasus_worker_runtime'
$webRoleName = 'pegasus_web_runtime_role'
$workerRoleName = 'pegasus_worker_runtime_role'

function Get-RequiredApplication {
    param([Parameter(Mandatory)][string]$Name)

    $command = Get-Command -Name $Name -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $command) {
        throw "$Name is required. Install the pinned SQL command-line tooling before retrying."
    }

    return $command
}

function ConvertTo-SqlGuidSidHex {
    param([Parameter(Mandatory)][guid]$Value)

    return '0x' + [Convert]::ToHexString($Value.ToByteArray())
}

if ($DeploymentMode -ne 'approved-live-deployment') {
    throw "Azure SQL bootstrap is blocked for deployment mode '$DeploymentMode'."
}
if (-not $ApprovedOperation.IsPresent) {
    throw 'Azure SQL bootstrap is blocked without -ApprovedOperation for the exact approved target.'
}
if ($WebClientId -eq [guid]::Empty -or $WorkerClientId -eq [guid]::Empty) {
    throw 'WebClientId and WorkerClientId must both be non-empty managed-identity client IDs.'
}
if ($WebClientId -eq $WorkerClientId) {
    throw 'Web and Worker managed-identity client IDs must be distinct.'
}

$sqlcmd = Get-RequiredApplication -Name 'sqlcmd'
& $sqlcmd.Source '--version' | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "sqlcmd failed its version check with exit code $LASTEXITCODE."
}

$webSid = ConvertTo-SqlGuidSidHex -Value $WebClientId
$workerSid = ConvertTo-SqlGuidSidHex -Value $WorkerClientId
$bootstrapSql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @webSid varbinary(16) = $webSid;
DECLARE @workerSid varbinary(16) = $workerSid;
DECLARE @dboPrincipalId int = DATABASE_PRINCIPAL_ID(N'dbo');
DECLARE @webRoleId int = DATABASE_PRINCIPAL_ID(N'$webRoleName');
DECLARE @workerRoleId int = DATABASE_PRINCIPAL_ID(N'$workerRoleName');

IF @webRoleId IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM sys.database_principals
       WHERE principal_id = @webRoleId
         AND [type] = 'R'
         AND is_fixed_role = 0
         AND owning_principal_id = @dboPrincipalId)
    THROW 51000, N'The migration-managed Pegasus Web runtime role is missing or invalid.', 1;
IF @workerRoleId IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM sys.database_principals
       WHERE principal_id = @workerRoleId
         AND [type] = 'R'
         AND is_fixed_role = 0
         AND owning_principal_id = @dboPrincipalId)
    THROW 51000, N'The migration-managed Pegasus Worker runtime role is missing or invalid.', 1;
IF @webRoleId = @workerRoleId
    THROW 51000, N'The Web and Worker runtime roles must be distinct.', 1;
IF EXISTS (
    SELECT 1
    FROM sys.database_permissions
    WHERE grantee_principal_id IN (@webRoleId, @workerRoleId)
      AND (class <> 1
           OR minor_id <> 0
           OR [state] NOT IN ('G', 'D')
           OR permission_name NOT IN (N'SELECT', N'INSERT', N'UPDATE', N'DELETE')
           OR ([state] = 'D' AND permission_name <> N'DELETE')))
    THROW 51000, N'Runtime roles may contain only migration-managed object-level DML grants.', 1;
IF EXISTS (
    SELECT 1 FROM sys.schemas WHERE principal_id IN (@webRoleId, @workerRoleId))
   OR EXISTS (
    SELECT 1
    FROM sys.database_principals
    WHERE owning_principal_id IN (@webRoleId, @workerRoleId))
    THROW 51000, N'Runtime roles must not own schemas or database principals.', 1;

IF EXISTS (
    SELECT 1
    FROM sys.database_principals
    WHERE sid = @webSid AND name <> N'$webUserName')
    THROW 51000, N'The Web managed-identity SID is already bound to another database principal.', 1;
IF EXISTS (
    SELECT 1
    FROM sys.database_principals
    WHERE sid = @workerSid AND name <> N'$workerUserName')
    THROW 51000, N'The Worker managed-identity SID is already bound to another database principal.', 1;

IF DATABASE_PRINCIPAL_ID(N'$webUserName') IS NULL
    CREATE USER [$webUserName] WITH SID = $webSid, TYPE = E;
IF DATABASE_PRINCIPAL_ID(N'$workerUserName') IS NULL
    CREATE USER [$workerUserName] WITH SID = $workerSid, TYPE = E;

DECLARE @webUserId int = DATABASE_PRINCIPAL_ID(N'$webUserName');
DECLARE @workerUserId int = DATABASE_PRINCIPAL_ID(N'$workerUserName');
IF @webUserId IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM sys.database_principals
       WHERE principal_id = @webUserId
         AND [type] = 'E'
         AND authentication_type = 4
         AND DATALENGTH(sid) = 16
         AND sid = @webSid)
    THROW 51000, N'The existing Web database user does not match the approved managed identity.', 1;
IF @workerUserId IS NULL
   OR NOT EXISTS (
       SELECT 1
       FROM sys.database_principals
       WHERE principal_id = @workerUserId
         AND [type] = 'E'
         AND authentication_type = 4
         AND DATALENGTH(sid) = 16
         AND sid = @workerSid)
    THROW 51000, N'The existing Worker database user does not match the approved managed identity.', 1;
IF @webUserId = @workerUserId
    THROW 51000, N'The Web and Worker managed identities must use distinct database users.', 1;

IF EXISTS (
    SELECT 1
    FROM sys.database_permissions
    WHERE grantee_principal_id IN (@webUserId, @workerUserId))
    THROW 51000, N'Runtime database users must not have direct permissions.', 1;
IF EXISTS (
    SELECT 1 FROM sys.schemas WHERE principal_id IN (@webUserId, @workerUserId))
   OR EXISTS (
    SELECT 1
    FROM sys.database_principals
    WHERE owning_principal_id IN (@webUserId, @workerUserId))
    THROW 51000, N'Runtime database users must not own schemas or database principals.', 1;
IF EXISTS (
    SELECT 1
    FROM sys.database_role_members
    WHERE member_principal_id = @webRoleId
       OR member_principal_id = @workerRoleId)
    THROW 51000, N'Runtime roles must not be nested in broader database roles.', 1;
IF EXISTS (
    SELECT 1
    FROM sys.database_role_members
    WHERE (role_principal_id = @webRoleId AND member_principal_id <> @webUserId)
       OR (role_principal_id = @workerRoleId AND member_principal_id <> @workerUserId))
    THROW 51000, N'Runtime roles contain an unexpected database principal.', 1;
IF EXISTS (
    SELECT 1
    FROM sys.database_role_members
    WHERE (member_principal_id = @webUserId AND role_principal_id <> @webRoleId)
       OR (member_principal_id = @workerUserId AND role_principal_id <> @workerRoleId))
    THROW 51000, N'A runtime database user is already assigned to a broader or incorrect role.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members
    WHERE role_principal_id = @webRoleId
      AND member_principal_id = @webUserId)
    ALTER ROLE [$webRoleName] ADD MEMBER [$webUserName];
IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members
    WHERE role_principal_id = @workerRoleId
      AND member_principal_id = @workerUserId)
    ALTER ROLE [$workerRoleName] ADD MEMBER [$workerUserName];

IF (SELECT COUNT(*) FROM sys.database_role_members WHERE role_principal_id = @webRoleId) <> 1
   OR (SELECT COUNT(*) FROM sys.database_role_members WHERE role_principal_id = @workerRoleId) <> 1
   OR (SELECT COUNT(*) FROM sys.database_role_members WHERE member_principal_id = @webUserId) <> 1
   OR (SELECT COUNT(*) FROM sys.database_role_members WHERE member_principal_id = @workerUserId) <> 1
    THROW 51000, N'Runtime role membership verification failed.', 1;

COMMIT TRANSACTION;
"@

$queryPath = Join-Path ([System.IO.Path]::GetTempPath()) (
    "pegasus-azure-sql-bootstrap-$([guid]::NewGuid().ToString('N')).sql")
try {
    [System.IO.File]::WriteAllText(
        $queryPath,
        $bootstrapSql,
        [System.Text.UTF8Encoding]::new($false))
    $arguments = @(
        '-S', "tcp:$Server,1433",
        '-d', $Database,
        '--authentication-method', 'ActiveDirectoryDefault',
        '-N',
        '-b',
        '-r', '1',
        '-i', $queryPath)
    & $sqlcmd.Source @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "SQL bootstrap failed for the approved target '$Server/$Database' with exit code $LASTEXITCODE."
    }
}
finally {
    if ([System.IO.File]::Exists($queryPath)) {
        [System.IO.File]::Delete($queryPath)
    }
}

Write-Output "Azure SQL runtime identities are bound to the fixed least-privilege roles for '$Server/$Database'."
Write-Output "Approval reference: $ApprovalReference"
Write-Output "Evidence reference: $EvidenceReference"
