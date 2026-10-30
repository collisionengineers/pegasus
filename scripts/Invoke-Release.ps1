#!/usr/bin/env pwsh
<#
.SYNOPSIS
The Pegasus release route, in one script run twice: a read-only run that
preflights, builds and prints the approval request, then an approved run
that promotes, deploys and prints the record.

.DESCRIPTION
The default run writes nothing to GitHub or Azure. It fetches both remote
heads, lists the candidate range and its pull requests, reads the deployed
state and the live migration head, says which route the candidate needs and
whether provisioning is needed, builds the exact-SHA artifacts in a detached
worktree, validates them, and prints the one approval request.

-Execute is the approved run. Its parameters are the words of the approval:
the exact SHA, the manifest SHA-256, the route, whether to provision and, for
a destructive route, the old serving Web source SHA. It promotes dev to main
with a leased atomic push, runs the one route to a passing full smoke, copies
the azd environment back to the primary checkout, retains the artifacts and
prints the record skeleton for docs/operations.md.

A destructive run that failed after containment is resumed with the same
-Execute line: the script reads where it is (Web Stopped and the migration
head) and continues forward. It never contains twice and never starts old
bytes after SQL began (ADR-0046).

.EXAMPLE
pwsh ./scripts/Invoke-Release.ps1

.EXAMPLE
pwsh ./scripts/Invoke-Release.ps1 -Execute -ReleaseNumber 130 -Route Normal `
  -ApprovedReleaseSha <sha> -ApprovedManifestSha256 <hash> -Provision

.EXAMPLE
pwsh ./scripts/Invoke-Release.ps1 -Execute -ReleaseNumber 130 -Route Destructive `
  -ApprovedReleaseSha <sha> -ApprovedManifestSha256 <hash> -ApprovedOldWebSourceSha <sha>

.EXAMPLE
pwsh ./scripts/Invoke-Release.ps1 -PromoteOnly -ApprovedReleaseSha <sha>
#>
[CmdletBinding(DefaultParameterSetName = 'Prepare')]
param(
    # Read-only run: stop after the preflight report, without building.
    [Parameter(ParameterSetName = 'Prepare')]
    [switch] $PreflightOnly,

    # The approved run.
    [Parameter(Mandatory, ParameterSetName = 'Execute')]
    [switch] $Execute,

    [Parameter(Mandatory, ParameterSetName = 'Execute')]
    [ValidateSet('Normal', 'Destructive')]
    [string] $Route,

    # The release number the record will carry; names the retained folder.
    [Parameter(Mandatory, ParameterSetName = 'Execute')]
    [ValidateRange(1, 100000)]
    [int] $ReleaseNumber,

    [Parameter(Mandatory, ParameterSetName = 'Execute')]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string] $ApprovedManifestSha256,

    # The approval said provisioning is needed (the preflight report says
    # whether it is). Without it the normal route deploys packages only.
    [Parameter(ParameterSetName = 'Execute')]
    [switch] $Provision,

    # Destructive route only: the source SHA the serving Web App reported in
    # the preflight report, which the approval named.
    [Parameter(ParameterSetName = 'Execute')]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $ApprovedOldWebSourceSha,

    # Normal route only: the release adds or changes WEBSITE_WARMUP_PATH or
    # WEBSITE_WARMUP_STATUSES, so the package deploys before provisioning.
    [Parameter(ParameterSetName = 'Execute')]
    [switch] $WarmupSettingsChange,

    # Promote the exact SHA and stop: no build, no Azure write.
    [Parameter(Mandatory, ParameterSetName = 'PromoteOnly')]
    [switch] $PromoteOnly,

    [Parameter(Mandatory, ParameterSetName = 'Execute')]
    [Parameter(Mandatory, ParameterSetName = 'PromoteOnly')]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $ApprovedReleaseSha,

    [ValidatePattern('^\d+\.\d+\.\d+-alpha\.\d+$')]
    [string] $Version = '0.1.0-alpha.1',

    # The primary checkout. Git runs against it; the release worktree sits
    # beside it under ../pegasus-worktrees.
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    # Seconds between state read-backs. The bounded waits are 12 or 24 reads.
    [ValidateRange(0, 60)]
    [int] $PollSeconds = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Native exit codes are read explicitly after every call.
$PSNativeCommandUseErrorActionPreference = $false

# --- Fixed production target ------------------------------------------------

$releaseEnvironment = 'pegasus-prod'
$subscriptionId = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$tenantId = '858cf5b3-aa0a-47a6-9b40-4851fd0afa94'
$resourceGroup = 'rg-pegasus-prod'
$webApp = 'pegasus-prod-web-252ow37gij'
$webOrigin = 'https://pegasus-prod-web-252ow37gij.azurewebsites.net/'
$workerApp = 'pegasus-prod-worker-252ow37gij'
$classifierApp = 'pegasus-prod-classifier-252ow37gij'
$keyVault = 'pegasusprodkv252ow37g'
$webStack = 'DOTNETCORE|10.0'
$gitHubRepository = 'collisionengineers/pegasus'

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
. (Join-Path $RepositoryRoot 'scripts/PegasusPlatform.ps1')
$migrationBundle = Get-PegasusMigrationBundle

# A path under src/, infra/ or the solution's dependency files is a deployable
# change; anything else (documentation, tests, skills, release tooling) alone
# is a promotion-only candidate.
$deployablePattern = '^(src/|infra/|azure\.yaml$|global\.json$|Directory\.[A-Za-z]+\.props$|nuget\.config$)'
$provisionPattern = '^(infra/|azure\.yaml$)'
$migrationsDirectory = 'src/Pegasus.Infrastructure/Persistence/Migrations'

# --- Native command helpers -------------------------------------------------

function Invoke-Git {
    param([Parameter(Mandatory)][string[]] $Arguments, [string] $Failure)
    $output = @(& git -C $RepositoryRoot @Arguments 2>&1 | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0) {
        $detail = if ($Failure) { $Failure } else { "git $($Arguments -join ' ') failed." }
        throw "$detail`n$($output -join "`n")"
    }
    return $output
}

function Invoke-Az {
    # Runs the Azure CLI and throws on a non-zero exit. Output is returned as
    # one string so JSON can be parsed and tsv trimmed.
    param([Parameter(Mandatory)][string[]] $Arguments, [Parameter(Mandatory)][string] $Failure)
    $output = (& az @Arguments) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw $Failure }
    return $output
}

function Invoke-AzJson {
    param([Parameter(Mandatory)][string[]] $Arguments, [Parameter(Mandatory)][string] $Failure)
    $json = Invoke-Az -Arguments ($Arguments + @('--output', 'json')) -Failure $Failure
    try { return $json | ConvertFrom-Json }
    catch { throw "$Failure (the read-back was not valid JSON)." }
}

function Invoke-Azd {
    param([Parameter(Mandatory)][string[]] $Arguments, [Parameter(Mandatory)][string] $Failure)
    $output = @(& azd @Arguments 2>&1 | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0) { throw "$Failure`n$($output -join "`n")" }
    return $output
}

function Invoke-ReleaseScript {
    # Runs one of the repository scripts at the release SHA in a child pwsh
    # with the worktree as its working directory, as the skill always did.
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][string] $Failure
    )
    Push-Location $Root
    try {
        # The child's lines go to the host, never into a caller's return value.
        & pwsh -NoLogo -NoProfile -File (Join-Path $Root "scripts/$Name") @Arguments | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -ne 0) { throw $Failure }
    }
    finally {
        Pop-Location
    }
}

function Wait-Poll {
    if ($PollSeconds -gt 0) { Start-Sleep -Seconds $PollSeconds }
}

# --- Read-only probes ----------------------------------------------------------

