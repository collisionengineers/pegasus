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
the exact SHA, the manifest SHA-256, the version, the route, whether to
provision and, for a destructive route, the old serving Web source SHA. It
reads the hosted test instance to prove it is serving the approved SHA, runs
the one route to a passing full smoke, then promotes main to the SHA the
estate now serves and tags it v<version> in one leased push (R680, R686),
copies the azd environment back to the primary checkout, retains the
artifacts and prints the record skeleton for docs/operations.md.

Releases are Semantic Versions from 1.0.0 (R684). The live version is the
highest v* tag on main; the read-only run proposes the next one from the
range (R685) and builds it unless -Version says otherwise; the approved run
refuses a version that is not one step above the live one, or not the one
the manifest was built with.

A destructive run that failed after containment is resumed with the same
-Execute line: the script reads where it is (Web Stopped and the migration
head) and continues forward. It never contains twice and never starts old
bytes after SQL began (ADR-0046).

.EXAMPLE
pwsh ./scripts/Invoke-Release.ps1

.EXAMPLE
pwsh ./scripts/Invoke-Release.ps1 -Execute -Version 1.1.0 -Route Normal `
  -ApprovedReleaseSha <sha> -ApprovedManifestSha256 <hash> -Provision

.EXAMPLE
pwsh ./scripts/Invoke-Release.ps1 -Execute -Version 1.1.0 -Route Destructive `
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

    # The Semantic Version the release carries (R684). The read-only run
    # proposes one from the live tag and the range (R685) and builds it unless
    # given here; the approved run names the one the approval granted, one
    # step above the live version and the one the manifest was built with.
    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(Mandatory, ParameterSetName = 'Execute')]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version,

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
# The hosted test instance (ADR-0088) runs dev; a deployable candidate is the
# SHA it is serving, read the way the Web App is (R682). The origin is the
# platform script's, the one the instance's own lifecycle refuses to leave.
$testInstanceOrigin = Get-PegasusTestInstanceOrigin

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
    param([string] $Origin = $webOrigin)
    $json = (& az rest --method get --url ([uri]::new([uri]$Origin, 'diagnostics/version').AbsoluteUri) --skip-authorization-header --output json 2>$null) -join "`n"
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) { return $null }
    try { return $json | ConvertFrom-Json }
    catch { return $null }
}

function Assert-TestInstanceServes {
    # "Vetted on the test server" is the instance serving the candidate SHA
    # (R682): read over the tailnet, never assumed. The operator's approval
    # message stays the acceptance; this proves it saw the same bytes.
    param([Parameter(Mandatory)][string] $Sha)
    $version = Get-WebVersion -Origin $testInstanceOrigin
    $serving = if ($null -ne $version) { [string]$version.sourceSha } else { '(not answering)' }
    if ($serving -cne $Sha) {
        throw "The test instance at $testInstanceOrigin is serving $serving, not the candidate ${Sha}: move it to dev's tip (pegasus-test-instance skill, section 6) and run again."
    }
    Write-Host "Test instance: serving $Sha at $testInstanceOrigin"
}

# --- Version ---------------------------------------------------------------------

function Get-LiveVersion {
    # Releases are Semantic Versions (R684) and main carries each release's
    # tag (R686), so the live version is the highest v<MAJOR.MINOR.PATCH> tag
    # merged into origin/main. Null until the first one.
    $versions = @(Invoke-Git -Arguments @('tag', '--list', 'v*', '--merged', 'origin/main') |
        ForEach-Object { if ($_ -match '^v(\d+\.\d+\.\d+)$') { [version]$Matches[1] } })
    if ($versions.Count -eq 0) { return $null }
    return @($versions | Sort-Object -Descending)[0]
}

function Get-NextVersions {
    param([Parameter(Mandatory)][version] $Live)
    return [ordered]@{
        PATCH = [version]::new($Live.Major, $Live.Minor, $Live.Build + 1)
        MINOR = [version]::new($Live.Major, $Live.Minor + 1, 0)
        MAJOR = [version]::new($Live.Major + 1, 0, 0)
    }
}

function Assert-VersionStep {
    # R684, R685: the first Semantic Version is 1.0.0; after it a release is
    # exactly one step above the live version. Returns the step.
    param([version] $Live, [Parameter(Mandatory)][string] $Candidate)
    if ($null -eq $Live) {
        if ($Candidate -ne '1.0.0') { throw "No v* tag on main: the first Semantic Version is 1.0.0, not $Candidate (R684)." }
        return 'first'
    }
    $next = Get-NextVersions -Live $Live
    foreach ($step in $next.Keys) { if ($next[$step] -eq [version]$Candidate) { return $step } }
    throw "$Candidate is not one step above the live v${Live}: the next versions are $($next.PATCH) (PATCH), $($next.MINOR) (MINOR) or $($next.MAJOR) (MAJOR)."
}

