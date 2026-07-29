[CmdletBinding()]
param(
    [string]$ApprovalReference = $env:AZURE_DATABASE_ACCESS_APPROVAL_REFERENCE,

    [string]$EvidenceReference = $env:AZURE_DATABASE_ACCESS_EVIDENCE_REFERENCE,

    [switch]$ApprovedOperation = ($env:AZURE_DATABASE_ACCESS_APPROVED -eq 'true')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $ApprovedOperation) {
    throw 'Database access is blocked. Supply the explicit approved operation; Azure authentication and azd outputs never imply approval.'
}
if ($env:DEPLOYMENT_MODE -ne 'approved-live-deployment') {
    throw "Database access is blocked for deployment mode '$($env:DEPLOYMENT_MODE)'. Only the separately approved live deployment mode may grant runtime access."
}
if ($ApprovalReference -notmatch '^APPROVAL-[A-Za-z0-9._-]+$') {
    throw 'Database access is blocked. A recorded exact-target approval reference is required.'
}
if ($EvidenceReference -notmatch '^EVIDENCE-[A-Za-z0-9._-]+$') {
    throw 'Database access is blocked. A recorded exact-target evidence reference is required.'
}

$requiredOutputs = @(
    'AZURE_SQL_SERVER_FQDN',
    'AZURE_SQL_DATABASE_NAME',
    'WEB_IDENTITY_CLIENT_ID',
    'WEB_SQL_USER_NAME',
    'WORKER_IDENTITY_CLIENT_ID',
    'WORKER_SQL_USER_NAME'
)
$outputValues = @{}
foreach ($name in $requiredOutputs) {
    $value = [Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Required azd output '$name' is not available."
    }
    $outputValues[$name] = $value
}

if ($outputValues.AZURE_SQL_SERVER_FQDN -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{0,252}\.database\.windows\.net$') {
    throw 'AZURE_SQL_SERVER_FQDN is not an Azure SQL fully qualified domain name.'
}
if ($outputValues.AZURE_SQL_DATABASE_NAME -notmatch '^[A-Za-z][A-Za-z0-9_]{0,127}$') {
    throw 'AZURE_SQL_DATABASE_NAME is not a valid Azure SQL database identifier.'
}
foreach ($name in 'WEB_SQL_USER_NAME', 'WORKER_SQL_USER_NAME') {
    if ($outputValues[$name] -notmatch '^[A-Za-z][A-Za-z0-9_]{0,127}$') {
        throw "'$name' is not a valid Azure SQL contained-user identifier."
    }
}
if ($outputValues.WEB_SQL_USER_NAME -eq $outputValues.WORKER_SQL_USER_NAME) {
    throw 'Web and Worker SQL users must be distinct managed identities.'
}

$webClientId = [guid]::Empty
$workerClientId = [guid]::Empty
if (-not [guid]::TryParse($outputValues.WEB_IDENTITY_CLIENT_ID, [ref]$webClientId) -or
    -not [guid]::TryParse($outputValues.WORKER_IDENTITY_CLIENT_ID, [ref]$workerClientId) -or
    $webClientId -eq [guid]::Empty -or
    $workerClientId -eq [guid]::Empty -or
    $webClientId -eq $workerClientId) {
    throw 'Web and Worker managed-identity client IDs must be distinct non-empty GUIDs.'
}

$invokeSqlcmd = Get-Command -Name Invoke-Sqlcmd -CommandType Cmdlet -ErrorAction SilentlyContinue
if ($null -eq $invokeSqlcmd) {
    throw 'Invoke-Sqlcmd from the SqlServer PowerShell module is required for the approved database-access operation.'
}

$token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) {
    throw 'Could not acquire an Azure SQL access token for the explicitly approved target.'
}

function Convert-ClientIdToSqlSid {
    param([Parameter(Mandatory)] [guid]$ClientId)

    return '0x' + [Convert]::ToHexString($ClientId.ToByteArray())
}

function New-RuntimeGrantQuery {
    param(
        [Parameter(Mandatory)] [string]$UserName,
        [Parameter(Mandatory)] [guid]$ClientId
    )

    $escapedName = $UserName.Replace(']', ']]')
    $escapedLiteral = $UserName.Replace("'", "''")
    $sid = Convert-ClientIdToSqlSid $ClientId
    return @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF DATABASE_PRINCIPAL_ID(N'$escapedLiteral') IS NULL
    CREATE USER [$escapedName] WITH SID = $sid, TYPE = E;
ELSE IF NOT EXISTS (
    SELECT 1
    FROM sys.database_principals
    WHERE name = N'$escapedLiteral' AND type = 'E' AND sid = $sid)
    THROW 51000, 'The existing runtime user does not match its approved managed-identity client ID.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS membership
    INNER JOIN sys.database_principals AS role_principal ON role_principal.principal_id = membership.role_principal_id
    INNER JOIN sys.database_principals AS member_principal ON member_principal.principal_id = membership.member_principal_id
    WHERE role_principal.name = N'db_datareader' AND member_principal.name = N'$escapedLiteral')
    ALTER ROLE db_datareader ADD MEMBER [$escapedName];

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS membership
    INNER JOIN sys.database_principals AS role_principal ON role_principal.principal_id = membership.role_principal_id
    INNER JOIN sys.database_principals AS member_principal ON member_principal.principal_id = membership.member_principal_id
    WHERE role_principal.name = N'db_datawriter' AND member_principal.name = N'$escapedLiteral')
    ALTER ROLE db_datawriter ADD MEMBER [$escapedName];

COMMIT TRANSACTION;
"@
}

$serverInstance = "tcp:$($outputValues.AZURE_SQL_SERVER_FQDN),1433"
foreach ($runtimeIdentity in @(
        @{ UserName = $outputValues.WEB_SQL_USER_NAME; ClientId = $webClientId },
        @{ UserName = $outputValues.WORKER_SQL_USER_NAME; ClientId = $workerClientId }
    )) {
    Invoke-Sqlcmd `
        -ServerInstance $serverInstance `
        -Database $outputValues.AZURE_SQL_DATABASE_NAME `
        -AccessToken $token `
        -Query (New-RuntimeGrantQuery @runtimeIdentity) `
        -Encrypt Mandatory `
        -AbortOnError
}

Write-Output "Azure SQL runtime access was granted for the approved target under approval '$ApprovalReference' and evidence '$EvidenceReference'."