function Get-ProbeUri {
    # The two-argument [uri] constructor with a [uri] base keeps the relative
    # path. With a string first argument PowerShell binds the obsolete
    # Uri(string, bool) overload, drops the path and probes '/', which
    # redirects to sign-in and makes a healthy site read as not ready
    # (Releases 52 and 54).
    param([Parameter(Mandatory)][string] $Path)
    return [uri]::new([uri]$webOrigin, $Path).AbsoluteUri
}

function Test-WebAnswers {
    # True when the anonymous endpoint answers 2xx. The Azure CLI makes the
    # request without a token, so this needs nothing beyond the CLI.
    param([Parameter(Mandatory)][string] $Path)
    & az rest --method get --url (Get-ProbeUri $Path) --skip-authorization-header --output none 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}

function Get-WebVersion {
    # The running bytes identify themselves at /diagnostics/version; null when
    # the site does not answer or answers something else.
    $json = (& az rest --method get --url (Get-ProbeUri 'diagnostics/version') --skip-authorization-header --output json 2>$null) -join "`n"
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) { return $null }
    try { return $json | ConvertFrom-Json }
    catch { return $null }
}

function Get-WebSite {
    return Invoke-AzJson -Failure "Unable to read the Web App $webApp." -Arguments @(
        'webapp', 'show', '--subscription', $subscriptionId, '--resource-group', $resourceGroup, '--name', $webApp,
        '--query', '{state:state,stack:siteConfig.linuxFxVersion,host:defaultHostName}')
}

function Get-FunctionAppState {
    # A Flex Consumption app reports no state through az functionapp show.
    param([Parameter(Mandatory)][string] $Name)
    return (Invoke-Az -Failure "Unable to read the state of $Name." -Arguments @(
        'resource', 'show', '--subscription', $subscriptionId, '--resource-group', $resourceGroup,
        '--name', $Name, '--resource-type', 'Microsoft.Web/sites', '--api-version', '2024-04-01',
        '--query', 'properties.state', '--output', 'tsv')).Trim()
}

function Wait-FunctionAppState {
    param([Parameter(Mandatory)][string] $Name, [Parameter(Mandatory)][string] $Expected)
    for ($attempt = 1; $attempt -le 12; $attempt++) {
        if ((Get-FunctionAppState $Name) -ceq $Expected) { return }
        if ($attempt -lt 12) { Wait-Poll }
    }
    throw "$Name did not read back as $Expected."
}

function Test-ClassifierPresent {
    $listed = Invoke-Az -Failure 'Unable to read whether the classifier Function App exists.' -Arguments @(
        'functionapp', 'list', '--subscription', $subscriptionId, '--resource-group', $resourceGroup,
        '--query', "[?name=='$classifierApp'].name", '--output', 'tsv')
    $names = @($listed -split "`n" | ForEach-Object { $_.Trim() })
    return $names -ccontains $classifierApp
}

function Get-LiveWorkerActivation {
    # What the live Disabled census says now: every member true is
    # 'disabled', every member false is 'approved-live-worker'. A resumed run
    # reads this instead of assuming where the previous run stopped.
    $expected = @(Get-PegasusWorkerDisabledSettingNames)
    $settings = @(Invoke-AzJson -Failure 'Unable to read the Worker Disabled census.' -Arguments @(
        'functionapp', 'config', 'appsettings', 'list', '--subscription', $subscriptionId, '--resource-group', $resourceGroup,
        '--name', $workerApp, '--query', "[?starts_with(name, 'AzureWebJobs.') && ends_with(name, '.Disabled')].{name:name,value:value}"))
    $values = @($settings | Where-Object { $expected -ccontains [string]$_.name } | ForEach-Object { [string]$_.value } | Sort-Object -Unique)
    if ($values.Count -ne 1 -or $settings.Count -ne $expected.Count) { throw 'The live Worker Disabled census is neither wholly true nor wholly false.' }
    return $(if ($values[0] -ceq 'true') { 'disabled' } else { 'approved-live-worker' })
}

function Wait-WebStopped {
    # Containment evidence is the site Stopped and its public origin no longer
    # serving the application: /health/live must not answer 2xx (a stopped
    # site answers 403). A disabled trigger or a missing response is not it.
    for ($attempt = 1; $attempt -le 12; $attempt++) {
        $state = [string](Get-WebSite).state
        if ($state -cnotin @('Stopped', 'Running')) { throw "Web App reported an unexpected state '$state' during containment." }
        if ($state -ceq 'Stopped' -and -not (Test-WebAnswers 'health/live')) { return }
        if ($attempt -lt 12) { Wait-Poll }
    }
    throw 'Old Web did not read back Stopped and unserved.'
}

function Wait-PegasusExpectedWebSite {
    # The release read-back: the site Running on the platform stack,
    # /health/ready answering 200 and the version endpoint reporting the exact
    # release. A site reporting an earlier SHA is still serving the previous
    # package; a site whose health check fails is recycled by the platform
    # and is not evidence.
    param([Parameter(Mandatory)][string] $ExpectedSourceRevision, [Parameter(Mandatory)][string] $ExpectedVersion)
    for ($attempt = 1; $attempt -le 24; $attempt++) {
        $site = Get-WebSite
        if ([string]$site.stack -cne $webStack) { throw "Web App stack is '$($site.stack)', not $webStack." }
        $state = [string]$site.state
        if ($state -ceq 'Running') {
            if (Test-WebAnswers 'health/ready') {
                $reported = Get-WebVersion
                if ($null -ne $reported -and
                    [string]$reported.sourceSha -ceq $ExpectedSourceRevision -and
                    [string]$reported.version -ceq $ExpectedVersion) {
                    Write-Host "Web App read back Running, ready and at $ExpectedSourceRevision / $ExpectedVersion on attempt $attempt."
                    return
                }
            }
        }
        elseif ($state -cnotin @('Stopped', 'Starting')) {
            throw "Web App reported an unexpected state '$state'."
        }
        if ($attempt -lt 24) { Wait-Poll }
    }
    throw 'The Web App did not become Running, ready and at the approved release within the bounded wait.'
}

function Get-AzdValues {
    param([Parameter(Mandatory)][string] $Root)
    Push-Location $Root
    try {
        $lines = Invoke-Azd -Failure 'azd env get-values failed.' -Arguments @('env', 'get-values', '-e', $releaseEnvironment, '--no-prompt')
    }
    finally {
        Pop-Location
    }
    $values = @{}
    foreach ($line in $lines) {
        if ($line -match '^([A-Z0-9_]+)=(.*)$') { $values[$Matches[1]] = $Matches[2].Trim('"') }
    }
    return $values
}

function Read-MigrationHead {
    # The newest row of __EFMigrationsHistory, read with the Azure CLI
    # identity's token. Test-ReleaseValidation.ps1 supplies the value through
    # PEGASUS_TEST_MIGRATION_HEAD because a fake CLI cannot answer SQL; the
    # variable changes nothing else.
    param([Parameter(Mandatory)][hashtable] $Values)
    if (-not [string]::IsNullOrWhiteSpace($env:PEGASUS_TEST_MIGRATION_HEAD)) {
        Write-Host 'TEST: the migration head is supplied by PEGASUS_TEST_MIGRATION_HEAD.'
        return $env:PEGASUS_TEST_MIGRATION_HEAD.Trim()
    }
    foreach ($key in 'AZURE_SQL_SERVER_FQDN', 'AZURE_SQL_DATABASE_NAME') {
        if ([string]::IsNullOrWhiteSpace($Values[$key])) { throw "azd environment is missing $key." }
    }
    $token = (Invoke-Az -Failure 'Unable to obtain an Azure SQL access token.' -Arguments @(
        'account', 'get-access-token', '--resource', 'https://database.windows.net/', '--query', 'accessToken', '--output', 'tsv')).Trim()
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'Unable to obtain an Azure SQL access token.' }
    Add-Type -AssemblyName System.Data
    $connection = [System.Data.SqlClient.SqlConnection]::new()
    $connection.ConnectionString = "Server=tcp:$($Values['AZURE_SQL_SERVER_FQDN']),1433;Database=$($Values['AZURE_SQL_DATABASE_NAME']);Encrypt=True;Connect Timeout=60;"
    $connection.AccessToken = $token
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC'
        $head = $command.ExecuteScalar()
    }
    finally {
        $connection.Dispose()
    }
    if ($null -eq $head -or [string]::IsNullOrWhiteSpace([string]$head)) { throw 'The production database reports no migration head.' }
    return ([string]$head).Trim()
}