function Get-ProposedVersion {
    # R685: PATCH when every pull request in the range closes only type:bug
    # issues, MAJOR when the route is destructive, MINOR otherwise. The route
    # is classified after the preflight, so a range that adds a migration is
    # proposed both ways. A proposal: the approval names the version.
    param([version] $Live, [Parameter(Mandatory)][bool] $BugFixesOnly, [Parameter(Mandatory)][string] $BugFixesReason, [Parameter(Mandatory)][bool] $AddsMigration)
    if ($null -eq $Live) {
        return [pscustomobject]@{ Version = '1.0.0'; Step = 'first'; Reason = 'no v* tag on main, so the first Semantic Version is 1.0.0 (R684)'; Alternative = '' }
    }
    $next = Get-NextVersions -Live $Live
    $step = if ($BugFixesOnly) { 'PATCH' } else { 'MINOR' }
    $alternative = if ($AddsMigration) { "$($next.MAJOR) (MAJOR) if the migration is non-additive and the route destructive" } else { '' }
    return [pscustomobject]@{ Version = [string]$next[$step]; Step = $step; Reason = $BugFixesReason; Alternative = $alternative }
}

function Get-RemoteTagSha {
    # The commit a remote annotated tag points at; empty when it is absent.
    param([Parameter(Mandatory)][string] $Name)
    $lines = @(Invoke-Git -Arguments @('ls-remote', '--tags', 'origin', "refs/tags/$Name^{}") | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($lines.Count -eq 0) { return '' }
    return ($lines[0] -split '\s+')[0]
}

function New-ReleaseTag {
    # The release's annotated tag in the primary checkout (R686); an existing
    # local tag must already point at the released SHA.
    param([Parameter(Mandatory)][string] $Sha)
    $name = "v$Version"
    $existing = (& git -C $RepositoryRoot rev-parse --verify --quiet "refs/tags/$name^{commit}" 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -eq 0) {
        if ($existing -cne $Sha) { throw "A local tag $name already points at $existing, not the released $Sha." }
        return
    }
    Invoke-Git -Failure "Unable to create the tag $name." -Arguments @('tag', '-a', '-m', "Release $Version", $name, $Sha) | Out-Null
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
    # Same access-token pattern as Invoke-AzureDatabaseBootstrap.ps1.
    if (-not (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue)) {
        throw 'The SqlServer PowerShell module is required to read the migration head.'
    }
    $token = (Invoke-Az -Failure 'Unable to obtain an Azure SQL access token.' -Arguments @(
        'account', 'get-access-token', '--resource', 'https://database.windows.net/', '--query', 'accessToken', '--output', 'tsv')).Trim()
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'Unable to obtain an Azure SQL access token.' }
    $row = Invoke-Sqlcmd `
        -ServerInstance "tcp:$($Values['AZURE_SQL_SERVER_FQDN']),1433" `
        -Database $Values['AZURE_SQL_DATABASE_NAME'] `
        -AccessToken $token `
        -AbortOnError `
        -ErrorAction Stop `
        -Query 'SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC'
    $head = if ($null -ne $row) { [string]$row.MigrationId } else { '' }
    if ([string]::IsNullOrWhiteSpace($head)) { throw 'The production database reports no migration head.' }
    return $head.Trim()
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
    # no release has retained one yet. Folders are release-<version>-<sha8>
    # from 1.0.0 (R684) and release-<n>-<sha8> before it; the newest Semantic
    # Version comes first, then the highest counter.
    $retained = @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'artifacts/releases') -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^release-(\d+\.\d+\.\d+|\d+)-[0-9a-f]{8}$' } |
        Sort-Object -Descending -Property @{ Expression = { ($_.Name -split '-')[1].Contains('.') } }, @{ Expression = {
            $label = ($_.Name -split '-')[1]
            if ($label.Contains('.')) { [version]$label } else { [version]::new(0, [int]$label) } } })
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
    # or foreign environment is refused before provisioning.
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
    Assert-AzdEnvironment -Values (Get-AzdValues -Root $Root)
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

    # R605: the restore point is read and recorded before the first SQL
    # statement; it is the input to the runbook's point-in-time restore.
    $sqlServer = [string]$values['AZURE_SQL_SERVER_FQDN'] -replace '\.database\.windows\.net$', ''
    $earliestRestore = (@(Invoke-Az -Failure 'Reading the database restore point failed. Nothing starts.' -Arguments @(
        'sql', 'db', 'show', '--subscription', $subscriptionId, '--resource-group', $resourceGroup,
        '--server', $sqlServer, '--name', $values['AZURE_SQL_DATABASE_NAME'],
        '--query', 'earliestRestoreDate', '--output', 'tsv')) -join '').Trim()
    if ([string]::IsNullOrWhiteSpace($earliestRestore)) { throw 'The database reported no earliestRestoreDate. Nothing starts.' }
    $script:record['Restore point'] = "$([DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss'))Z before SQL; earliestRestoreDate $earliestRestore"
    Write-Host "Restore point recorded: $($script:record['Restore point'])"

    $bundlePath = Join-Path (Split-Path -Parent $script:manifestPath) $Manifest.migrationBundleName
    Push-Location (Join-Path $Root 'src/Pegasus.Web')
    try {
        & $bundlePath --connection $env:ConnectionStrings__Pegasus
        if ($LASTEXITCODE -ne 0) { throw 'Migration bundle failed. Nothing starts; on the destructive route the Worker, the classifier and the Web App stay stopped. Recover forward only; the recorded restore point is the recovery source.' }
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
    #
    # Promotion only (-PromoteOnly): the exact SHA goes to main and dev in one
    # atomic push; dev must still be at it; no tag, nothing was deployed.
    # After the smoke (-AfterSmoke): main alone moves to the SHA the estate
    # now serves and the annotated tag v<version> goes with it in one atomic
    # push (R680, R686); dev must contain it, and may have moved on during
    # the release.
    param([Parameter(Mandatory)][string] $Sha, [switch] $AfterSmoke)
    Invoke-Git -Arguments @('fetch', 'origin', '--prune') | Out-Null
    $mainSha = @(Invoke-Git -Arguments @('rev-parse', 'origin/main'))[0].Trim()
    $devSha = @(Invoke-Git -Arguments @('rev-parse', 'origin/dev'))[0].Trim()
    $tagName = "v$Version"
    if ($mainSha -ceq $Sha) {
        if ($AfterSmoke) {
            # A resumed run after the promotion: the tag may still be owed.
            $remoteTag = Get-RemoteTagSha -Name $tagName
            if ($remoteTag -ceq $Sha) { Write-Host "origin/main and $tagName already read $Sha; promotion was done."; return }
            if ($remoteTag) { throw "origin/main reads $Sha but $tagName points at $remoteTag." }
            New-ReleaseTag -Sha $Sha
            Invoke-Git -Failure "Push of the tag $tagName failed." -Arguments @('push', 'origin', "refs/tags/$tagName") | ForEach-Object { Write-Host $_ }
            if ((Get-RemoteTagSha -Name $tagName) -cne $Sha) { throw "Remote read-back of $tagName does not equal the released SHA." }
            Write-Host "$tagName reads back $Sha; main already read it."
            $script:record['Source'] = "$Sha, promoted after the smoke; main and $tagName read it back"
            return
        }
        # A resumed run: dev may have moved on since the promotion.
        Write-Host "origin/main already reads $Sha; promotion was done (origin/dev is at $devSha)."
        return
    }
    & git -C $RepositoryRoot merge-base --is-ancestor $mainSha $Sha
    if ($LASTEXITCODE -ne 0) { throw 'origin/main is not an ancestor of the approved SHA.' }
    if ($AfterSmoke) {
        & git -C $RepositoryRoot merge-base --is-ancestor $Sha $devSha
        if ($LASTEXITCODE -ne 0) { throw "origin/dev at $devSha does not contain the released $Sha." }
        New-ReleaseTag -Sha $Sha
        Invoke-Git -Failure 'Promotion of main failed; the estate serves the release and main does not record it yet.' -Arguments @(
            'push', '--atomic', "--force-with-lease=refs/heads/main:$mainSha", 'origin', "${Sha}:refs/heads/main", "refs/tags/$tagName") | ForEach-Object { Write-Host $_ }
        Invoke-Git -Arguments @('fetch', 'origin', '--prune') | Out-Null
        $promotedMain = @(Invoke-Git -Arguments @('rev-parse', 'origin/main'))[0].Trim()
        if ($promotedMain -cne $Sha) { throw 'Remote read-back of main does not equal the released SHA.' }
        if ((Get-RemoteTagSha -Name $tagName) -cne $Sha) { throw "Remote read-back of $tagName does not equal the released SHA." }
        Write-Host "main and $tagName read back $Sha after the smoke."
        $script:record['Source'] = "$Sha, promoted from $mainSha after the smoke; main and $tagName read it back"
        return
    }
    if ($devSha -cne $Sha) { throw "origin/dev is at $devSha, not the approved $Sha. Obtain a fresh approval for the current head." }
    Invoke-Git -Failure 'Atomic promotion failed.' -Arguments @(
        'push', '--atomic', "--force-with-lease=refs/heads/dev:$Sha", 'origin', "${Sha}:refs/heads/main", "${Sha}:refs/heads/dev") | ForEach-Object { Write-Host $_ }
    Invoke-Git -Arguments @('fetch', 'origin', '--prune') | Out-Null
    $promotedMain = @(Invoke-Git -Arguments @('rev-parse', 'origin/main'))[0].Trim()
    $promotedDev = @(Invoke-Git -Arguments @('rev-parse', 'origin/dev'))[0].Trim()
    if ($promotedMain -cne $Sha -or $promotedDev -cne $Sha) { throw 'Remote read-back does not equal the approved release SHA.' }
    Write-Host "main and dev read back $Sha."
    $script:record['Source'] = "$Sha, promoted from $mainSha; main and dev read it back"
}

