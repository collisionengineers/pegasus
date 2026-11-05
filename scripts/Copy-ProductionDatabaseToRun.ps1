<#
    .SYNOPSIS
    Copies the production database into a hosted test instance's run database
    for release rehearsal (ADR-0088): a read-only SqlPackage extract of
    production to a local .dacpac, published over the run's own database, then
    every approved mailbox deactivated so the test Worker polls nothing until
    an Administrator re-enables a mailbox there.

    .DESCRIPTION
    Nothing is read until -Approve is given; without it the script prints the
    exact source, target and post-copy statement and exits 2. The extract is a
    live read of production under the operator's own az sign-in (the server is
    Entra-only; never a SQL login) and writes only beneath the run root; no
    storage account is written and nothing in Azure changes. The run must be
    Stopped; its database container is started for the publish and stopped
    again, and the run manifest is not touched.

    The hosted test instance's host only (R694): the copy refuses any other
    machine before printing its plan, and the run must be the instance's (its
    manifest records the instance's public origin). The extract is removed
    once published, whether or not the publish succeeded, so the run database
    is the only copy of production on the machine; the record keeps the
    extract's size and SHA-256. Linux only: the hosted test instance runs on
    the container engine, and the post-copy statements run inside the
    container with sqlcmd reading the sa password from the container's own
    environment. SqlPackage's publish has no
    such route, so the run's sa password is on that one host command line;
    anyone who can query the container runtime can read it anyway (runbook,
    "Local database").

    Users, logins, role memberships and permissions are not published: the
    production users are Entra external users a SQL Server container cannot
    create, and the run's hosts connect as sa. Database options are not
    scripted either, since Azure SQL edition settings do not apply here. The
    data arrives with __EFMigrationsHistory, so the next Start migrates the
    copy forward to the checked-out source.

    .EXAMPLE
    pwsh ./scripts/Copy-ProductionDatabaseToRun.ps1 -RunId <run-id>
    pwsh ./scripts/Copy-ProductionDatabaseToRun.ps1 -RunId <run-id> -Approve
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{32}$')]
    [string]$RunId,
    [string]$SqlPackage = 'sqlpackage',
    [ValidateRange(60, 7200)]
    [int]$TimeoutSeconds = 1800,
    [switch]$Approve
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')

# The fixed production source (the release skill's target table). Read only.
$subscriptionId = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$tenantId = '858cf5b3-aa0a-47a6-9b40-4851fd0afa94'
$sqlServer = 'pegasus-prod-sql-252ow37gij.database.windows.net'
$sqlDatabase = 'pegasus'
$excludedObjectTypes = 'Users;Logins;RoleMembership;Permissions'

if ((Get-PegasusDatabaseEngineKind) -ne 'DockerSqlServer') {
    throw 'This copy supports the Linux container engine the hosted test instance runs on (ADR-0088).'
}
Assert-PegasusTestInstanceHost -Operation 'The production copy'