function Get-CandidateMigrationIdentity {
    # The newest migration source at the candidate, by the same rule as
    # Get-PegasusMigrationIdentity, read from the tree without a checkout.
    param([Parameter(Mandatory)][string] $Sha)
    $names = @(Invoke-Git -Arguments @('ls-tree', '--name-only', $Sha, "$migrationsDirectory/") |
        ForEach-Object { Split-Path -Leaf $_ } |
        Where-Object { $_ -match '^\d{14}_[A-Za-z0-9]+\.cs$' } |
        Sort-Object)
    if ($names.Count -eq 0) { throw "No migration source found under $migrationsDirectory at $Sha." }
    return [IO.Path]::GetFileNameWithoutExtension($names[-1])
}

function Get-AzdEnvironmentHash {
    param([Parameter(Mandatory)][string] $Root)
    $envFile = Join-Path $Root ".azure/$releaseEnvironment/.env"
    if (-not (Test-Path -LiteralPath $envFile -PathType Leaf)) { throw "The $releaseEnvironment azd environment is unavailable at $envFile." }
    return (Get-FileHash -LiteralPath $envFile -Algorithm SHA256).Hash
}

function Get-RetainedAzdEnvironmentHash {
    # The hash the previous release retained beside its artifacts; null when
    # no release has retained one yet.
    $retained = @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'artifacts/releases') -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^release-\d+-[0-9a-f]{8}$' } |
        Sort-Object { [int]($_.Name -split '-')[1] } -Descending)
    foreach ($folder in $retained) {
        $file = Join-Path $folder.FullName 'azd-env.sha256'
        if (Test-Path -LiteralPath $file -PathType Leaf) {
            return [pscustomobject]@{ Release = $folder.Name; Hash = (Get-Content -LiteralPath $file -Raw).Trim() }
        }
    }
    return $null
}

function Get-ReleaseRoot {
    param([Parameter(Mandatory)][string] $Sha)
    return Join-Path (Split-Path -Parent $RepositoryRoot) "pegasus-worktrees/release-$($Sha.Substring(0, 8))"
}

function Get-DeploymentId {
    # Best effort: the deployment record id the CLI reports, for the record.
    param([AllowEmptyString()][string] $Json)
    try {
        $record = $Json | ConvertFrom-Json
        foreach ($name in 'id', 'deploymentId', 'name') {
            $property = $record.PSObject.Properties[$name]
            if ($null -ne $property -and -not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
                return [string]$property.Value
            }
        }
    }
    catch { }
    return '(not reported)'
}

# --- Azure writes --------------------------------------------------------------

$script:record = [ordered]@{}

function Set-WorkerCensus {
    # Every member of the canonical Disabled census, to one value.
    param([Parameter(Mandatory)][ValidateSet('true', 'false')][string] $Value)
    $settings = @(Get-PegasusWorkerDisabledSettingNames | ForEach-Object { "$_=$Value" })
    Invoke-Az -Failure "Unable to set the Worker Disabled census to $Value." -Arguments (@(
        'functionapp', 'config', 'appsettings', 'set', '--subscription', $subscriptionId,
        '--resource-group', $resourceGroup, '--name', $workerApp, '--settings') + $settings + @('--output', 'none')) | Out-Null
}