function Assert-DevStillAt {
    # R567: the approval named dev's head; a moved head needs a fresh message.
    # A resumed run is the exception: the estate is part way through the
    # approved SHA, dev may have taken a later merge meanwhile (the rulesets
    # allow it), and the only way forward is the same SHA, so dev need only
    # still contain it.
    param([Parameter(Mandatory)][string] $Sha, [switch] $Resumed)
    Invoke-Git -Arguments @('fetch', 'origin', '--prune') | Out-Null
    $mainSha = @(Invoke-Git -Arguments @('rev-parse', 'origin/main'))[0].Trim()
    if ($mainSha -ceq $Sha) { return }
    $devSha = @(Invoke-Git -Arguments @('rev-parse', 'origin/dev'))[0].Trim()
    if ($devSha -ceq $Sha) { return }
    if ($Resumed) {
        & git -C $RepositoryRoot merge-base --is-ancestor $Sha $devSha
        if ($LASTEXITCODE -ne 0) { throw "origin/dev at $devSha does not contain the approved $Sha; the resumed release cannot be promoted." }
        Write-Host "Resuming: origin/dev has moved on to $devSha and still contains the approved $Sha."
        return
    }
    throw "origin/dev is at $devSha, not the approved $Sha. Obtain a fresh approval for the current head."
}

