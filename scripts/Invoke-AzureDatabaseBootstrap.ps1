[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]{0,252}\.database\.windows\.net$')]
    [string]$Server,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,127}$')]
    [string]$Database,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,127}$')]
    [string]$WebUserName,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,127}$')]
    [string]$WorkerUserName,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,127}$')]
    [string]$WebRoleName,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,127}$')]
    [string]$WorkerRoleName,

    [Parameter(Mandatory)]
    [guid]$WebClientId,

    [Parameter(Mandatory)]
    [guid]$WorkerClientId,

    [Parameter(Mandatory)]
    [ValidatePattern('^APPROVAL-[A-Za-z0-9._-]+$')]
    [string]$ApprovalReference,

    [Parameter(Mandatory)]
    [switch]$ApprovedOperation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $ApprovedOperation) {
    throw 'An explicit exact-target approval is required. This script never infers approval from authentication, installed tools, or configuration.'
}
if ($WebUserName -eq $WorkerUserName) {
    throw 'Web and Worker users must be distinct managed identities.'
}
if ($WebRoleName -eq $WorkerRoleName) {
    throw 'Web and Worker roles must be distinct schema-managed roles.'
}

$sqlcmd = Get-Command -Name 'sqlcmd' -CommandType Application -ErrorAction SilentlyContinue
if ($null -eq $sqlcmd) {
    throw 'Microsoft Go sqlcmd 1.10.0 is required. Run Invoke-Doctor.ps1 -Profile Cloud for the exact CurrentUser repair command.'
}
$version = (& sqlcmd --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $version -notmatch '1\.10\.0') {
    throw "Microsoft Go sqlcmd 1.10.0 is required; observed '$version'."
}

function Convert-ClientIdToSqlSid {
    param([Parameter(Mandatory)] [guid]$ClientId)
    return '0x' + [Convert]::ToHexString($ClientId.ToByteArray())
}

$webSid = Convert-ClientIdToSqlSid $WebClientId
$workerSid = Convert-ClientIdToSqlSid $WorkerClientId

$bootstrapSql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF DATABASE_PRINCIPAL_ID(N'$WebRoleName') IS NULL
    THROW 51000, 'The required Web role does not exist. Apply the reviewed migration bundle before SQL bootstrap.', 1;
IF DATABASE_PRINCIPAL_ID(N'$WorkerRoleName') IS NULL
    THROW 51001, 'The required Worker role does not exist. Apply the reviewed migration bundle before SQL bootstrap.', 1;

IF DATABASE_PRINCIPAL_ID(N'$WebUserName') IS NULL
    CREATE USER [$WebUserName] WITH SID = $webSid, TYPE = E;
ELSE IF NOT EXISTS (
    SELECT 1 FROM sys.database_principals
    WHERE name = N'$WebUserName' AND type = 'E' AND sid = $webSid)
    THROW 51002, 'The existing Web user does not match the approved client ID.', 1;

IF DATABASE_PRINCIPAL_ID(N'$WorkerUserName') IS NULL
    CREATE USER [$WorkerUserName] WITH SID = $workerSid, TYPE = E;
ELSE IF NOT EXISTS (
    SELECT 1 FROM sys.database_principals
    WHERE name = N'$WorkerUserName' AND type = 'E' AND sid = $workerSid)
    THROW 51003, 'The existing Worker user does not match the approved client ID.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS membership
    INNER JOIN sys.database_principals AS role_principal ON role_principal.principal_id = membership.role_principal_id
    INNER JOIN sys.database_principals AS member_principal ON member_principal.principal_id = membership.member_principal_id
    WHERE role_principal.name = N'$WebRoleName' AND member_principal.name = N'$WebUserName')
    ALTER ROLE [$WebRoleName] ADD MEMBER [$WebUserName];

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS membership
    INNER JOIN sys.database_principals AS role_principal ON role_principal.principal_id = membership.role_principal_id
    INNER JOIN sys.database_principals AS member_principal ON member_principal.principal_id = membership.member_principal_id
    WHERE role_principal.name = N'$WorkerRoleName' AND member_principal.name = N'$WorkerUserName')
    ALTER ROLE [$WorkerRoleName] ADD MEMBER [$WorkerUserName];

COMMIT TRANSACTION;
"@

$bootstrapSql | & sqlcmd -S "tcp:$Server,1433" -d $Database --authentication-method ActiveDirectoryDefault -N -b
if ($LASTEXITCODE -ne 0) {
    throw "SQL bootstrap failed for the explicitly approved target '$Server/$Database'."
}

Write-Output "SQL runtime-role bootstrap completed for the approved target '$Server/$Database' under approval reference '$ApprovalReference'. No password, secret, or Azure login was supplied by this script."