function Invoke-WorkerSmoke {
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][ValidateSet('disabled', 'approved-live-worker')][string] $Activation,
        [string] $ClassifierState
    )
    $arguments = @('-WorkerOnly', '-ResourceGroupName', $resourceGroup, '-SubscriptionId', $subscriptionId,
        '-ExpectedWorkerActivation', $Activation)
    if ($ClassifierState) { $arguments += @('-ExpectedClassifierState', $ClassifierState) }
    Invoke-ReleaseScript -Root $Root -Name 'Invoke-ProductionSmoke.ps1' -Arguments $arguments `
        -Failure "Worker-only smoke failed (expected $Activation)."
}

function Invoke-FullSmoke {
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $Sha,
        [Parameter(Mandatory)][string] $WebPackageSha256,
        [Parameter(Mandatory)][bool] $ClassifierPresent
    )
    $arguments = @('-BaseUri', $webOrigin, '-ExpectedSourceRevision', $Sha, '-ExpectedVersion', $Version,
        '-ExpectedWebPackageSha256', $WebPackageSha256, '-ResourceGroupName', $resourceGroup,
        '-SubscriptionId', $subscriptionId, '-ExpectedWorkerActivation', 'approved-live-worker')
    if ($ClassifierPresent) { $arguments += @('-ExpectedClassifierState', 'Running') }
    Invoke-ReleaseScript -Root $Root -Name 'Invoke-ProductionSmoke.ps1' -Arguments $arguments -Failure 'Exact release smoke failed.'
    $script:record['Smoke'] = "passed at $([DateTimeOffset]::UtcNow.ToString('HH:mm:ss'))Z"
}

function Publish-Web {
    # az webapp deploy authenticates with the operator's Entra token; basic
    # publishing credentials are disabled by the template. --clean removes the
    # previous package contents. With -Restart:$false the site stays Stopped
    # and the client does not wait for a start that is not going to happen.
    param([Parameter(Mandatory)][string] $PackagePath, [Parameter(Mandatory)][bool] $Restart)
    $arguments = @('webapp', 'deploy', '--subscription', $subscriptionId, '--resource-group', $resourceGroup,
        '--name', $webApp, '--src-path', $PackagePath, '--type', 'zip', '--clean', 'true',
        '--restart', $Restart.ToString().ToLowerInvariant())
    if (-not $Restart) { $arguments += @('--track-status', 'false') }
    $json = Invoke-Az -Failure 'Web package deployment failed.' -Arguments ($arguments + @('--output', 'json'))
    $script:record['Web deployment'] = Get-DeploymentId $json
}

function Publish-FunctionApp {
    # Never azd deploy worker: it invokes a remote Oryx build against an
    # already-published package.
    param([Parameter(Mandatory)][string] $Name, [Parameter(Mandatory)][string] $PackagePath, [Parameter(Mandatory)][string] $Label)
    $json = Invoke-Az -Failure "$Label deployment failed." -Arguments @(
        'functionapp', 'deployment', 'source', 'config-zip', '--subscription', $subscriptionId,
        '--resource-group', $resourceGroup, '--name', $Name, '--src', $PackagePath, '--output', 'json')
    $script:record["$Label deployment"] = Get-DeploymentId $json
}

function Stop-FunctionApp {
    param([Parameter(Mandatory)][string] $Name)
    Invoke-Az -Failure "$Name stop failed." -Arguments @('functionapp', 'stop', '--subscription', $subscriptionId, '--resource-group', $resourceGroup, '--name', $Name, '--output', 'none') | Out-Null
    Wait-FunctionAppState -Name $Name -Expected 'Stopped'
    Write-Host "$Name read back Stopped."
}

function Start-FunctionApp {
    param([Parameter(Mandatory)][string] $Name)
    Invoke-Az -Failure "$Name start failed." -Arguments @('functionapp', 'start', '--subscription', $subscriptionId, '--resource-group', $resourceGroup, '--name', $Name, '--output', 'none') | Out-Null
    Wait-FunctionAppState -Name $Name -Expected 'Running'
    Write-Host "$Name read back Running."
}

function Assert-AzdEnvironment {
    # The environment names the fixed target and only its Key Vault; a stale
    # or foreign environment is refused before any write.
    param([Parameter(Mandatory)][hashtable] $Values)
    if ($Values['AZURE_SUBSCRIPTION_ID'] -ne $subscriptionId -or $Values['AZURE_TENANT_ID'] -ne $tenantId -or
        $Values['AZURE_RESOURCE_GROUP'] -ne $resourceGroup) {
        throw 'The azd environment names a subscription, tenant or resource group other than the fixed target.'
    }
    foreach ($key in @($Values.Keys | Where-Object { $_ -like '*_SECRET_URI' -or $_ -like '*_SECRET_URIS' })) {
        foreach ($uri in ([string]$Values[$key]).Split(',')) {
            if (-not $uri.Trim().StartsWith("https://$keyVault.vault.azure.net/", [StringComparison]::OrdinalIgnoreCase)) {
                throw "$key names a secret outside $keyVault."
            }
        }
    }
}

function Invoke-Provision {
    # Sets both activations, gates with PreProvision against the observed live
    # Worker, and provisions once. Provision changes settings, never the
    # served bytes.
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][ValidateSet('disabled', 'approved-live-worker')][string] $ExpectedLiveWorkerActivation
    )
    Push-Location $Root
    try {
        Invoke-Azd -Failure 'Unable to set Web activation.' -Arguments @('env', 'set', 'PEGASUS_WEB_ACTIVATION', 'approved', '-e', $releaseEnvironment) | Out-Null
        Invoke-Azd -Failure 'Unable to set Worker activation.' -Arguments @('env', 'set', 'PEGASUS_WORKER_ACTIVATION', 'approved-live-worker', '-e', $releaseEnvironment) | Out-Null
    }
    finally {
        Pop-Location
    }
    Invoke-ReleaseScript -Root $Root -Name 'Test-AzureDeploymentPlan.ps1' -Failure 'Pre-provision validation failed.' -Arguments @(
        '-Mode', 'PreProvision', '-Environment', $releaseEnvironment, '-ManifestPath', $script:manifestPath,
        '-WorkerActivation', 'approved-live-worker', '-ExpectedLiveWorkerActivation', $ExpectedLiveWorkerActivation)
    Push-Location $Root
    try {
        $started = [DateTimeOffset]::UtcNow
        Invoke-Azd -Failure 'azd provision failed.' -Arguments @('provision', '-e', $releaseEnvironment, '--no-prompt') | ForEach-Object { Write-Host $_ }
        $script:record['Provision'] = "azd provision succeeded in $([int]([DateTimeOffset]::UtcNow - $started).TotalSeconds) s"
    }
    finally {
        Pop-Location
    }
}

function Invoke-Migration {
    # The migration recipe: the manifest- and environment-bound gate, the
    # bundle from the validated manifest against the process-local Production
    # host environment, the runtime principals and permission census, then the
    # live head must equal the manifest's identity before anything starts.
    param([Parameter(Mandatory)][string] $Root, [Parameter(Mandatory)][object] $Manifest)
    Invoke-ReleaseScript -Root $Root -Name 'Test-AzureDeploymentPlan.ps1' -Failure 'Pre-migration validation failed.' -Arguments @(
        '-Mode', 'PreMigration', '-Environment', $releaseEnvironment, '-ManifestPath', $script:manifestPath,
        '-ManifestSha256', $ApprovedManifestSha256)
    $values = Get-AzdValues -Root $Root
    $required = @(
        'AZURE_SQL_SERVER_FQDN', 'AZURE_SQL_DATABASE_NAME', 'WEB_IDENTITY_CLIENT_ID',
        'TRANSPORT_STORAGE_ACCOUNT_NAME', 'CUSTODY_STORAGE_ACCOUNT_NAME',
        'AZURE_TENANT_ID', 'BOX_HOLDING_FOLDER_ID',
        'GLASS_MARKET_VALUE_ASSESSOR_BASE_URI', 'GLASS_ESTIMATOR_BASE_URI',
        'GLASS_REPAIR_PROFILE_ID', 'GITHUB_PROBLEM_REPORT_TOKEN_SECRET_URI',
        'GITHUB_PROBLEM_REPORT_REPOSITORY')
    $missing = @($required | Where-Object { [string]::IsNullOrWhiteSpace($values[$_]) })
    if ($missing.Count) { throw "azd environment is missing: $($missing -join ', '). GLASS_* base URIs fall back to main.parameters.json when unset; set them with azd env set first." }

    # The migration host validates its Production configuration but uses none
    # of the external routes: the non-azd values below are process-only,
    # shape-valid placeholders, not configuration or secrets.
    $placeholder = 'migration-host-placeholder'
    $migrationHost = @{
        ASPNETCORE_ENVIRONMENT = 'Production'
        Runtime__Profile = 'Production'
        AZURE_TOKEN_CREDENTIALS = 'AzureCliCredential'
        ConnectionStrings__Pegasus = "Server=tcp:$($values['AZURE_SQL_SERVER_FQDN']),1433;Database=$($values['AZURE_SQL_DATABASE_NAME']);Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
        AzureIdentity__WebClientId = $values['WEB_IDENTITY_CLIENT_ID']
        IntakeQueue__ServiceUri = "https://$($values['TRANSPORT_STORAGE_ACCOUNT_NAME']).queue.core.windows.net/"
        CustodyStorage__ServiceUri = "https://$($values['CUSTODY_STORAGE_ACCOUNT_NAME']).blob.core.windows.net/"
        Graph__BaseUri = 'https://graph.microsoft.com/v1.0/'
        Graph__TenantId = $values['AZURE_TENANT_ID']
        Graph__ChangeNotificationClientState = $placeholder
        Box__BaseUri = 'https://api.box.com/2.0/'
        Box__UploadUri = 'https://upload.box.com/api/2.0/'
        Box__RootFolderId = '405543781910'
        Box__HoldingFolderId = $values['BOX_HOLDING_FOLDER_ID']
        Box__ConfigJson = '{"boxAppSettings":{"clientID":"migration-host-placeholder","appAuth":{"publicKeyID":"migration-host-placeholder","privateKey":"migration-host-placeholder","passphrase":"migration-host-placeholder"}},"enterpriseID":"migration-host-placeholder"}'
        Box__ClientSecret = $placeholder
        Glass__MarketValueAssessorBaseUri = $values['GLASS_MARKET_VALUE_ASSESSOR_BASE_URI']
        Glass__EstimatorBaseUri = $values['GLASS_ESTIMATOR_BASE_URI']
        Glass__CallbackBaseUri = $webOrigin
        Glass__RepairProfileId = $values['GLASS_REPAIR_PROFILE_ID']
        Glass__ValuationAccount__Username = $placeholder
        Glass__ValuationAccount__Password = $placeholder
        Cazana__CompanionAccount__Email = $placeholder
        Cazana__CompanionAccount__Password = $placeholder
        Brego__ValuationAccount__Email = $placeholder
        Brego__ValuationAccount__Password = $placeholder
        GitHub__ProblemReports__Token = $placeholder
        GitHub__ProblemReports__Repository = $values['GITHUB_PROBLEM_REPORT_REPOSITORY']
    }
    foreach ($entry in $migrationHost.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    $bundlePath = Join-Path (Split-Path -Parent $script:manifestPath) $Manifest.migrationBundleName
    Push-Location (Join-Path $Root 'src/Pegasus.Web')
    try {
        & $bundlePath --connection $env:ConnectionStrings__Pegasus
        if ($LASTEXITCODE -ne 0) { throw 'Migration bundle failed. The Worker, the classifier and the Web App stay stopped; recover forward only.' }
    }
    finally {
        Pop-Location
    }
    Invoke-ReleaseScript -Root $Root -Name 'Invoke-AzureDatabaseBootstrap.ps1' -Failure 'Database bootstrap failed.' -Arguments @(
        '-Environment', $releaseEnvironment, '-ManifestPath', $script:manifestPath, '-ManifestSha256', $ApprovedManifestSha256)
    $head = Read-MigrationHead -Values $values
    if ($head -cne [string]$Manifest.migrationIdentity) {
        throw "The live migration head is $head, not the manifest's $($Manifest.migrationIdentity). Nothing starts."
    }
    $script:record['Migration'] = "head $head"
    Write-Host "Migration head read back $head."
}

# --- Promotion -----------------------------------------------------------------

function Invoke-Promotion {
    # The lease is an equality guard, not permission to rewrite history. A
    # failure is never retried with rebase, reset, an unleased push or another
    # SHA.
    param([Parameter(Mandatory)][string] $Sha)
    Invoke-Git -Arguments @('fetch', 'origin', '--prune')
    $mainSha = (Invoke-Git -Arguments @('rev-parse', 'origin/main'))[0].Trim()
    $devSha = (Invoke-Git -Arguments @('rev-parse', 'origin/dev'))[0].Trim()
    if ($mainSha -ceq $Sha) {
        if ($devSha -cne $Sha) { throw "origin/main is already at $Sha but origin/dev is at $devSha." }
        Write-Host "origin/main already reads $Sha; promotion was done."
        return
    }
    if ($devSha -cne $Sha) { throw "origin/dev is at $devSha, not the approved $Sha. Obtain a fresh approval for the current head." }
    & git -C $RepositoryRoot merge-base --is-ancestor $mainSha $Sha
    if ($LASTEXITCODE -ne 0) { throw 'origin/main is not an ancestor of the approved SHA.' }
    Invoke-Git -Failure 'Atomic promotion failed.' -Arguments @(
        'push', '--atomic', "--force-with-lease=refs/heads/dev:$Sha", 'origin', "${Sha}:refs/heads/main", "${Sha}:refs/heads/dev") | ForEach-Object { Write-Host $_ }
    Invoke-Git -Arguments @('fetch', 'origin', '--prune')
    $promotedMain = (Invoke-Git -Arguments @('rev-parse', 'origin/main'))[0].Trim()
    $promotedDev = (Invoke-Git -Arguments @('rev-parse', 'origin/dev'))[0].Trim()
    if ($promotedMain -cne $Sha -or $promotedDev -cne $Sha) { throw 'Remote read-back does not equal the approved release SHA.' }
    Write-Host "main and dev read back $Sha."
    $script:record['Source'] = "$Sha, promoted from $mainSha; main and dev read it back"
}

# --- Default run: preflight, build, approval request -------------------------------

function Invoke-Preflight {
    Write-Host '=== Preflight (read-only) ==='
    Invoke-Git -Arguments @('fetch', 'origin', '--prune')
    $mainSha = (Invoke-Git -Arguments @('rev-parse', 'origin/main'))[0].Trim()
    $devSha = (Invoke-Git -Arguments @('rev-parse', 'origin/dev'))[0].Trim()
    & git -C $RepositoryRoot merge-base --is-ancestor $mainSha $devSha
    if ($LASTEXITCODE -ne 0) { throw 'origin/main is not an ancestor of origin/dev.' }
    Write-Host "origin/main $mainSha"
    Write-Host "origin/dev  $devSha"
    if ($mainSha -ceq $devSha) {
        Write-Host 'main already equals dev: nothing to release.'
        return $null
    }
    Write-Host ''
    Invoke-Git -Arguments @('log', '--oneline', '--decorate', "$mainSha..$devSha") | ForEach-Object { Write-Host $_ }
    Write-Host ''
    Invoke-Git -Arguments @('diff', '--stat', "$mainSha..$devSha") | ForEach-Object { Write-Host $_ }

    # Every merged pull request in the range and its checks, read from GitHub:
    # the dev to main run only starts at promotion, so this is the evidence
    # available before it.
    Write-Host ''
    Write-Host '--- Pull requests in the range ---'
    $pullNumbers = @(Invoke-Git -Arguments @('log', '--merges', '--format=%s', "$mainSha..$devSha") |
        ForEach-Object { if ($_ -match 'Merge pull request #(\d+)') { [int]$Matches[1] } } | Sort-Object -Unique)
    $notGreen = 0
    foreach ($number in $pullNumbers) {
        $json = (& gh pr view $number --repo $gitHubRepository --json 'number,title,state,statusCheckRollup' 2>&1) -join "`n"
        if ($LASTEXITCODE -ne 0) { Write-Host "#$number : gh could not read it ($json)"; $notGreen++; continue }
        $pull = $json | ConvertFrom-Json
        $checks = @($pull.statusCheckRollup)
        # A check run carries a conclusion; a commit status carries a state.
        $failing = @($checks | Where-Object {
            $verdict = ''
            foreach ($name in 'conclusion', 'state') {
                $property = $_.PSObject.Properties[$name]
                if ($null -ne $property -and -not [string]::IsNullOrWhiteSpace([string]$property.Value)) { $verdict = [string]$property.Value; break }
            }
            $verdict -notin @('SUCCESS', 'SKIPPED', 'NEUTRAL')
        })
        if ($failing.Count -gt 0) { $notGreen++ }
        Write-Host ("#{0} {1}: {2} checks, {3} not successful" -f $pull.number, $pull.title, $checks.Count, $failing.Count)
    }
    if ($notGreen -gt 0) { Write-Host "$notGreen pull request(s) are not green: the approval must waive each one by number or the candidate waits." }

    # The candidate's kind: deployable files decide the route; infra files and
    # azd-environment drift decide whether provisioning is needed.
    $files = @(Invoke-Git -Arguments @('diff', '--name-only', "$mainSha..$devSha"))
    $deployable = @($files | Where-Object { $_ -match $deployablePattern })
    $provisionFiles = @($files | Where-Object { $_ -match $provisionPattern })
    $migrationFiles = @($files | Where-Object { $_ -like "$migrationsDirectory/*" -and $_ -match '/\d{14}_[A-Za-z0-9]+\.cs$' })

    Write-Host ''
    Write-Host '--- Deployed state ---'
    $account = Invoke-AzJson -Failure 'az account show failed.' -Arguments @('account', 'show', '--query', '{subscription:id,tenant:tenantId,user:user.name}')
    if ([string]$account.subscription -ne $subscriptionId -or [string]$account.tenant -ne $tenantId) {
        throw "The Azure CLI account is $($account.subscription)/$($account.tenant), not the fixed target."
    }
    Write-Host "Azure CLI: $($account.user) in the fixed subscription."
    $site = Get-WebSite
    $servingVersion = Get-WebVersion
    $servingSha = if ($null -ne $servingVersion) { [string]$servingVersion.sourceSha } else { '(not answering)' }
    Write-Host "Web App: $($site.state) on $($site.stack) at $($site.host); serving $servingSha / $(if ($null -ne $servingVersion) { $servingVersion.version } else { '?' })"
    Write-Host "Worker: $(Get-FunctionAppState $workerApp)"
    $classifierPresent = Test-ClassifierPresent
    Write-Host "Classifier: $(if ($classifierPresent) { Get-FunctionAppState $classifierApp } else { 'absent' })"
    $schedule = Invoke-Az -Failure 'Unable to read the Worker settings.' -Arguments @(
        'functionapp', 'config', 'appsettings', 'list', '--subscription', $subscriptionId, '--resource-group', $resourceGroup,
        '--name', $workerApp, '--query', "[?contains(name,'Schedule') || starts_with(name,'AzureWebJobs.')].{name:name,value:value}", '--output', 'tsv')
    Write-Host "Worker schedule and Disabled settings:`n$schedule"

    $values = Get-AzdValues -Root $RepositoryRoot
    $head = Read-MigrationHead -Values $values
    $candidateIdentity = Get-CandidateMigrationIdentity -Sha $devSha
    Write-Host "Migration head: $head; candidate identity: $candidateIdentity"

    Write-Host ''
    Write-Host '--- Route ---'
    $route = 'promotion-only'
    if ($deployable.Count -gt 0) {
        $route = if ($head -ceq $candidateIdentity) { 'normal (migration identity unchanged)' }
            elseif ($migrationFiles.Count -eq 0) { "normal (the head $head differs from the candidate identity but the range adds no migration: resolve before approving)" }
            else { "normal after an additive migration, or destructive: read the migration source(s) and classify`n  " + ($migrationFiles -join "`n  ") }
    }
    Write-Host "Route: $route"
    if ($deployable.Count -eq 0) { Write-Host 'No deployable path changed; promote the exact SHA and stop.' }
    else { Write-Host "Deployable paths: $($deployable.Count)" }

    $envHash = Get-AzdEnvironmentHash -Root $RepositoryRoot
    $retained = Get-RetainedAzdEnvironmentHash
    $provisionNeeded = $provisionFiles.Count -gt 0 -or $null -eq $retained -or $retained.Hash -cne $envHash
    $reason = if ($provisionFiles.Count -gt 0) { "the range touches $($provisionFiles -join ', ')" }
        elseif ($null -eq $retained) { 'no retained release holds an azd-env.sha256 to compare with' }
        elseif ($retained.Hash -cne $envHash) { "the azd environment changed since $($retained.Release)" }
        else { "no infra path changed and the azd environment equals $($retained.Release)'s" }
    Write-Host "Provision: $(if ($provisionNeeded) { 'needed' } else { 'not needed' }) ($reason)"

    return [pscustomobject]@{
        MainSha = $mainSha; DevSha = $devSha; Route = $route; PromotionOnly = ($deployable.Count -eq 0)
        ProvisionNeeded = $provisionNeeded; ServingSha = $servingSha; Head = $head; CandidateIdentity = $candidateIdentity
        ClassifierPresent = $classifierPresent; AzdEnvironmentHash = $envHash
    }
}