function Test-ReleaseResumed {
    # Where the estate is, read the way the routes read it: a run is resumed
    # when the Web App is Stopped (the destructive route's containment) or
    # already serves the approved SHA (the packages went on, the smoke or the
    # promotion did not finish). A fresh run finds the old release serving.
    param([Parameter(Mandatory)][string] $Sha)
    $site = Get-WebSite
    if ([string]$site.state -ceq 'Stopped') { return $true }
    $version = Get-WebVersion
    return ($null -ne $version -and [string]$version.sourceSha -ceq $Sha)
}

# --- Default run: preflight, build, approval request -------------------------------

function Read-Protection {
    # R689: dev and main carry rulesets whose specification is the runbook's
    # "Branch and tag protection". The preflight reads them back and warns;
    # it never writes one.
    $json = (& gh api "repos/$gitHubRepository/rulesets" 2>&1) -join "`n"
    $rulesets = @()
    if ($LASTEXITCODE -eq 0) { try { $rulesets = @($json | ConvertFrom-Json) } catch { $rulesets = @() } }
    if ($rulesets.Count -eq 0) { Write-Host "Warning: no ruleset read back from $gitHubRepository (R689)." }
    foreach ($ruleset in $rulesets) { Write-Host "ruleset $($ruleset.name): $($ruleset.target), $($ruleset.enforcement)" }
    foreach ($name in 'dev', 'main') {
        if (@($rulesets | Where-Object { [string]$_.name -ceq $name -and [string]$_.enforcement -ceq 'active' }).Count -eq 0) {
            Write-Host "Warning: $name has no active ruleset named $name (R689)."
        }
    }
}