$runRoot = Join-Path (Join-Path $repositoryRoot 'artifacts/local-development') $RunId
$manifestPath = Join-Path $runRoot 'run-manifest.json'
if (-not [System.IO.File]::Exists($manifestPath)) {
    throw "No owned run manifest exists for run '$RunId': $manifestPath"
}
$manifest = [System.IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -Depth 30
if ([string]$manifest.runId -ne $RunId -or $manifest.kind -ne 'Pegasus.LocalDevelopment.Run') {
    throw "The manifest at $manifestPath does not describe run '$RunId'."
}
if ([string]$manifest.state -ne 'Stopped') {
    throw "Run '$RunId' is '$($manifest.state)'; stop it first: pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Stop -RunId $RunId"
}
if (-not $manifest.resources.database.created) {
    throw "Run '$RunId' has no database yet; Start it once so the lifecycle owns one, then Stop it."
}
$runOrigin = $manifest.runtime.PSObject.Properties['publicOrigin']
if ($null -eq $runOrigin -or -not (Test-PegasusTestInstanceOrigin -Origin ([string]$runOrigin.Value))) {
    throw "Run '$RunId' is not the hosted test instance's run: its manifest records no public origin of $(Get-PegasusTestInstanceOrigin). Production is copied only into the instance (ADR-0088)."
}

$databaseName = [string]$manifest.resources.database.databaseName
if ($databaseName -ne "PegasusDevelopment_$RunId") {
    throw "Unexpected run database name '$databaseName'."
}
$containerName = Get-PegasusDatabaseContainerName -RunId $RunId
$databasePort = [int]$manifest.resources.ports.database
$secretPath = [string]$manifest.resources.database.secretPath
$stamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$dacpacPath = Join-Path $runRoot "state/pegasus-production-$stamp.dacpac"
$recordPath = Join-Path $runRoot "state/production-copy-$stamp.json"
$deactivateStatement = 'UPDATE dbo.ApprovedMailboxes SET ActivatedAtUtc = NULL WHERE ActivatedAtUtc IS NOT NULL;'
# R678: a copied subscription row would make the instance renew production's
# subscription instead of creating its own under its own webhook.
$clearSubscriptionsStatement = 'DELETE FROM dbo.ApprovedMailboxSubscriptions;'
# R679: production's Principal API keys must not authenticate against the instance.
$pauseCredentialsStatement = "UPDATE dbo.PrincipalApiCredentials SET State = 'Paused', PausedAtUtc = SYSUTCDATETIME(), Version = Version + 1 WHERE State = 'Active';"
# R680: the answered-instruction tidy does not check activation, so the copied
# pending tidies are closed here; the instance then moves only the instructions
# its own sends answered.
$closeTidiesStatement = "UPDATE dbo.StaffMailSendOperations SET InstructionMoveState = 'Failed', InstructionMoveFailureCode = 'production_copy' WHERE InstructionMoveState IS NULL AND Purpose = 'CaseReport' AND State = 'Sent';"

Write-Host "Source:   $sqlServer / $sqlDatabase (read-only extract under your az sign-in)"
Write-Host "Dacpac:   $dacpacPath (removed once published; the run database is the only copy)"
Write-Host "Target:   container $containerName, database $databaseName (replaced)"
Write-Host "Then:     $deactivateStatement"
Write-Host "          $clearSubscriptionsStatement"
Write-Host "          $pauseCredentialsStatement"
Write-Host "          $closeTidiesStatement"
Write-Host "Skipped:  $excludedObjectTypes; database options"
if (-not $Approve) {
    Write-Host 'Nothing was read or written. Re-run with -Approve to copy.'
    exit 2
}

function Get-RequiredApplication {
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$Repair)

    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $command) {
        throw "$Name is required. Repair: $Repair"
    }
    return [string]$command.Source
}

$sqlPackageCommand = Get-RequiredApplication -Name $SqlPackage -Repair 'dotnet tool install --global microsoft.sqlpackage'
$docker = Get-RequiredApplication -Name (Get-PegasusDatabaseCommandName) -Repair (Get-PegasusRepairHint -Id 'database-engine')
$az = Get-RequiredApplication -Name 'az' -Repair 'install the Azure CLI and sign in with az login'

$account = & $az account show --output json 2>$null | Out-String
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($account)) {
    throw 'az account show failed: sign in with az login before reading production.'
}
$accountInfo = $account | ConvertFrom-Json
if ([string]$accountInfo.id -ne $subscriptionId -or [string]$accountInfo.tenantId -ne $tenantId) {
    throw "The az account is not the Pegasus subscription: $($accountInfo.id) in tenant $($accountInfo.tenantId)."
}
Write-Host "Azure account: $($accountInfo.user.name) ($($accountInfo.name))"

function Invoke-RunSql {
    <#
        .SYNOPSIS
        Runs one statement inside the container against the run database; the
        sa password is read from the container's environment, never passed in.
    #>
    param([Parameter(Mandatory)][string]$Query)

    if ($Query.Contains('"')) {
        throw 'Run SQL statements may not contain double quotes.'
    }
    # -I: QUOTED_IDENTIFIER on. sqlcmd's default is off, and the copied schema's
    # filtered indexes refuse an UPDATE under it (first run, 3 November 2026).
    $script = 'exec 2>&1; /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -I -l 30 -h -1 -W -d "' +
        $databaseName + '" -Q "SET NOCOUNT ON; ' + $Query + '"'
    $output = (& $docker exec $containerName bash -c $script 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "A run SQL statement failed (exit $LASTEXITCODE): $output"
    }
    return $output
}

function Wait-RunDatabase {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(180)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-PegasusDatabaseReady -Command $docker -ContainerName $containerName -Port $databasePort -Database '') {
            return
        }
        Start-Sleep -Milliseconds 500
    }
    throw "The run database container '$containerName' did not become ready."
}

# The extract is production's data in one file; it lives only between the
# extract and the publish, and is removed on any exit.
function Remove-Extract {
    if ([System.IO.File]::Exists($dacpacPath)) {
        [System.IO.File]::Delete($dacpacPath)
        Write-Host "Removed the extract $dacpacPath."
    }
}