function New-ReleaseWorktree {
    # A disposable detached worktree outside the caller's checkout, with the
    # ignored azd environment copied in. An existing worktree at the SHA is
    # reused when it is clean.
    param([Parameter(Mandatory)][string] $Sha)
    $root = Get-ReleaseRoot -Sha $Sha
    $environmentSource = Join-Path $RepositoryRoot ".azure/$releaseEnvironment"
    if (-not (Test-Path -LiteralPath $environmentSource)) { throw "The $releaseEnvironment azd environment is unavailable." }
    if (-not (Test-Path -LiteralPath $root)) {
        Invoke-Git -Failure 'Unable to add the release worktree.' -Arguments @('worktree', 'add', '--detach', $root, $Sha) | Out-Null
    }
    $head = (& git -C $root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -cne $Sha) { throw "The release worktree at $root is not at $Sha." }
    New-Item -ItemType Directory -Force -Path (Join-Path $root '.azure') | Out-Null
    Copy-Item -Recurse -Force -LiteralPath $environmentSource -Destination (Join-Path $root ".azure/$releaseEnvironment")
    if (@(& git -C $root status --porcelain).Count -ne 0) { throw "The release worktree at $root is not clean." }
    return $root
}

function Invoke-Build {
    param([Parameter(Mandatory)][string] $Sha, [Parameter(Mandatory)][string] $AzdEnvironmentHash)
    Write-Host ''
    Write-Host '=== Build (writes only the worktree) ==='
    $root = New-ReleaseWorktree -Sha $Sha
    Invoke-ReleaseScript -Root $root -Name 'Build-ReleaseArtifacts.ps1' -Failure 'Release build failed.' -Arguments @('-Version', $Version, '-SourceRevision', $Sha)
    $manifestPath = Join-Path $root "artifacts/releases/$Version/release-manifest.json"
    Invoke-ReleaseScript -Root $root -Name 'Test-AzureDeploymentPlan.ps1' -Failure 'Artifact validation failed.' -Arguments @('-Mode', 'Artifact', '-ManifestPath', $manifestPath)
    Set-Content -LiteralPath (Join-Path (Split-Path -Parent $manifestPath) 'azd-env.sha256') -Value $AzdEnvironmentHash -Encoding ascii
    $manifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -Depth 10
    return [pscustomobject]@{
        Root = $root
        ManifestPath = $manifestPath
        ManifestSha256 = $manifestSha256
        MigrationIdentity = [string]$manifest.migrationIdentity
        Artifacts = @($manifest.artifacts | ForEach-Object { "$($_.name) $($_.sha256)" })
    }
}