function Read-CandidateChecks {
    # The push-to-dev run on the candidate is its exact-head evidence (R681);
    # the merged pull requests in the range and their checks are listed with
    # it, and what they close decides the proposed version step (R685). A run
    # that is not green is counted and never waived (R689): the read-only run
    # reports it and the approved run refuses before any write.
    param([Parameter(Mandatory)][string] $MainSha, [Parameter(Mandatory)][string] $DevSha)
    $runsJson = (& gh run list --repo $gitHubRepository --workflow repository-check --branch dev --commit $devSha --json 'status,conclusion,url' 2>&1) -join "`n"
    $devRun = $null
    if ($LASTEXITCODE -eq 0) { try { $devRun = @($runsJson | ConvertFrom-Json) | Select-Object -First 1 } catch { $devRun = $null } }
    if ($null -eq $devRun) { Write-Host "No repository-check run found for $devSha on dev: the candidate waits (R689)."; $notGreen = 1 }
    elseif ([string]$devRun.conclusion -cne 'success') { Write-Host "The dev run for $devSha is $($devRun.status)/$($devRun.conclusion): $($devRun.url)"; $notGreen = 1 }
    else { Write-Host "dev run for ${devSha}: success ($($devRun.url))"; $notGreen = 0 }
    Write-Host ''
    Write-Host '--- Pull requests in the range ---'
    $pullNumbers = @(Invoke-Git -Arguments @('log', '--merges', '--format=%s', "$mainSha..$devSha") |
        ForEach-Object { if ($_ -match 'Merge pull request #(\d+)') { [int]$Matches[1] } } | Sort-Object -Unique)
    # R685: what the pull requests close decides the proposed version step.
    # GitHub links a closing reference only on a pull request into the default
    # branch, and these target dev, so the body's own "Closes #N" lines are
    # read as well as any linked reference (R470).
    $bugFixesOnly = $pullNumbers.Count -gt 0
    $bugFixesReason = if ($bugFixesOnly) { 'every pull request in the range closes only type:bug issues' } else { 'the range has no merged pull request' }
    foreach ($number in $pullNumbers) {
        $json = (& gh pr view $number --repo $gitHubRepository --json 'number,title,state,body,statusCheckRollup,closingIssuesReferences' 2>&1) -join "`n"
        if ($LASTEXITCODE -ne 0) { Write-Host "#$number : gh could not read it ($json)"; $notGreen++; continue }
        $pull = $json | ConvertFrom-Json
        $closingProperty = $pull.PSObject.Properties['closingIssuesReferences']
        $closing = @(if ($null -ne $closingProperty) { @($closingProperty.Value) | ForEach-Object { [int]$_.number } })
        $bodyProperty = $pull.PSObject.Properties['body']
        if ($null -ne $bodyProperty -and -not [string]::IsNullOrWhiteSpace([string]$bodyProperty.Value)) {
            $closing += @([regex]::Matches([string]$bodyProperty.Value, '(?i)\b(?:close|closes|closed|fix|fixes|fixed|resolve|resolves|resolved)\s*:?\s+#(\d+)\b') |
                ForEach-Object { [int]$_.Groups[1].Value })
        }
        $closing = @($closing | Sort-Object -Unique)
        if ($closing.Count -eq 0 -and $bugFixesOnly) { $bugFixesOnly = $false; $bugFixesReason = "#$number closes no issue" }
        foreach ($issue in $closing) {
            $labelsJson = (& gh issue view $issue --repo $gitHubRepository --json 'labels' 2>&1) -join "`n"
            if ($LASTEXITCODE -ne 0) { throw "gh could not read issue #${issue}: $labelsJson" }
            $typeLabels = @(($labelsJson | ConvertFrom-Json).labels | ForEach-Object { [string]$_.name } | Where-Object { $_ -like 'type:*' })
            if ($bugFixesOnly -and ($typeLabels -join ',') -cne 'type:bug') {
                $bugFixesOnly = $false
                $bugFixesReason = "#$number closes #$issue ($(if ($typeLabels.Count -gt 0) { $typeLabels -join ', ' } else { 'no type label' }))"
            }
        }
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
        # A pull request that reports no check at all (merged past the ruleset
        # by the bypass actor, or before it existed) is not green either.
        if ($checks.Count -eq 0 -or $failing.Count -gt 0) { $notGreen++ }
        Write-Host ("#{0} {1}: {2} checks, {3} not successful{4}" -f $pull.number, $pull.title, $checks.Count, $failing.Count, $(if ($checks.Count -eq 0) { ' (no check reported: not green)' } else { '' }))
    }
    if ($notGreen -gt 0) { Write-Host "$notGreen run(s) are not green: the candidate waits; nothing is waived (R689)." }
    return [pscustomobject]@{ NotGreen = $notGreen; BugFixesOnly = $bugFixesOnly; BugFixesReason = $bugFixesReason }
}