try {
# 1. Extract production, read only, to the local file. The connection carries
#    no secret: Active Directory Default takes the operator's az sign-in.
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($dacpacPath)) | Out-Null
$env:AZURE_TOKEN_CREDENTIALS = 'AzureCliCredential'
try {
    $sourceConnection = "Server=tcp:$sqlServer,1433;Initial Catalog=$sqlDatabase;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connect Timeout=60"
    Write-Host 'Extracting production (schema and all table data)...'
    & $sqlPackageCommand /Action:Extract `
        "/SourceConnectionString:$sourceConnection" `
        "/TargetFile:$dacpacPath" `
        /p:ExtractAllTableData=true `
        /p:IgnorePermissions=true `
        /p:IgnoreUserLoginMappings=true `
        /p:VerifyExtraction=true `
        "/p:CommandTimeout=$TimeoutSeconds"
    if ($LASTEXITCODE -ne 0) {
        throw "SqlPackage Extract failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item Env:AZURE_TOKEN_CREDENTIALS -ErrorAction SilentlyContinue
}
$dacpacSha256 = (Get-FileHash -LiteralPath $dacpacPath -Algorithm SHA256).Hash.ToLowerInvariant()
$dacpacBytes = [System.IO.FileInfo]::new($dacpacPath).Length
Write-Host "Extracted $dacpacBytes bytes, SHA-256 $dacpacSha256."

# 2. Publish over the run database. CreateNewDatabase replaces the existing
#    run database, so no separate DROP is needed and the run keeps its name.
Start-PegasusDatabaseInstance -InstanceName $databaseName -Command $docker -ContainerName $containerName -RunId $RunId
try {
    Wait-RunDatabase
    $password = Read-PegasusDatabaseSecretFile -Path $secretPath
    $targetConnection = Get-PegasusDatabaseConnectionString `
        -InstanceName $databaseName -DatabaseName $databaseName -Port $databasePort -Password $password
    Write-Host "Publishing into $databaseName..."
    & $sqlPackageCommand /Action:Publish `
        "/SourceFile:$dacpacPath" `
        "/TargetConnectionString:$targetConnection" `
        /p:CreateNewDatabase=true `
        /p:BlockOnPossibleDataLoss=false `
        /p:AllowIncompatiblePlatform=true `
        /p:ScriptDatabaseOptions=false `
        "/p:ExcludeObjectTypes=$excludedObjectTypes" `
        "/p:CommandTimeout=$TimeoutSeconds"
    if ($LASTEXITCODE -ne 0) {
        throw "SqlPackage Publish failed with exit code $LASTEXITCODE."
    }

    # 3. Deactivate every approved mailbox (R627): the copy carries production's
    #    activated rows, and the test Worker must poll nothing until an
    #    Administrator enables a mailbox on the instance. Then clear the copied
    #    subscriptions (R678), pause the copied Principal API keys (R679) and
    #    close the copied pending instruction tidies (R680).
    $deactivated = Invoke-RunSql -Query ($deactivateStatement + ' SELECT @@ROWCOUNT;')
    $subscriptionsCleared = Invoke-RunSql -Query ($clearSubscriptionsStatement + ' SELECT @@ROWCOUNT;')
    $credentialsPaused = Invoke-RunSql -Query ($pauseCredentialsStatement + ' SELECT @@ROWCOUNT;')
    $tidiesClosed = Invoke-RunSql -Query ($closeTidiesStatement + ' SELECT @@ROWCOUNT;')
    $migrationHead = Invoke-RunSql -Query 'SELECT TOP 1 MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC;'
    $caseCount = Invoke-RunSql -Query 'SELECT COUNT(*) FROM dbo.Cases;'
}
finally {
    Stop-PegasusDatabaseInstance -InstanceName $databaseName -Command $docker -ContainerName $containerName -RunId $RunId
}
}
finally {
    Remove-Extract
}

$record = [ordered]@{
    kind = 'Pegasus.LocalDevelopment.ProductionCopy'
    runId = $RunId
    copiedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    source = [ordered]@{ server = $sqlServer; database = $sqlDatabase; account = [string]$accountInfo.user.name }
    dacpac = [ordered]@{ sha256 = $dacpacSha256; bytes = [int64]$dacpacBytes; retained = $false }
    excludedObjectTypes = $excludedObjectTypes
    migrationHead = $migrationHead
    caseCount = [int]$caseCount
    mailboxesDeactivated = [int]$deactivated
    subscriptionsCleared = [int]$subscriptionsCleared
    principalCredentialsPaused = [int]$credentialsPaused
    instructionTidiesClosed = [int]$tidiesClosed
}
[System.IO.File]::WriteAllText($recordPath, (($record | ConvertTo-Json -Depth 5) + "`n"), [System.Text.UTF8Encoding]::new($false))

Write-Host "Copied. Migration head $migrationHead; $caseCount Cases; $deactivated mailbox row(s) deactivated; $subscriptionsCleared subscription row(s) cleared; $credentialsPaused Principal API key(s) paused; $tidiesClosed pending instruction tidy(ies) closed."
Write-Host "Record: $recordPath"
Write-Host "Next: pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start -RunId $RunId  (migrates the copy forward), then enable a mailbox in Administration > Mailboxes for intake and send."