function Write-ApprovalRequest {
    param([Parameter(Mandatory)][object] $Preflight, [object] $Build)
    Write-Host ''
    Write-Host '=== Approval request ==='
    if ($Preflight.PromotionOnly) {
        Write-Host "Promotion only: approve the push of $($Preflight.DevSha) to main and dev. No Azure write."
        Write-Host "Then: pwsh ./scripts/Invoke-Release.ps1 -PromoteOnly -ApprovedReleaseSha $($Preflight.DevSha)"
        return
    }
    if ($null -eq $Build) {
        Write-Host "Preflight only: the route is '$($Preflight.Route)', provision $(if ($Preflight.ProvisionNeeded) { 'needed' } else { 'not needed' }). Run without -PreflightOnly to build and print the request."
        return
    }
    Write-Host 'One message from the operator names all of these; it grants the promotion push and the Azure writes for exactly them:'
    Write-Host "  Release SHA        $($Preflight.DevSha) (from main $($Preflight.MainSha))"
    Write-Host "  Manifest SHA-256   $($Build.ManifestSha256)"
    $Build.Artifacts | ForEach-Object { Write-Host "  Artifact           $_" }
    Write-Host "  Migration          head $($Preflight.Head) -> $($Build.MigrationIdentity)"
    Write-Host "  Route              $($Preflight.Route)"
    Write-Host "  Provision          $(if ($Preflight.ProvisionNeeded) { 'yes' } else { 'no' })"
    Write-Host "  Targets            $webApp, $workerApp$(if ($Preflight.ClassifierPresent) { ", $classifierApp" }), SQL pegasus-prod-sql-252ow37gij/pegasus"
    Write-Host "  Old Web SHA        $($Preflight.ServingSha) (destructive route only; the approval names it and a window)"
    Write-Host ''
    $provisionArgument = if ($Preflight.ProvisionNeeded) { ' -Provision' } else { '' }
    Write-Host "Normal route:      pwsh ./scripts/Invoke-Release.ps1 -Execute -ReleaseNumber <n> -Route Normal -ApprovedReleaseSha $($Preflight.DevSha) -ApprovedManifestSha256 $($Build.ManifestSha256)$provisionArgument"
    Write-Host "Destructive route: pwsh ./scripts/Invoke-Release.ps1 -Execute -ReleaseNumber <n> -Route Destructive -ApprovedReleaseSha $($Preflight.DevSha) -ApprovedManifestSha256 $($Build.ManifestSha256) -ApprovedOldWebSourceSha $($Preflight.ServingSha)"
}