function Invoke-Preflight {
    Write-Host '=== Preflight (read-only) ==='
    Invoke-Git -Arguments @('fetch', 'origin', '--prune') | Out-Null
    $mainSha = @(Invoke-Git -Arguments @('rev-parse', 'origin/main'))[0].Trim()
    $devSha = @(Invoke-Git -Arguments @('rev-parse', 'origin/dev'))[0].Trim()
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

    Write-Host ''
    Write-Host '--- Protection ---'
    Read-Protection
    Write-Host ''
    Write-Host '--- CI on the candidate ---'
    $checks = Read-CandidateChecks -MainSha $mainSha -DevSha $devSha
    $bugFixesOnly = $checks.BugFixesOnly
    $bugFixesReason = $checks.BugFixesReason

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
    else {
        Write-Host "Deployable paths: $($deployable.Count)"
        Assert-TestInstanceServes -Sha $devSha
    }

    # The live version is the tag on main (R684, R686); the proposal is a
    # proposal, the approval names the version (R685).
    Write-Host ''
    Write-Host '--- Version ---'
    $live = Get-LiveVersion
    $reportedVersion = if ($null -ne $servingVersion) { [string]$servingVersion.version } else { '?' }
    if ($null -eq $live) { Write-Host "Live version: none (no v* tag on main); the Web App reports $reportedVersion" }
    else {
        Write-Host "Live version: $live (tag v$live on main); the Web App reports $reportedVersion"
        if ($reportedVersion -cne [string]$live) { Write-Host "Warning: the Web App reports $reportedVersion, not ${live}: a rollback leaves it so, and the next version steps above the tag." }
    }
    $buildVersion = ''
    $proposal = $null
    if ($deployable.Count -gt 0) {
        $proposal = Get-ProposedVersion -Live $live -BugFixesOnly $bugFixesOnly -BugFixesReason $bugFixesReason -AddsMigration ($migrationFiles.Count -gt 0)
        Write-Host "Proposed: $($proposal.Version) ($($proposal.Step): $($proposal.Reason))$(if ($proposal.Alternative) { "; or $($proposal.Alternative)" })"
        if ($Version) {
            $step = Assert-VersionStep -Live $live -Candidate $Version
            Write-Host "Build version: $Version ($step, given)"
            $buildVersion = $Version
        }
        else {
            Write-Host "Build version: $($proposal.Version) (proposed; pass -Version to build another)"
            $buildVersion = $proposal.Version
        }
    }
    else { Write-Host 'Version: none; a promotion-only range deploys nothing and carries no tag.' }

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
        ClassifierPresent = $classifierPresent; LiveVersion = $live; BuildVersion = $buildVersion; Proposal = $proposal; NotGreen = $checks.NotGreen
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
    # The folder's contents, so a reused worktree's copy is refreshed rather
    # than nested inside itself.
    $environmentTarget = Join-Path $root ".azure/$releaseEnvironment"
    New-Item -ItemType Directory -Force -Path $environmentTarget | Out-Null
    Copy-Item -Recurse -Force -Path (Join-Path $environmentSource '*') -Destination $environmentTarget
    if (@(& git -C $root status --porcelain).Count -ne 0) { throw "The release worktree at $root is not clean." }
    return $root
}