# --- Approved run ----------------------------------------------------------------

function Invoke-NormalRoute {
    param([Parameter(Mandatory)][string] $Root, [Parameter(Mandatory)][object] $Manifest, [Parameter(Mandatory)][string] $WebPackagePath, [Parameter(Mandatory)][string] $WebPackageSha256)
    $classifierPresent = Test-ClassifierPresent
    if ($WarmupSettingsChange) {
        # The previous package cannot answer a new warm-up path or status, so
        # the platform would keep failing its start after provision: deploy
        # first, read the exact release back, then provision and read it back
        # again.
        if (-not $Provision) { throw '-WarmupSettingsChange needs -Provision: the warm-up setting is provisioned.' }
        Publish-Web -PackagePath $WebPackagePath -Restart $true
        Wait-PegasusExpectedWebSite -ExpectedSourceRevision $ApprovedReleaseSha -ExpectedVersion $Version
        Invoke-Provision -Root $Root -ExpectedLiveWorkerActivation (Get-LiveWorkerActivation)
        Wait-PegasusExpectedWebSite -ExpectedSourceRevision $ApprovedReleaseSha -ExpectedVersion $Version
    }
    else {
        if ($Provision) {
            # Provision may restart the site onto the previous package; on this
            # route that package runs against a compatible schema.
            Invoke-Provision -Root $Root -ExpectedLiveWorkerActivation (Get-LiveWorkerActivation)
        }
        else {
            $script:record['Provision'] = 'not run (no infra change, azd environment unchanged)'
        }
        Publish-Web -PackagePath $WebPackagePath -Restart $true
        Wait-PegasusExpectedWebSite -ExpectedSourceRevision $ApprovedReleaseSha -ExpectedVersion $Version
    }
    Publish-FunctionApp -Name $workerApp -PackagePath (Join-Path $Root "artifacts/releases/$Version/worker.zip") -Label 'Worker'
    if ($classifierPresent) {
        Publish-FunctionApp -Name $classifierApp -PackagePath (Join-Path $Root "artifacts/releases/$Version/classifier.zip") -Label 'Classifier'
    }
    Invoke-FullSmoke -Root $Root -Sha $ApprovedReleaseSha -WebPackageSha256 $WebPackageSha256 -ClassifierPresent $classifierPresent
}

function Invoke-DestructiveRoute {
    # ADR-0046: the classifier, the Worker and the old Web are stopped and
    # read back before SQL; after SQL, recovery is forward only. A run that
    # stopped part way is resumed from where the estate is, never from the
    # beginning.
    param([Parameter(Mandatory)][string] $Root, [Parameter(Mandatory)][object] $Manifest, [Parameter(Mandatory)][string] $WebPackagePath, [Parameter(Mandatory)][string] $WebPackageSha256)
    if ([string]::IsNullOrWhiteSpace($ApprovedOldWebSourceSha)) { throw 'The destructive route needs -ApprovedOldWebSourceSha, the exact old Web source SHA the approval named.' }
    $identity = [string]$Manifest.migrationIdentity
    $classifierPresent = Test-ClassifierPresent
    $workerPackagePath = Join-Path $Root "artifacts/releases/$Version/worker.zip"
    $site = Get-WebSite
    if ([string]$site.stack -cne $webStack) { throw "Destructive migration requires the Web App on $webStack; it reports '$($site.stack)'." }
    if ("https://$($site.host)/" -cne $webOrigin) { throw 'Web App hostname differs from the fixed target.' }

    # Where the estate is: a fresh run finds the old release serving; a resumed
    # run finds the site Stopped (before or after SQL) or already serving the
    # new release.
    $stage = 'contain'
    switch ([string]$site.state) {
        'Running' {
            $serving = Get-WebVersion
            $servingSha = if ($null -ne $serving) { [string]$serving.sourceSha } else { '' }
            if ($servingSha -ceq $ApprovedReleaseSha) { $stage = 'activated' }
            elseif ($servingSha -cne $ApprovedOldWebSourceSha) { throw "The Web App reports source SHA '$servingSha', neither the approved old SHA nor the release." }
        }
        'Stopped' {
            $values = Get-AzdValues -Root $Root
            $head = Read-MigrationHead -Values $values
            $stage = if ($head -ceq $identity) { 'deploy-web' } else { 'migrate' }
            Write-Host "Resuming: the Web App is Stopped and the migration head is $head; continuing from '$stage'. Nothing is contained again."
        }
        default { throw "The Web App reports state '$($site.state)'." }
    }

    if ($stage -eq 'contain') {
        Set-WorkerCensus -Value 'true'
        # Proves every function is disabled before the new package is staged
        # (ADR-0046: staged with all functions disabled, not activated).
        Invoke-WorkerSmoke -Root $Root -Activation 'disabled'
        Publish-FunctionApp -Name $workerApp -PackagePath $workerPackagePath -Label 'Worker'
        # The classifier is a client of the Web App: it stops first.
        if ($classifierPresent) { Stop-FunctionApp -Name $classifierApp }
        Stop-FunctionApp -Name $workerApp
        Invoke-Az -Failure "Unable to stop $webApp." -Arguments @('webapp', 'stop', '--subscription', $subscriptionId, '--resource-group', $resourceGroup, '--name', $webApp, '--output', 'none') | Out-Null
        Wait-WebStopped
        Write-Host 'Web App read back Stopped and unserved.'
        $script:record['Containment'] = "classifier$(if (-not $classifierPresent) { ' (absent)' }), Worker and Web stopped and read back at $([DateTimeOffset]::UtcNow.ToString('HH:mm:ss'))Z"
        $stage = 'migrate'
    }

    if ($stage -eq 'migrate') {
        # One fresh read-back immediately before the gate, not the earlier
        # polling result.
        if ((Get-FunctionAppState $workerApp) -cne 'Stopped') { throw 'Worker is not freshly confirmed Stopped before destructive SQL.' }
        if ([string](Get-WebSite).state -cne 'Stopped') { throw 'Web App is not freshly confirmed Stopped before destructive SQL.' }
        if (Test-WebAnswers 'health/live') { throw 'Web App is still serving the application before destructive SQL.' }
        Invoke-WorkerSmoke -Root $Root -Activation 'disabled' -ClassifierState $(if ($classifierPresent) { 'Stopped' } else { '' })
        Invoke-Migration -Root $Root -Manifest $Manifest
        $stage = 'deploy-web'
    }

    if ($stage -eq 'deploy-web') {
        # The approved web.zip goes onto the stopped site so that no start,
        # however caused, can run the old package against the new schema; the
        # deployment endpoint stays available while the site is stopped.
        if ([string](Get-WebSite).state -cne 'Stopped') { throw 'The Web App must read back Stopped before the new package is deployed.' }
        Publish-Web -PackagePath $WebPackagePath -Restart $false
        if ([string](Get-WebSite).state -cne 'Stopped') { throw 'The Web App did not remain Stopped after the package deployment.' }
        $values = Get-AzdValues -Root $Root
        Assert-AzdEnvironment -Values $values
        # One provision with both activations approved (operator, 30 October
        # 2026): the new Worker bytes were staged before SQL, so enabling the
        # census here starts nothing but the approved release. A resumed run
        # may find the census already enabled by the run before it.
        Invoke-Provision -Root $Root -ExpectedLiveWorkerActivation (Get-LiveWorkerActivation)
        Invoke-Az -Failure 'Web App start failed.' -Arguments @('webapp', 'start', '--subscription', $subscriptionId, '--resource-group', $resourceGroup, '--name', $webApp, '--output', 'none') | Out-Null
        Wait-PegasusExpectedWebSite -ExpectedSourceRevision $ApprovedReleaseSha -ExpectedVersion $Version
        $stage = 'activated'
    }

    # Completion: Web Running at the release, Worker Running, classifier
    # Running when it exists, every Disabled value false, full smoke.
    $workerState = Get-FunctionAppState $workerApp
    if ($workerState -ceq 'Stopped') { Start-FunctionApp -Name $workerApp }
    elseif ($workerState -cne 'Running') { throw "Worker state after activation is neither Running nor Stopped: '$workerState'." }
    else { Wait-FunctionAppState -Name $workerApp -Expected 'Running' }
    if ($classifierPresent) {
        if ((Get-FunctionAppState $classifierApp) -cne 'Running') { Start-FunctionApp -Name $classifierApp }
        Publish-FunctionApp -Name $classifierApp -PackagePath (Join-Path $Root "artifacts/releases/$Version/classifier.zip") -Label 'Classifier'
    }
    Invoke-FullSmoke -Root $Root -Sha $ApprovedReleaseSha -WebPackageSha256 $WebPackageSha256 -ClassifierPresent $classifierPresent
}