function Invoke-Build {
    param([Parameter(Mandatory)][string] $Sha)
    Write-Host ''
    Write-Host '=== Build (writes only the worktree) ==='
    $root = New-ReleaseWorktree -Sha $Sha
    Invoke-ReleaseScript -Root $root -Name 'Build-ReleaseArtifacts.ps1' -Failure 'Release build failed.' -Arguments @('-Version', $Version, '-SourceRevision', $Sha)
    $manifestPath = Join-Path $root "artifacts/releases/$Version/release-manifest.json"
    Invoke-ReleaseScript -Root $root -Name 'Test-AzureDeploymentPlan.ps1' -Failure 'Artifact validation failed.' -Arguments @('-Mode', 'Artifact', '-ManifestPath', $manifestPath)
    $manifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -Depth 10
    return [pscustomobject]@{
        Root = $root
        Version = $Version
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
    Write-Host 'One message from the operator names all of these; it grants the Azure writes for exactly them and the promotion of main that follows the smoke:'
    Write-Host "  Release SHA        $($Preflight.DevSha) (from main $($Preflight.MainSha))"
    Write-Host "  Version            $($Build.Version) (live $(if ($null -ne $Preflight.LiveVersion) { "v$($Preflight.LiveVersion)" } else { 'none' }); proposed $($Preflight.Proposal.Version), $($Preflight.Proposal.Step): $($Preflight.Proposal.Reason))"
    Write-Host "  Manifest SHA-256   $($Build.ManifestSha256)"
    $Build.Artifacts | ForEach-Object { Write-Host "  Artifact           $_" }
    Write-Host "  Migration          head $($Preflight.Head) -> $($Build.MigrationIdentity)"
    Write-Host "  Route              $($Preflight.Route)"
    Write-Host "  Provision          $(if ($Preflight.ProvisionNeeded) { 'yes' } else { 'no' })"
    Write-Host "  Targets            $webApp, $workerApp$(if ($Preflight.ClassifierPresent) { ", $classifierApp" }), SQL pegasus-prod-sql-252ow37gij/pegasus"
    Write-Host "  Old Web SHA        $($Preflight.ServingSha) (destructive route only; the approval names it and a window)"
    Write-Host ''
    $provisionArgument = if ($Preflight.ProvisionNeeded) { ' -Provision' } else { '' }
    Write-Host "Normal route:      pwsh ./scripts/Invoke-Release.ps1 -Execute -Version $($Build.Version) -Route Normal -ApprovedReleaseSha $($Preflight.DevSha) -ApprovedManifestSha256 $($Build.ManifestSha256)$provisionArgument"
    Write-Host "Destructive route: pwsh ./scripts/Invoke-Release.ps1 -Execute -Version $($Build.Version) -Route Destructive -ApprovedReleaseSha $($Preflight.DevSha) -ApprovedManifestSha256 $($Build.ManifestSha256) -ApprovedOldWebSourceSha $($Preflight.ServingSha)"
}

# --- Approved run ----------------------------------------------------------------

function Invoke-NormalRoute {
    param([Parameter(Mandatory)][string] $Root, [Parameter(Mandatory)][object] $Manifest, [Parameter(Mandatory)][string] $WebPackagePath, [Parameter(Mandatory)][string] $WebPackageSha256)
    $classifierPresent = Test-ClassifierPresent
    # An additive migration finishes its boundary before anything is
    # provisioned or deployed; the running release is compatible with it.
    $head = Read-MigrationHead -Values (Get-AzdValues -Root $Root)
    if ($head -cne [string]$Manifest.migrationIdentity) {
        Write-Host "Migration head $head differs from the manifest's $($Manifest.migrationIdentity): applying the additive migration first."
        Invoke-Migration -Root $Root -Manifest $Manifest
    }
    else {
        $script:record['Migration'] = "none (head $head)"
    }
    # The Worker is expected live and approved, as before any normal release:
    # a census disabled on purpose is refused, not quietly re-enabled.
    if ($WarmupSettingsChange) {
        # The previous package cannot answer a new warm-up path or status, so
        # the platform would keep failing its start after provision: deploy
        # first, read the exact release back, then provision and read it back
        # again.
        if (-not $Provision) { throw '-WarmupSettingsChange needs -Provision: the warm-up setting is provisioned.' }
        Publish-Web -PackagePath $WebPackagePath -Restart $true
        Wait-PegasusExpectedWebSite -ExpectedSourceRevision $ApprovedReleaseSha -ExpectedVersion $Version
        Invoke-Provision -Root $Root -ExpectedLiveWorkerActivation 'approved-live-worker'
        Wait-PegasusExpectedWebSite -ExpectedSourceRevision $ApprovedReleaseSha -ExpectedVersion $Version
    }
    else {
        if ($Provision) {
            # Provision may restart the site onto the previous package; on this
            # route that package runs against a compatible schema.
            Invoke-Provision -Root $Root -ExpectedLiveWorkerActivation 'approved-live-worker'
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
    $retained = Join-Path $RepositoryRoot "artifacts/releases/release-$Version-$($ApprovedReleaseSha.Substring(0, 8))"
    New-Item -ItemType Directory -Force -Path $retained | Out-Null
    Copy-Item -Recurse -Force -Path (Join-Path $Root "artifacts/releases/$Version/*") -Destination $retained
    # The hash of the environment as this release left it, after its own
    # azd env set and provision, so the next preflight compares like with like.
    Set-Content -LiteralPath (Join-Path $retained 'azd-env.sha256') -Value (Get-AzdEnvironmentHash -Root $RepositoryRoot) -Encoding ascii
    Invoke-Git -Failure 'Unable to remove the release worktree; remove it by hand.' -Arguments @('worktree', 'remove', '--force', $Root) | Out-Null
    $script:record['Evidence'] = "artifacts/releases/release-$Version-$($ApprovedReleaseSha.Substring(0, 8))"
}

function Write-Record {
    Write-Host ''
    Write-Host "=== Record for docs/operations.md (at most 15 lines; owed items become issues) ==="
    Write-Host "## Release $Version - $([DateTimeOffset]::UtcNow.ToString('d MMMM yyyy')) (deployment live)"
    Write-Host ''
    Write-Host "Release $Version deployed <what>. $Route route; <migration or 'no migration'>; <outage or 'no outage'>."
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
        $script:Version = $preflight.BuildVersion
        $build = Invoke-Build -Sha $preflight.DevSha
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
        # R684, R685: the approved version is one step above the live one, or
        # 1.0.0 before any tag; a resumed run after the promotion finds its own
        # tag live. Read before anything else.
        Invoke-Git -Arguments @('fetch', 'origin', '--prune') | Out-Null
        $liveVersion = Get-LiveVersion
        if (@(Invoke-Git -Arguments @('rev-parse', 'origin/main'))[0].Trim() -ceq $ApprovedReleaseSha) {
            if ($null -ne $liveVersion -and [string]$liveVersion -cne $Version) { throw "origin/main is at the released SHA and the live tag is v${liveVersion}, not v${Version}." }
        }
        else {
            $step = Assert-VersionStep -Live $liveVersion -Candidate $Version
            Write-Host "Version: $Version ($step above $(if ($null -ne $liveVersion) { "v$liveVersion" } else { 'no tag' }))."
        }
        $root = Get-ReleaseRoot -Sha $ApprovedReleaseSha
        if (-not (Test-Path -LiteralPath $root)) { throw "No release worktree at ${root}: run the default (read-only) run first." }
        $head = (& git -C $root rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or $head -cne $ApprovedReleaseSha) { throw "The release worktree at $root is not at the approved SHA." }
        if (@(& git -C $root status --porcelain).Count -ne 0) { throw "The release worktree at $root is not clean." }
        $script:manifestPath = Join-Path $root "artifacts/releases/$Version/release-manifest.json"
        if (-not (Test-Path -LiteralPath $script:manifestPath -PathType Leaf)) { throw "No manifest at ${script:manifestPath}: run the read-only run with -Version $Version first." }
        $actualManifestSha256 = (Get-FileHash -LiteralPath $script:manifestPath -Algorithm SHA256).Hash
        if (-not $actualManifestSha256.Equals($ApprovedManifestSha256, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The built manifest does not match the approved SHA-256.'
        }
        $manifest = Get-Content -Raw -LiteralPath $script:manifestPath | ConvertFrom-Json -Depth 10
        if ([string]$manifest.sourceRevision -cne $ApprovedReleaseSha) { throw 'The manifest sourceRevision is not the approved SHA.' }
        if ([string]$manifest.releaseVersion -cne $Version) { throw "The manifest was built for version $($manifest.releaseVersion), not the approved $Version." }
        Invoke-ReleaseScript -Root $root -Name 'Test-AzureDeploymentPlan.ps1' -Failure 'Pre-deploy validation failed.' -Arguments @(
            '-Mode', 'PreDeploy', '-ManifestPath', $script:manifestPath, '-ManifestSha256', $ApprovedManifestSha256)
        $webPackagePath = Join-Path $root "artifacts/releases/$Version/web.zip"
        $webPackageSha256 = [string]@($manifest.artifacts | Where-Object name -eq 'web.zip')[0].sha256
        $script:record['Packages'] = "manifest $($actualManifestSha256.ToUpperInvariant()); " + (@($manifest.artifacts | ForEach-Object { "$($_.name) $($_.sha256)" }) -join '; ')

        # R567 and R682: the approval named dev's head, and the instance must
        # be serving it; both are read before any write. A resumed run (the
        # Web App Stopped by the destructive route, or already serving the
        # release) proved both before its first write; dev may have taken a
        # later merge since and the instance may have moved to it, so the
        # resumed run requires only that dev still contains the approved SHA.
        $resumed = Test-ReleaseResumed -Sha $ApprovedReleaseSha
        Assert-DevStillAt -Sha $ApprovedReleaseSha -Resumed:$resumed
        if ($resumed) { Write-Host "Resuming: the estate is part way through $ApprovedReleaseSha; the test instance was read before the first write and is not read again." }
        else { Assert-TestInstanceServes -Sha $ApprovedReleaseSha }
        # R681, R689: the candidate's push-to-dev run and every pull request in
        # the range are read again here; one that is not green is a refusal,
        # never a waiver.
        $checks = Read-CandidateChecks -MainSha (@(Invoke-Git -Arguments @('rev-parse', 'origin/main'))[0].Trim()) -DevSha $ApprovedReleaseSha
        if ($checks.NotGreen -gt 0) { throw "$($checks.NotGreen) run(s) on the candidate are not green: the candidate waits; nothing is waived (R689)." }
        if ($Route -eq 'Destructive') {
            # The route's preconditions are read before any write, so a
            # refusal writes nothing anywhere.
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
        if ($Route -eq 'Normal') {
            Invoke-NormalRoute -Root $root -Manifest $manifest -WebPackagePath $webPackagePath -WebPackageSha256 $webPackageSha256
        }
        else {
            Invoke-DestructiveRoute -Root $root -Manifest $manifest -WebPackagePath $webPackagePath -WebPackageSha256 $webPackageSha256
        }
        # main records what is live: it moves only now, after the full smoke
        # (R680). A failed release has left it at the SHA still serving.
        Invoke-Promotion -Sha $ApprovedReleaseSha -AfterSmoke
        Complete-Release -Root $root
        Write-Record
    }
}