function Complete-Release {
    # Values set during the release survive the worktree; the exact artifacts
    # are retained beside the previous releases'; the worktree goes.
    param([Parameter(Mandatory)][string] $Root)
    $environmentSource = Join-Path $Root ".azure/$releaseEnvironment"
    $environmentTarget = Join-Path $RepositoryRoot ".azure/$releaseEnvironment"
    foreach ($name in '.env', 'config.json') {
        $file = Join-Path $environmentSource $name
        if (Test-Path -LiteralPath $file -PathType Leaf) { Copy-Item -LiteralPath $file -Destination (Join-Path $environmentTarget $name) -Force }
    }
    $retained = Join-Path $RepositoryRoot "artifacts/releases/release-$ReleaseNumber-$($ApprovedReleaseSha.Substring(0, 8))"
    New-Item -ItemType Directory -Force -Path $retained | Out-Null
    Copy-Item -Recurse -Force -Path (Join-Path $Root "artifacts/releases/$Version/*") -Destination $retained
    Invoke-Git -Failure 'Unable to remove the release worktree; remove it by hand.' -Arguments @('worktree', 'remove', '--force', $Root) | Out-Null
    $script:record['Evidence'] = "artifacts/releases/release-$ReleaseNumber-$($ApprovedReleaseSha.Substring(0, 8))"
}

function Write-Record {
    Write-Host ''
    Write-Host "=== Record for docs/operations.md (at most 15 lines; owed items become issues) ==="
    Write-Host "## Release $ReleaseNumber - $([DateTimeOffset]::UtcNow.ToString('d MMMM yyyy')) (deployment live)"
    Write-Host ''
    Write-Host "Release $ReleaseNumber deployed <what>. $Route route; <migration or 'no migration'>; <outage or 'no outage'>."
    Write-Host ''
    Write-Host '| Observation | Value |'
    Write-Host '| --- | --- |'
    foreach ($entry in $script:record.GetEnumerator()) { Write-Host "| $($entry.Key) | $($entry.Value) |" }
    Write-Host '| Owed | <issues, or none> |'
}

# --- Entry -------------------------------------------------------------------------

switch ($PSCmdlet.ParameterSetName) {
    'Prepare' {
        $preflight = Invoke-Preflight
        if ($null -eq $preflight) { return }
        if ($PreflightOnly -or $preflight.PromotionOnly) {
            Write-ApprovalRequest -Preflight $preflight
            return
        }
        $build = Invoke-Build -Sha $preflight.DevSha -AzdEnvironmentHash $preflight.AzdEnvironmentHash
        Write-ApprovalRequest -Preflight $preflight -Build $build
    }
    'PromoteOnly' {
        Invoke-Promotion -Sha $ApprovedReleaseSha
        Write-Host 'Promotion only: no build, no Azure write. Record the promotion in docs/operations.md in one line.'
    }
    'Execute' {
        $account = Invoke-AzJson -Failure 'az account show failed.' -Arguments @('account', 'show', '--query', '{subscription:id,tenant:tenantId}')
        if ([string]$account.subscription -ne $subscriptionId -or [string]$account.tenant -ne $tenantId) {
            throw "The Azure CLI account is $($account.subscription)/$($account.tenant), not the fixed target."
        }
        $root = Get-ReleaseRoot -Sha $ApprovedReleaseSha
        if (-not (Test-Path -LiteralPath $root)) { throw "No release worktree at $root: run the default (read-only) run first." }
        $head = (& git -C $root rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or $head -cne $ApprovedReleaseSha) { throw "The release worktree at $root is not at the approved SHA." }
        if (@(& git -C $root status --porcelain).Count -ne 0) { throw "The release worktree at $root is not clean." }
        $script:manifestPath = Join-Path $root "artifacts/releases/$Version/release-manifest.json"
        if (-not (Test-Path -LiteralPath $script:manifestPath -PathType Leaf)) { throw "No manifest at $script:manifestPath: run the default run first." }
        $actualManifestSha256 = (Get-FileHash -LiteralPath $script:manifestPath -Algorithm SHA256).Hash
        if (-not $actualManifestSha256.Equals($ApprovedManifestSha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The built manifest does not match the approved SHA-256.'
        }
        $manifest = Get-Content -Raw -LiteralPath $script:manifestPath | ConvertFrom-Json -Depth 10
        if ([string]$manifest.sourceRevision -cne $ApprovedReleaseSha) { throw 'The manifest sourceRevision is not the approved SHA.' }
        Invoke-ReleaseScript -Root $root -Name 'Test-AzureDeploymentPlan.ps1' -Failure 'Pre-deploy validation failed.' -Arguments @(
            '-Mode', 'PreDeploy', '-ManifestPath', $script:manifestPath, '-ManifestSha256', $ApprovedManifestSha256)
        $webPackagePath = Join-Path $root "artifacts/releases/$Version/web.zip"
        $webPackageSha256 = [string]@($manifest.artifacts | Where-Object name -eq 'web.zip')[0].sha256
        $script:record['Packages'] = "manifest $($actualManifestSha256.ToUpperInvariant()); " + (@($manifest.artifacts | ForEach-Object { "$($_.name) $($_.sha256)" }) -join '; ')

        if ($Route -eq 'Destructive') {
            # The route's preconditions are read before the promotion push, so
            # a refusal writes nothing anywhere.
            if ([string]::IsNullOrWhiteSpace($ApprovedOldWebSourceSha)) { throw 'The destructive route needs -ApprovedOldWebSourceSha, the exact old Web source SHA the approval named.' }
            $site = Get-WebSite
            if ([string]$site.state -ceq 'Running') {
                $serving = Get-WebVersion
                $servingSha = if ($null -ne $serving) { [string]$serving.sourceSha } else { '' }
                if ($servingSha -cne $ApprovedReleaseSha -and $servingSha -cne $ApprovedOldWebSourceSha) {
                    throw "The Web App reports source SHA '$servingSha', neither the approved old SHA nor the release."
                }
            }
        }
        Invoke-Promotion -Sha $ApprovedReleaseSha
        if ($Route -eq 'Normal') {
            Invoke-NormalRoute -Root $root -Manifest $manifest -WebPackagePath $webPackagePath -WebPackageSha256 $webPackageSha256
        }
        else {
            Invoke-DestructiveRoute -Root $root -Manifest $manifest -WebPackagePath $webPackagePath -WebPackageSha256 $webPackageSha256
        }
        Complete-Release -Root $root
        Write-Record
    }
}
