#!/usr/bin/env pwsh
<#
.SYNOPSIS
Behaviour tests for the release validation scripts, against a fake Azure CLI.

.DESCRIPTION
Runs Invoke-ProductionSmoke.ps1 -WorkerOnly and Test-AzureDeploymentPlan.ps1
-Mode PreProvision in a child pwsh with a fake `az` and `azd` first on PATH.
The fakes record every argument list and answer from fixture files, so the
tests prove what the scripts refuse before any live read, what they accept,
and which exact target they bind to. No Azure call is made.

Then runs Invoke-Release.ps1 against a stateful fake estate (stop, start and
deploy change what later reads return), a bare Git repository standing in for
origin, a worktree with fixture artifacts that pass the manifest validator,
and PEGASUS_TEST_MIGRATION_HEAD for the one read a fake CLI cannot answer. The
cases prove the order of the containment writes, that nothing starts before the
new Web package is on the stopped site, that a resumed run never contains
twice, when provision runs, and that a stale or unapproved input is refused
before any write. The full smoke is stopped at its run-from-package read by
the fake, so no case reaches SQL or the public origin.

.EXAMPLE
pwsh ./scripts/Test-ReleaseValidation.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')

$productionSmoke = Join-Path $PSScriptRoot 'Invoke-ProductionSmoke.ps1'
$deploymentPlan = Join-Path $PSScriptRoot 'Test-AzureDeploymentPlan.ps1'
$expectedSettingNames = @(Get-PegasusWorkerDisabledSettingNames)
$approvedSubscription = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$root = Join-Path ([IO.Path]::GetTempPath()) "pegasus-release-validation-$([guid]::NewGuid().ToString('N'))"

function Assert-True {
    param(
        [Parameter(Mandatory)][bool] $Condition,
        [Parameter(Mandatory)][string] $Case,
        [Parameter(Mandatory)][string] $Failure,
        [string] $Diagnostic = ''
    )

    if (-not $Condition) {
        throw "$Case`: $Failure`n$Diagnostic"
    }
}

function ConvertTo-SingleLine {
    param([AllowEmptyString()][string] $Value)

    # pwsh wraps error text at the console width and prefixes continuation
    # lines with '|'; collapse both so a sentence can be matched whole.
    return [regex]::Replace([regex]::Replace($Value, '\s*\|\s*', ' '), '\s+', ' ')
}

function New-ExactSettings {
    param([Parameter(Mandatory)][string] $Value)

    $settings = [System.Collections.Generic.List[hashtable]]::new()
    foreach ($name in $expectedSettingNames) {
        $settings.Add([ordered]@{ name = $name; value = $Value })
    }
    # The comma keeps the list whole instead of unrolling it into an array.
    return , $settings
}

function New-CompiledWorkerTemplate {
    $resources = @($expectedSettingNames | ForEach-Object {
        [ordered]@{ name = $_; value = "[if(variables('workerActivationApproved'), 'false', 'true')]" }
    })
    # Ordered: the validator's regexes read name before value and type before
    # defaultValue, and a plain hashtable serialises in hash order.
    return [ordered]@{
        resources = $resources
        variables = @{ workerActivationApproved = "[equals(parameters('workerActivation'), 'approved-live-worker')]" }
        parameters = @{ workerActivation = [ordered]@{ type = 'string'; defaultValue = 'disabled' } }
    } | ConvertTo-Json -Depth 6 -Compress
}

function New-ValidPreProvisionEnvironment {
    return [ordered]@{
        AZURE_SUBSCRIPTION_ID = $approvedSubscription
        AZURE_TENANT_ID = '858cf5b3-aa0a-47a6-9b40-4851fd0afa94'
        AZURE_RESOURCE_GROUP = 'rg-pegasus-prod'
        WORKER_APP_NAME = 'pegasus-prod-worker-252ow37gij'
        PEGASUS_WORKER_ACTIVATION = 'disabled'
        BOX_HOLDING_FOLDER_ID = 'test-holding-folder'
        AZURE_KEY_VAULT_NAME = 'pegasusprodkv252ow37g'
        GITHUB_PROBLEM_REPORT_TOKEN_SECRET_URI =
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/problem-report-token/version-one'
        GITHUB_PROBLEM_REPORT_REPOSITORY = 'example/private-problem-reports'
        GLASS_VALUATION_USERNAME_SECRET_URI =
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/glass-valuation-username/version-one'
        GLASS_VALUATION_PASSWORD_SECRET_URI =
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/glass-valuation-password/version-one'
        CAZANA_COMPANION_EMAIL_SECRET_URI =
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/cazana-companion-email/version-one'
        CAZANA_COMPANION_PASSWORD_SECRET_URI =
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/cazana-companion-password/version-one'
        BREGO_VALUATION_EMAIL_SECRET_URI =
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/brego-valuation-email/version-one'
        BREGO_VALUATION_PASSWORD_SECRET_URI =
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/brego-valuation-password/version-one'
        ANTHROPIC_API_KEY_SECRET_URI =
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/anthropic-api-key/version-one'
        AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS =
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing-current/version-one,' +
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing-retained/version-two'
        AUTOMATION_MCP_ENCRYPTION_CERTIFICATE_SECRET_URIS =
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/encryption/version-three'
    }
}

function Write-FakeCli {
    <#
        The fake `az` appends its arguments to PEGASUS_TEST_AZ_ARGUMENTS_PATH,
        answers `bicep build` with the compiled-template fixture, a
        PendingWorkRecoverySchedule query with the once-per-minute cron,
        `functionapp list` and the classifier's `resource show` from the
        classifier-state fixture (empty: no classifier), and every other call
        with the settings fixture.
        The fake `azd` prints the environment fixture.
    #>
    param([Parameter(Mandatory)][string] $Directory)

    # PowerShell resolves a bare `az` to az.ps1 on PATH on Windows and Linux
    # alike, so one fake serves both platforms and no cmd or sh quoting is
    # involved. The scripts under test call `& az ...` from pwsh.
    Set-Content -LiteralPath (Join-Path $Directory 'az.ps1') -Value @'
param([Parameter(ValueFromRemainingArguments)][string[]] $Arguments)
$joined = $Arguments -join ' '
Add-Content -LiteralPath $env:PEGASUS_TEST_AZ_ARGUMENTS_PATH -Value $joined
if ($joined.Contains('bicep build')) { Get-Content -Raw -LiteralPath $env:PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH; exit 0 }
if ($joined.Contains('PendingWorkRecoverySchedule')) { '0 * * * * *'; exit 0 }
if ($joined.Contains('functionapp list')) { if ($env:PEGASUS_TEST_AZ_CLASSIFIER_STATE) { 'pegasus-prod-classifier-252ow37gij' }; exit 0 }
if ($joined.Contains('resource show') -and $joined.Contains('pegasus-prod-classifier-252ow37gij')) { $env:PEGASUS_TEST_AZ_CLASSIFIER_STATE; exit 0 }
Get-Content -Raw -LiteralPath $env:PEGASUS_TEST_AZ_SETTINGS_PATH
exit 0
'@ -Encoding ascii
    Set-Content -LiteralPath (Join-Path $Directory 'azd.ps1') -Value @'
Get-Content -LiteralPath $env:PEGASUS_TEST_AZD_VALUES_PATH
exit 0
'@ -Encoding ascii
}

# --- Invoke-Release.ps1 fixtures ---------------------------------------------

$releaseScript = Join-Path $PSScriptRoot 'Invoke-Release.ps1'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$releaseVersion = '0.1.0-alpha.1'
$releaseWebOrigin = 'https://pegasus-prod-web-252ow37gij.azurewebsites.net/'
$oldWebSha = 'a' * 40

function New-ReleaseEnvironment {
    # The PreProvision environment plus what PreMigration, the migration host
    # and the head read require.
    $environment = New-ValidPreProvisionEnvironment
    $environment['AZURE_SQL_SERVER_FQDN'] = 'pegasus-prod-sql-252ow37gij.database.windows.net'
    $environment['AZURE_SQL_DATABASE_NAME'] = 'pegasus'
    $environment['WEB_IDENTITY_CLIENT_ID'] = '00000000-0000-0000-0000-000000000001'
    $environment['WORKER_IDENTITY_CLIENT_ID'] = '00000000-0000-0000-0000-000000000002'
    $environment['TRANSPORT_STORAGE_ACCOUNT_NAME'] = 'pegtrans252ow37gij'
    $environment['CUSTODY_STORAGE_ACCOUNT_NAME'] = 'pegcustody252ow37gij'
    $environment['GLASS_MARKET_VALUE_ASSESSOR_BASE_URI'] = 'https://example.test/assessor/'
    $environment['GLASS_ESTIMATOR_BASE_URI'] = 'https://example.test/estimator/'
    $environment['GLASS_REPAIR_PROFILE_ID'] = 'profile'
    return $environment
}

function New-Estate {
    # The fake estate's starting state. The fake `az` reads and rewrites it.
    param(
        [string] $WebState = 'Running',
        [bool] $WebServing = $true,
        [string] $WebSourceSha = $oldWebSha,
        [string] $WorkerState = 'Running',
        [string] $WorkerDisabled = 'false',
        [bool] $ClassifierPresent = $true,
        [string] $ClassifierState = 'Running',
        [bool] $StickyServing = $false,
        [Parameter(Mandatory)][string] $NewSha
    )
    return [ordered]@{
        web = [ordered]@{ state = $WebState; serving = $WebServing; sourceSha = $WebSourceSha; deployedSha = $WebSourceSha; version = $releaseVersion; stickyServing = $StickyServing }
        worker = [ordered]@{ state = $WorkerState; disabled = $WorkerDisabled }
        classifier = [ordered]@{ present = $ClassifierPresent; state = $ClassifierState }
        newSha = $NewSha
    }
}

function Write-FakeReleaseCli {
    <#
        The stateful fakes. `az` answers reads from the estate file and
        rewrites it on stop, start, deploy and census writes; its HTTP probes
        (`az rest`) answer from the same file. `azd` reads and rewrites the
        environment fixture. `gh` answers one green pull request. Every call
        is appended to the arguments file with its command name first.
    #>
    param([Parameter(Mandatory)][string] $Directory)

    Set-Content -LiteralPath (Join-Path $Directory 'az.ps1') -Encoding ascii -Value @'
param([Parameter(ValueFromRemainingArguments)][string[]] $Arguments)
$joined = $Arguments -join ' '
Add-Content -LiteralPath $env:PEGASUS_TEST_AZ_ARGUMENTS_PATH -Value "az $joined"
$statePath = $env:PEGASUS_TEST_RELEASE_STATE_PATH
$state = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json
function Save-State { $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statePath -Encoding ascii }
function Get-Argument([string] $Name) {
    $index = [Array]::IndexOf($Arguments, $Name)
    if ($index -ge 0 -and $index + 1 -lt $Arguments.Count) { return $Arguments[$index + 1] }
    return ''
}
$name = Get-Argument '--name'
$web = 'pegasus-prod-web-252ow37gij'
$classifier = 'pegasus-prod-classifier-252ow37gij'
if ($joined.StartsWith('account show')) { '{"subscription":"e6076573-23a5-46a8-acef-7e22d264e5db","tenant":"858cf5b3-aa0a-47a6-9b40-4851fd0afa94","user":"operator@example.test"}'; exit 0 }
if ($joined.StartsWith('account get-access-token')) { 'fake-token'; exit 0 }
if ($joined.Contains('bicep build')) { Get-Content -Raw -LiteralPath $env:PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH; exit 0 }
if ($joined.StartsWith('rest ')) {
    $url = Get-Argument '--url'
    if ($url.EndsWith('/health/live') -or $url.EndsWith('/health/ready')) {
        if ($state.web.serving) { exit 0 }
        [Console]::Error.WriteLine('Forbidden'); exit 1
    }
    if ($url.EndsWith('/diagnostics/version')) {
        if ($state.web.serving) { @{ sourceSha = $state.web.sourceSha; version = $state.web.version } | ConvertTo-Json -Compress; exit 0 }
        [Console]::Error.WriteLine('Forbidden'); exit 1
    }
    '{"value":[]}'; exit 0
}
if ($joined.StartsWith('webapp show')) {
    $siteHost = "$web.azurewebsites.net"
    @{ state = $state.web.state; stack = 'DOTNETCORE|10.0'; host = $siteHost; defaultHostName = $siteHost; hostNames = @($siteHost); enabledHostNames = @($siteHost); httpsOnly = $true } | ConvertTo-Json -Compress
    exit 0
}
if ($joined.StartsWith('webapp stop')) {
    $state.web.state = 'Stopped'
    if (-not $state.web.stickyServing) { $state.web.serving = $false }
    Save-State; exit 0
}
if ($joined.StartsWith('webapp start')) {
    $state.web.state = 'Running'; $state.web.serving = $true; $state.web.sourceSha = $state.web.deployedSha
    Save-State; exit 0
}
if ($joined.StartsWith('webapp deploy')) {
    $state.web.deployedSha = $state.newSha
    if ((Get-Argument '--restart') -eq 'true') { $state.web.state = 'Running'; $state.web.serving = $true; $state.web.sourceSha = $state.newSha }
    Save-State; '{"id":"web-deployment-1"}'; exit 0
}
# WEBSITE_RUN_FROM_PACKAGE answers 0 so the full smoke stops here, before its
# SQL and HTTP reads, which no fake can answer.
if ($joined.StartsWith('webapp config appsettings list')) { '0'; exit 0 }
if ($joined.StartsWith('functionapp list')) { if ($state.classifier.present) { $classifier }; exit 0 }
if ($joined.StartsWith('resource show')) { if ($name -eq $classifier) { $state.classifier.state } else { $state.worker.state }; exit 0 }
if ($joined.StartsWith('functionapp stop')) { if ($name -eq $classifier) { $state.classifier.state = 'Stopped' } else { $state.worker.state = 'Stopped' }; Save-State; exit 0 }
if ($joined.StartsWith('functionapp start')) { if ($name -eq $classifier) { $state.classifier.state = 'Running' } else { $state.worker.state = 'Running' }; Save-State; exit 0 }
if ($joined.StartsWith('functionapp config appsettings set')) {
    $state.worker.disabled = $Arguments[[Array]::IndexOf($Arguments, '--settings') + 1].Split('=')[1]
    Save-State; exit 0
}
if ($joined.StartsWith('functionapp config appsettings list')) {
    if ($joined.Contains('PendingWorkRecoverySchedule')) { '0 * * * * *'; exit 0 }
    if ($joined.Contains("contains(name,'Schedule')")) { "PendingWorkRecoverySchedule`t0 * * * * *"; exit 0 }
    $settings = @($env:PEGASUS_TEST_AZ_SETTING_NAMES.Split(',') | ForEach-Object { @{ name = $_; value = $state.worker.disabled } })
    ConvertTo-Json $settings -Compress; exit 0
}
if ($joined.StartsWith('functionapp deployment source config-zip')) { "{`"id`":`"$name-deployment-1`"}"; exit 0 }
[Console]::Error.WriteLine("fake az: unhandled call: $joined"); exit 2
'@
    Set-Content -LiteralPath (Join-Path $Directory 'azd.ps1') -Encoding ascii -Value @'
param([Parameter(ValueFromRemainingArguments)][string[]] $Arguments)
$joined = $Arguments -join ' '
Add-Content -LiteralPath $env:PEGASUS_TEST_AZ_ARGUMENTS_PATH -Value "azd $joined"
if ($joined.StartsWith('env get-values')) { Get-Content -LiteralPath $env:PEGASUS_TEST_AZD_VALUES_PATH; exit 0 }
if ($joined.StartsWith('env set')) {
    $lines = @(Get-Content -LiteralPath $env:PEGASUS_TEST_AZD_VALUES_PATH | Where-Object { -not $_.StartsWith("$($Arguments[2])=") })
    $lines += "$($Arguments[2])=$($Arguments[3])"
    Set-Content -LiteralPath $env:PEGASUS_TEST_AZD_VALUES_PATH -Value $lines -Encoding ascii
    exit 0
}
if ($joined.StartsWith('provision')) {
    # Provision renders the Worker Disabled census from PEGASUS_WORKER_ACTIVATION.
    $activation = @(Get-Content -LiteralPath $env:PEGASUS_TEST_AZD_VALUES_PATH | Where-Object { $_.StartsWith('PEGASUS_WORKER_ACTIVATION=') })
    $state = Get-Content -Raw -LiteralPath $env:PEGASUS_TEST_RELEASE_STATE_PATH | ConvertFrom-Json
    $state.worker.disabled = if ($activation.Count -eq 1 -and $activation[0].EndsWith('=approved-live-worker')) { 'false' } else { 'true' }
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $env:PEGASUS_TEST_RELEASE_STATE_PATH -Encoding ascii
    'provisioned'; exit 0
}
[Console]::Error.WriteLine("fake azd: unhandled call: $joined"); exit 2
'@
    Set-Content -LiteralPath (Join-Path $Directory 'gh.ps1') -Encoding ascii -Value @'
param([Parameter(ValueFromRemainingArguments)][string[]] $Arguments)
Add-Content -LiteralPath $env:PEGASUS_TEST_AZ_ARGUMENTS_PATH -Value "gh $($Arguments -join ' ')"
'{"number":1,"title":"fixture","state":"MERGED","statusCheckRollup":[{"name":"unit","conclusion":"SUCCESS"}]}'
exit 0
'@
}

function New-FixtureRepository {
    <#
        A small repository standing in for the checkout: the release scripts,
        the infrastructure the plan check reads, the migrations folder, and a
        placeholder for src/Pegasus.Web. main is the first commit; dev adds one
        documentation file. A bare clone is origin.
    #>
    param([Parameter(Mandatory)][string] $Directory)
    $seed = Join-Path $Directory 'seed'
    New-Item -ItemType Directory -Path $seed | Out-Null
    foreach ($relative in 'infra', 'src/Pegasus.Infrastructure/Persistence/Migrations') {
        $target = Join-Path $seed $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        Copy-Item -Recurse -LiteralPath (Join-Path $repositoryRoot $relative) -Destination $target
    }
    # The scripts themselves, not the folders beside them (a local build may
    # have left output under the reference-data loader).
    New-Item -ItemType Directory -Force -Path (Join-Path $seed 'scripts') | Out-Null
    Copy-Item -Path (Join-Path $repositoryRoot 'scripts/*.ps1') -Destination (Join-Path $seed 'scripts')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'azure.yaml') -Destination (Join-Path $seed 'azure.yaml')
    New-Item -ItemType Directory -Force -Path (Join-Path $seed 'src/Pegasus.Web') | Out-Null
    Set-Content -LiteralPath (Join-Path $seed 'src/Pegasus.Web/placeholder.txt') -Value 'fixture' -Encoding ascii
    Set-Content -LiteralPath (Join-Path $seed '.gitignore') -Value @('artifacts/', '.azure/') -Encoding ascii
    $git = @('-c', 'user.name=fixture', '-c', 'user.email=fixture@example.test', '-c', 'commit.gpgsign=false')
    & git -C $seed init --quiet -b main
    & git -C $seed @git add -A
    & git -C $seed @git commit --quiet -m 'main'
    $mainSha = (& git -C $seed rev-parse HEAD).Trim()
    & git -C $seed checkout --quiet -b dev
    New-Item -ItemType Directory -Force -Path (Join-Path $seed 'docs') | Out-Null
    Set-Content -LiteralPath (Join-Path $seed 'docs/fixture.md') -Value 'documentation only' -Encoding ascii
    & git -C $seed @git add -A
    & git -C $seed @git commit --quiet -m 'docs: fixture'
    $devSha = (& git -C $seed rev-parse HEAD).Trim()
    $bare = Join-Path $Directory 'origin.git'
    & git clone --quiet --bare $seed $bare
    $work = Join-Path $Directory 'work'
    & git clone --quiet $bare $work
    & git -C $work checkout --quiet dev
    if ($LASTEXITCODE -ne 0) { throw 'Unable to build the fixture repository.' }
    # The ignored azd environment the script hashes and copies.
    $environmentDirectory = Join-Path $work '.azure/pegasus-prod'
    New-Item -ItemType Directory -Force -Path $environmentDirectory | Out-Null
    Set-Content -LiteralPath (Join-Path $environmentDirectory '.env') -Value 'AZURE_ENV_NAME="pegasus-prod"' -Encoding ascii
    return [pscustomobject]@{ Work = $work; Bare = $bare; MainSha = $mainSha; DevSha = $devSha; Git = $git }
}

function New-FixtureArtifacts {
    # Artifacts that pass Test-PegasusArtifactManifest: the required root
    # entries, a bundle named for this workstation, and a schema-3 manifest
    # whose sizes and hashes match. Returns the manifest SHA-256.
    param([Parameter(Mandatory)][string] $Worktree, [Parameter(Mandatory)][string] $Sha)
    $releaseDirectory = Join-Path $Worktree "artifacts/releases/$releaseVersion"
    New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    function New-FixtureZip {
        param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][string[]] $Entries)
        $stream = [IO.File]::Create($Path)
        $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
        foreach ($entry in $Entries) {
            $writer = [IO.StreamWriter]::new($archive.CreateEntry($entry).Open())
            $writer.Write('fixture')
            $writer.Dispose()
        }
        $archive.Dispose()
        $stream.Dispose()
    }
    New-FixtureZip -Path (Join-Path $releaseDirectory 'web.zip') -Entries @('Pegasus.Web.dll', 'Pegasus.Web.runtimeconfig.json')
    New-FixtureZip -Path (Join-Path $releaseDirectory 'worker.zip') -Entries @('.azurefunctions/functions.metadata')
    New-FixtureZip -Path (Join-Path $releaseDirectory 'classifier.zip') -Entries @('host.json', 'package.json', (Get-PegasusClassifierNativeEntry))
    $bundle = Get-PegasusMigrationBundle
    $bundlePath = Join-Path $releaseDirectory $bundle.Name
    Set-Content -LiteralPath $bundlePath -Value 'fixture bundle: not executable by design' -Encoding ascii
    if ($bundle.IsLinux) { [IO.File]::SetUnixFileMode($bundlePath, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute) }
    $artifacts = @('web.zip', 'worker.zip', 'classifier.zip', $bundle.Name) | ForEach-Object {
        $file = Get-Item -LiteralPath (Join-Path $releaseDirectory $_)
        [ordered]@{ name = $_; sizeBytes = $file.Length; sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
    }
    $manifest = [ordered]@{
        schemaVersion = 3
        releaseVersion = $releaseVersion
        sourceRevision = $Sha
        sourceStatus = 'clean'
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        tools = [ordered]@{ dotnetSdk = 'fixture'; node = 'fixture'; npm = 'fixture'; azureCli = 'fixture'; azureDeveloperCli = 'fixture' }
        migrationIdentity = (Get-PegasusMigrationIdentity -MigrationsDirectory (Join-Path $Worktree 'src/Pegasus.Infrastructure/Persistence/Migrations'))
        webPackage = [ordered]@{ name = 'web.zip'; runtimeIdentifier = 'linux-x64'; selfContained = $false; readyToRun = $true; hostStack = 'DOTNETCORE|10.0' }
        migrationRuntimeIdentifier = $bundle.RuntimeIdentifier
        migrationBundleName = $bundle.Name
        artifacts = @($artifacts)
    }
    $manifestPath = Join-Path $releaseDirectory 'release-manifest.json'
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
    return [pscustomobject]@{
        ManifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
        MigrationIdentity = $manifest.migrationIdentity
    }
}

function Invoke-ReleaseCase {
    param(
        [Parameter(Mandatory)][string] $WorkRoot,
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Estate,
        [string] $MigrationHead = ''
    )
    $script:caseIndex++
    $caseRoot = Join-Path $root ("case-{0:D3}" -f $script:caseIndex)
    New-Item -ItemType Directory -Path $caseRoot | Out-Null
    Write-FakeReleaseCli -Directory $caseRoot
    $argumentsPath = Join-Path $caseRoot 'cli-arguments.txt'
    $statePath = Join-Path $caseRoot 'estate.json'
    Set-Content -LiteralPath $statePath -Value (ConvertTo-Json -InputObject $Estate -Depth 5) -Encoding ascii
    $environmentPath = Join-Path $caseRoot 'environment.txt'
    $environment = New-ReleaseEnvironment
    Set-Content -LiteralPath $environmentPath -Value @($environment.Keys | ForEach-Object { "$_=$($environment[$_])" }) -Encoding ascii
    $templatePath = Join-Path $caseRoot 'compiled-template.json'
    Set-Content -LiteralPath $templatePath -Value (New-CompiledWorkerTemplate) -Encoding ascii

    $savedPath = $env:PATH
    $env:PATH = $caseRoot + [IO.Path]::PathSeparator + $savedPath
    $env:PEGASUS_TEST_AZ_ARGUMENTS_PATH = $argumentsPath
    $env:PEGASUS_TEST_RELEASE_STATE_PATH = $statePath
    $env:PEGASUS_TEST_AZD_VALUES_PATH = $environmentPath
    $env:PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH = $templatePath
    $env:PEGASUS_TEST_AZ_SETTING_NAMES = ($expectedSettingNames -join ',')
    $env:PEGASUS_TEST_MIGRATION_HEAD = $MigrationHead
    try {
        $output = @(& pwsh -NoLogo -NoProfile -File $releaseScript @Arguments -RepositoryRoot $WorkRoot -PollSeconds 0 2>&1 | ForEach-Object { "$_" })
        $exitCode = $LASTEXITCODE
    }
    finally {
        $env:PATH = $savedPath
        foreach ($name in 'PEGASUS_TEST_AZ_ARGUMENTS_PATH', 'PEGASUS_TEST_RELEASE_STATE_PATH', 'PEGASUS_TEST_AZD_VALUES_PATH',
            'PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH', 'PEGASUS_TEST_AZ_SETTING_NAMES', 'PEGASUS_TEST_MIGRATION_HEAD') {
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
    }
    $calls = @()
    if (Test-Path -LiteralPath $argumentsPath) { $calls = @(Get-Content -LiteralPath $argumentsPath) }
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = ConvertTo-SingleLine ($output -join "`n")
        Calls = $calls
        Diagnostic = "$($output -join "`n")`nRecorded calls:`n$($calls -join "`n")"
    }
}

function Get-CallIndex {
    # The first recorded call matching the wildcard pattern, or -1.
    param([Parameter(Mandatory)][AllowEmptyCollection()][string[]] $Calls, [Parameter(Mandatory)][string] $Pattern)
    for ($index = 0; $index -lt $Calls.Count; $index++) {
        if ($Calls[$index] -like $Pattern) { return $index }
    }
    return -1
}

function Get-LastCallIndex {
    param([Parameter(Mandatory)][AllowEmptyCollection()][string[]] $Calls, [Parameter(Mandatory)][string] $Pattern)
    for ($index = $Calls.Count - 1; $index -ge 0; $index--) {
        if ($Calls[$index] -like $Pattern) { return $index }
    }
    return -1
}

function Get-CallCount {
    param([Parameter(Mandatory)][AllowEmptyCollection()][string[]] $Calls, [Parameter(Mandatory)][string] $Pattern)
    return @($Calls | Where-Object { $_ -like $Pattern }).Count
}

function Assert-NoCall {
    param([Parameter(Mandatory)][object] $Result, [Parameter(Mandatory)][string] $Case, [Parameter(Mandatory)][string[]] $Patterns)
    foreach ($pattern in $Patterns) {
        Assert-True ((Get-CallIndex $Result.Calls $pattern) -lt 0) $Case "issued '$pattern'." $Result.Diagnostic
    }
}

function Assert-Order {
    param([Parameter(Mandatory)][object] $Result, [Parameter(Mandatory)][string] $Case, [Parameter(Mandatory)][string[]] $Patterns)
    $previous = -1
    foreach ($pattern in $Patterns) {
        $index = Get-CallIndex $Result.Calls $pattern
        Assert-True ($index -ge 0) $Case "never issued '$pattern'." $Result.Diagnostic
        Assert-True ($index -gt $previous) $Case "issued '$pattern' out of order." $Result.Diagnostic
        $previous = $index
    }
}

$script:caseIndex = 0

function Invoke-Isolated {
    <#
        Runs a script in a child pwsh with the fake CLI first on PATH. Returns
        the exit code, the combined output on one line, and the recorded
        Azure CLI arguments.
    #>
    param(
        [Parameter(Mandatory)][string] $Script,
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][object[]] $Settings,
        [System.Collections.IDictionary] $AzdEnvironment,
        [string] $ClassifierState
    )

    $script:caseIndex++
    $caseRoot = Join-Path $root ("case-{0:D3}" -f $script:caseIndex)
    New-Item -ItemType Directory -Path $caseRoot | Out-Null
    Write-FakeCli -Directory $caseRoot

    $argumentsPath = Join-Path $caseRoot 'azure-arguments.txt'
    $settingsPath = Join-Path $caseRoot 'worker-settings.json'
    Set-Content -LiteralPath $settingsPath -Value (ConvertTo-Json @($Settings) -Compress -Depth 3) -Encoding ascii
    $environmentPath = Join-Path $caseRoot 'environment.txt'
    $lines = @()
    if ($null -ne $AzdEnvironment) {
        $lines = @($AzdEnvironment.Keys | ForEach-Object { "$_=$($AzdEnvironment[$_])" })
    }
    Set-Content -LiteralPath $environmentPath -Value $lines -Encoding ascii
    $templatePath = Join-Path $caseRoot 'compiled-template.json'
    Set-Content -LiteralPath $templatePath -Value (New-CompiledWorkerTemplate) -Encoding ascii

    $savedPath = $env:PATH
    $env:PATH = $caseRoot + [IO.Path]::PathSeparator + $savedPath
    $env:PEGASUS_TEST_AZ_ARGUMENTS_PATH = $argumentsPath
    $env:PEGASUS_TEST_AZ_SETTINGS_PATH = $settingsPath
    $env:PEGASUS_TEST_AZD_VALUES_PATH = $environmentPath
    $env:PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH = $templatePath
    $env:PEGASUS_TEST_AZ_CLASSIFIER_STATE = $ClassifierState
    try {
        $output = @(& pwsh -NoLogo -NoProfile -File $Script @Arguments 2>&1 | ForEach-Object { "$_" })
        $exitCode = $LASTEXITCODE
    }
    finally {
        $env:PATH = $savedPath
        foreach ($name in 'PEGASUS_TEST_AZ_ARGUMENTS_PATH', 'PEGASUS_TEST_AZ_SETTINGS_PATH', 'PEGASUS_TEST_AZD_VALUES_PATH',
            'PEGASUS_TEST_AZ_COMPILED_TEMPLATE_PATH', 'PEGASUS_TEST_AZ_CLASSIFIER_STATE') {
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
    }

    $azureArguments = if (Test-Path -LiteralPath $argumentsPath) { Get-Content -Raw -LiteralPath $argumentsPath } else { '' }
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = ConvertTo-SingleLine ($output -join "`n")
        AzureArguments = $azureArguments
        Diagnostic = "$($output -join "`n")`nRecorded az calls:`n$azureArguments"
    }
}

function Invoke-WorkerSmoke {
    param(
        [Parameter(Mandatory)][object[]] $Settings,
        [Parameter(Mandatory)][string] $ExpectedActivation,
        [string] $SubscriptionId = $approvedSubscription,
        [string] $ExpectedClassifierState,
        [string] $ClassifierState
    )

    $arguments = @(
        '-WorkerOnly',
        '-SubscriptionId', $SubscriptionId,
        '-ResourceGroupName', 'rg-pegasus-prod',
        '-ExpectedWorkerActivation', $ExpectedActivation)
    if ($ExpectedClassifierState) {
        $arguments += '-ExpectedClassifierState', $ExpectedClassifierState
    }
    return Invoke-Isolated -Script $productionSmoke -Settings $Settings -Arguments $arguments -ClassifierState $ClassifierState
}

function Invoke-PreProvision {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Environment
    )

    return Invoke-Isolated -Script $deploymentPlan -Settings (New-ExactSettings 'true') `
        -AzdEnvironment $Environment -Arguments @(
            '-Mode', 'PreProvision',
            '-Environment', 'test',
            '-WorkerActivation', 'disabled',
            '-ExpectedLiveWorkerActivation', 'disabled')
}

function Assert-CensusRejected {
    param(
        [Parameter(Mandatory)][string] $Case,
        [Parameter(Mandatory)][object[]] $Settings,
        [Parameter(Mandatory)][string] $ProtectedSettingName
    )

    $result = Invoke-WorkerSmoke -Settings $Settings -ExpectedActivation 'disabled'
    Assert-True ($result.ExitCode -ne 0) $Case 'should fail.' $result.Diagnostic
    Assert-True ($result.Output.Contains('census differs from the exact five-function release contract')) $Case 'did not report the census.' $result.Diagnostic
    # The rejection names no live setting: the diagnostic must not echo what
    # the production Worker carries.
    Assert-True (-not $result.Output.Contains($ProtectedSettingName)) $Case 'echoed a live setting name.' $result.Diagnostic
}

New-Item -ItemType Directory -Path $root | Out-Null
try {
    # --- Invoke-ProductionSmoke.ps1 -WorkerOnly -----------------------------

    $result = Invoke-WorkerSmoke -Settings (New-ExactSettings 'true') -ExpectedActivation 'disabled'
    $case = 'Exact disabled census'
    Assert-True ($result.ExitCode -eq 0) $case 'should pass.' $result.Diagnostic
    Assert-True ($result.Output.Contains('Production Worker activation smoke passed (disabled).')) $case 'did not report success.' $result.Diagnostic
    foreach ($binding in "--subscription $approvedSubscription", '--name pegasus-prod-worker-252ow37gij', '--resource-group rg-pegasus-prod') {
        Assert-True ($result.AzureArguments.Contains($binding)) $case "did not bind $binding." $result.Diagnostic
    }

    $result = Invoke-WorkerSmoke -Settings (New-ExactSettings 'false') -ExpectedActivation 'approved-live-worker'
    $case = 'Exact approved census'
    Assert-True ($result.ExitCode -eq 0) $case 'should pass.' $result.Diagnostic
    Assert-True ($result.Output.Contains('Production Worker activation smoke passed (approved-live-worker).')) $case 'did not report success.' $result.Diagnostic

    $result = Invoke-WorkerSmoke -Settings (New-ExactSettings 'true') -ExpectedActivation 'disabled' -SubscriptionId '00000000-0000-0000-0000-000000000000'
    $case = 'Unapproved subscription'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
    Assert-True ($result.Output.Contains('does not belong to the set')) $case 'was not refused by parameter validation.' $result.Diagnostic
    Assert-True ([string]::IsNullOrEmpty($result.AzureArguments)) $case 'reached Azure before refusing.' $result.Diagnostic

    $settings = New-ExactSettings 'true'
    $settings.Add([ordered]@{ name = 'AzureWebJobs.UnexpectedFunction.Disabled'; value = 'true' })
    Assert-CensusRejected -Case 'Extra disabled setting' -Settings $settings -ProtectedSettingName 'AzureWebJobs.UnexpectedFunction.Disabled'

    $settings = New-ExactSettings 'true'
    $settings.Add([ordered]@{ name = 'AzureWebJobs.Extra-Function.Disabled'; value = 'true' })
    Assert-CensusRejected -Case 'Malformed disabled setting' -Settings $settings -ProtectedSettingName 'AzureWebJobs.Extra-Function.Disabled'

    $settings = New-ExactSettings 'true'
    $settings.RemoveAll({ param($s) $s.name -eq 'AzureWebJobs.SentEvidencePollFunction.Disabled' }) | Out-Null
    $settings.Add([ordered]@{ name = 'AzureWebJobs.sentevidencepollfunction.Disabled'; value = 'true' })
    Assert-CensusRejected -Case 'Case-variant disabled setting' -Settings $settings -ProtectedSettingName 'AzureWebJobs.sentevidencepollfunction.Disabled'

    $settings = New-ExactSettings 'true'
    $settings.RemoveAll({ param($s) $s.name -eq 'AzureWebJobs.SentEvidencePollFunction.Disabled' }) | Out-Null
    Assert-CensusRejected -Case 'Missing disabled setting' -Settings $settings -ProtectedSettingName 'AzureWebJobs.SentEvidencePollFunction.Disabled'

    $settings = New-ExactSettings 'true'
    $settings.Add([ordered]@{ name = 'AzureWebJobs.SentEvidencePollFunction.Disabled'; value = 'true' })
    Assert-CensusRejected -Case 'Duplicate disabled setting' -Settings $settings -ProtectedSettingName 'AzureWebJobs.SentEvidencePollFunction.Disabled'

    $settings = New-ExactSettings 'true'
    ($settings | Where-Object { $_.name -eq 'AzureWebJobs.SentEvidencePollFunction.Disabled' }).value = 'false'
    $result = Invoke-WorkerSmoke -Settings $settings -ExpectedActivation 'disabled'
    $case = 'Mixed disabled values'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
    Assert-True ($result.Output.Contains("do not match the intended 'disabled' activation value")) $case 'did not report the value mismatch.' $result.Diagnostic
    Assert-True (-not $result.Output.Contains('SentEvidencePollFunction') -and -not $result.Output.Contains('false')) $case 'echoed a live setting.' $result.Diagnostic

    # #1490: the containment route reads the classifier Function App state.
    $result = Invoke-WorkerSmoke -Settings (New-ExactSettings 'true') -ExpectedActivation 'disabled' -ExpectedClassifierState 'Stopped' -ClassifierState 'Stopped'
    $case = 'Classifier Stopped as expected'
    Assert-True ($result.ExitCode -eq 0) $case 'should pass.' $result.Diagnostic
    Assert-True ($result.Output.Contains('Classifier Function App state smoke passed (Stopped).')) $case 'did not report success.' $result.Diagnostic
    Assert-True ($result.AzureArguments.Contains('resource show') -and $result.AzureArguments.Contains('--name pegasus-prod-classifier-252ow37gij')) $case 'did not read the classifier state.' $result.Diagnostic

    $result = Invoke-WorkerSmoke -Settings (New-ExactSettings 'true') -ExpectedActivation 'disabled' -ExpectedClassifierState 'Stopped' -ClassifierState 'Running'
    $case = 'Classifier Running when Stopped is expected'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
    Assert-True ($result.Output.Contains("The classifier Function App state is 'Running', not Stopped.")) $case 'did not name the state.' $result.Diagnostic

    $result = Invoke-WorkerSmoke -Settings (New-ExactSettings 'false') -ExpectedActivation 'approved-live-worker' -ExpectedClassifierState 'Running' -ClassifierState 'Running'
    $case = 'Classifier Running as expected'
    Assert-True ($result.ExitCode -eq 0) $case 'should pass.' $result.Diagnostic
    Assert-True ($result.Output.Contains('Classifier Function App state smoke passed (Running).')) $case 'did not report success.' $result.Diagnostic

    $result = Invoke-WorkerSmoke -Settings (New-ExactSettings 'true') -ExpectedActivation 'disabled' -ExpectedClassifierState 'Stopped'
    $case = 'Classifier absent'
    Assert-True ($result.ExitCode -eq 0) $case 'should pass.' $result.Diagnostic
    Assert-True ($result.Output.Contains('Classifier Function App absent; no state to check.')) $case 'did not report the absent classifier.' $result.Diagnostic
    Assert-True (-not $result.AzureArguments.Contains('resource show')) $case 'read a state for an absent classifier.' $result.Diagnostic

    # --- Test-AzureDeploymentPlan.ps1 -Mode PreProvision ---------------------

    foreach ($key in 'BOX_HOLDING_FOLDER_ID', 'GITHUB_PROBLEM_REPORT_TOKEN_SECRET_URI',
        'GITHUB_PROBLEM_REPORT_REPOSITORY', 'GLASS_VALUATION_USERNAME_SECRET_URI',
        'GLASS_VALUATION_PASSWORD_SECRET_URI', 'CAZANA_COMPANION_EMAIL_SECRET_URI',
        'CAZANA_COMPANION_PASSWORD_SECRET_URI', 'BREGO_VALUATION_EMAIL_SECRET_URI',
        'BREGO_VALUATION_PASSWORD_SECRET_URI', 'ANTHROPIC_API_KEY_SECRET_URI',
        'AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS',
        'AUTOMATION_MCP_ENCRYPTION_CERTIFICATE_SECRET_URIS') {
        foreach ($value in @($null, '', '   ')) {
            $environment = New-ValidPreProvisionEnvironment
            if ($null -eq $value) { $environment.Remove($key) } else { $environment[$key] = $value }
            $result = Invoke-PreProvision -Environment $environment
            $case = "Missing or empty $key ('$value')"
            Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
            Assert-True ($result.Output.Contains("missing $key")) $case 'did not name the missing key.' $result.Diagnostic
            Assert-True (-not $result.AzureArguments.Contains('functionapp config appsettings list')) $case 'reached the Worker smoke.' $result.Diagnostic
        }
    }

    foreach ($key in 'GITHUB_PROBLEM_REPORT_TOKEN_SECRET_URI',
        'GLASS_VALUATION_USERNAME_SECRET_URI', 'GLASS_VALUATION_PASSWORD_SECRET_URI',
        'CAZANA_COMPANION_EMAIL_SECRET_URI', 'CAZANA_COMPANION_PASSWORD_SECRET_URI',
        'BREGO_VALUATION_EMAIL_SECRET_URI', 'BREGO_VALUATION_PASSWORD_SECRET_URI',
        'ANTHROPIC_API_KEY_SECRET_URI') {
        foreach ($value in @(
            'http://pegasusprodkv252ow37g.vault.azure.net/secrets/a-secret/version-one',
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/a-secret',
            'https://pegasusprodkv252ow37g.vault.azure.net/secrets/a-secret/version-one?x=1',
            'https://another-vault.vault.azure.net/secrets/a-secret/version-one')) {
            $environment = New-ValidPreProvisionEnvironment
            $environment[$key] = $value
            $result = Invoke-PreProvision -Environment $environment
            $case = "Invalid $key $value"
            Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
            Assert-True ($result.Output.Contains("$key must be")) $case 'did not name the rule.' $result.Diagnostic
            Assert-True (-not $result.AzureArguments.Contains('functionapp config appsettings list')) $case 'reached the Worker smoke.' $result.Diagnostic
        }
    }

    foreach ($value in @(
        'http://pegasusprodkv252ow37g.vault.azure.net/secrets/signing/version',
        'https://operator@pegasusprodkv252ow37g.vault.azure.net/secrets/signing/version',
        'https://pegasusprodkv252ow37g.vault.azure.net:444/secrets/signing/version',
        'https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing/version?query=value',
        'https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing/version#fragment',
        'https://pegasusprodkv252ow37g.vault.azure.net/secrets/signing')) {
        $environment = New-ValidPreProvisionEnvironment
        $environment['AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS'] = $value
        $result = Invoke-PreProvision -Environment $environment
        $case = "Malformed certificate URI $value"
        Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
        Assert-True ($result.Output.Contains('AUTOMATION_MCP_SIGNING_CERTIFICATE_SECRET_URIS must contain')) $case 'did not name the rule.' $result.Diagnostic
        Assert-True (-not $result.AzureArguments.Contains('functionapp config appsettings list')) $case 'reached the Worker smoke.' $result.Diagnostic
    }

    $environment = New-ValidPreProvisionEnvironment
    $environment['AUTOMATION_MCP_ENCRYPTION_CERTIFICATE_SECRET_URIS'] = 'https://another-vault.vault.azure.net/secrets/encryption/version'
    $result = Invoke-PreProvision -Environment $environment
    $case = 'Cross-vault certificates'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
    Assert-True ($result.Output.Contains('same Azure Key Vault')) $case 'did not name the rule.' $result.Diagnostic
    Assert-True (-not $result.AzureArguments.Contains('functionapp config appsettings list')) $case 'reached the Worker smoke.' $result.Diagnostic

    $result = Invoke-PreProvision -Environment (New-ValidPreProvisionEnvironment)
    $case = 'Valid pre-provision environment'
    Assert-True ($result.ExitCode -eq 0) $case 'should pass.' $result.Diagnostic
    Assert-True ($result.Output.Contains('Azure deployment plan validation passed (PreProvision')) $case 'did not report success.' $result.Diagnostic
    Assert-True ($result.AzureArguments.Contains('functionapp config appsettings list')) $case 'did not reach the Worker smoke.' $result.Diagnostic

    # --- Invoke-Release.ps1 ---------------------------------------------------

    $fixture = New-FixtureRepository -Directory (Join-Path $root 'release')
    $releaseSha = $fixture.DevSha
    $worktree = Join-Path (Split-Path -Parent $fixture.Work) "pegasus-worktrees/release-$($releaseSha.Substring(0, 8))"
    & git -C $fixture.Work worktree add --quiet --detach $worktree $releaseSha
    if ($LASTEXITCODE -ne 0) { throw 'Unable to add the fixture worktree.' }
    Copy-Item -Recurse -LiteralPath (Join-Path $fixture.Work '.azure') -Destination (Join-Path $worktree '.azure')
    $artifacts = New-FixtureArtifacts -Worktree $worktree -Sha $releaseSha
    $identity = $artifacts.MigrationIdentity
    $executeNormal = @('-Execute', '-ReleaseNumber', '1', '-Route', 'Normal', '-ApprovedReleaseSha', $releaseSha, '-ApprovedManifestSha256', $artifacts.ManifestSha256)
    $executeDestructive = @('-Execute', '-ReleaseNumber', '1', '-Route', 'Destructive', '-ApprovedReleaseSha', $releaseSha, '-ApprovedManifestSha256', $artifacts.ManifestSha256)
    $writes = @('az webapp deploy*', 'az webapp stop*', 'az webapp start*', 'az functionapp stop*', 'az functionapp start*',
        'az functionapp deployment source config-zip*', 'az functionapp config appsettings set*', 'azd provision*', 'azd env set*')

    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments @('-PreflightOnly') -Estate (New-Estate -NewSha $releaseSha) -MigrationHead $identity
    $case = 'Preflight only'
    Assert-True ($result.ExitCode -eq 0) $case 'should pass.' $result.Diagnostic
    Assert-True ($result.Output.Contains('Route: promotion-only')) $case 'did not classify a documentation-only range as promotion only.' $result.Diagnostic
    Assert-True ($result.Output.Contains("Migration head: $identity; candidate identity: $identity")) $case 'did not compare the head with the candidate identity.' $result.Diagnostic
    Assert-True ($result.Output.Contains('Provision: needed (no retained release')) $case 'did not say provision is needed without a retained baseline.' $result.Diagnostic
    Assert-NoCall -Result $result -Case $case -Patterns $writes
    Assert-True ((& git -C $fixture.Bare rev-parse main).Trim() -eq $fixture.MainSha) $case 'moved main.' $result.Diagnostic

    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Estate (New-Estate -NewSha $releaseSha) -MigrationHead $identity `
        -Arguments @('-Execute', '-ReleaseNumber', '1', '-Route', 'Normal', '-ApprovedReleaseSha', $releaseSha, '-ApprovedManifestSha256', ('0' * 64))
    $case = 'Manifest SHA-256 mismatch'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
    Assert-True ($result.Output.Contains('does not match the approved SHA-256')) $case 'did not name the mismatch.' $result.Diagnostic
    Assert-NoCall -Result $result -Case $case -Patterns $writes
    Assert-True ((& git -C $fixture.Bare rev-parse main).Trim() -eq $fixture.MainSha) $case 'promoted before refusing.' $result.Diagnostic

    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments $executeDestructive -Estate (New-Estate -NewSha $releaseSha) -MigrationHead $identity
    $case = 'Destructive route without the old Web SHA'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
    Assert-True ($result.Output.Contains('-ApprovedOldWebSourceSha')) $case 'did not name the missing parameter.' $result.Diagnostic
    Assert-NoCall -Result $result -Case $case -Patterns $writes
    Assert-True ((& git -C $fixture.Bare rev-parse main).Trim() -eq $fixture.MainSha) $case 'promoted before refusing.' $result.Diagnostic

    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments ($executeDestructive + @('-ApprovedOldWebSourceSha', $oldWebSha)) `
        -Estate (New-Estate -NewSha $releaseSha -WebSourceSha ('c' * 40)) -MigrationHead $identity
    $case = 'Destructive route against a Web App at another SHA'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
    Assert-True ($result.Output.Contains('neither the approved old SHA nor the release')) $case 'did not name the SHA.' $result.Diagnostic
    Assert-NoCall -Result $result -Case $case -Patterns $writes
    Assert-True ((& git -C $fixture.Bare rev-parse main).Trim() -eq $fixture.MainSha) $case 'promoted before refusing.' $result.Diagnostic

    # The fresh destructive run: it promotes, disables, stages, stops in order,
    # reads the containment back and reaches the migration gate; the fixture
    # bundle cannot run, so it stops there with nothing deployed or started.
    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments ($executeDestructive + @('-ApprovedOldWebSourceSha', $oldWebSha)) `
        -Estate (New-Estate -NewSha $releaseSha) -MigrationHead 'old'
    $case = 'Destructive route, fresh run'
    Assert-True ($result.ExitCode -ne 0) $case 'should stop at the fixture bundle.' $result.Diagnostic
    Assert-True ((& git -C $fixture.Bare rev-parse main).Trim() -eq $releaseSha -and (& git -C $fixture.Bare rev-parse dev).Trim() -eq $releaseSha) $case 'did not promote main and dev to the approved SHA.' $result.Diagnostic
    Assert-Order -Result $result -Case $case -Patterns @(
        'az functionapp config appsettings set*=true*',
        'az functionapp config appsettings list*AzureWebJobs*',
        'az functionapp deployment source config-zip*--name pegasus-prod-worker-252ow37gij*',
        'az functionapp stop*--name pegasus-prod-classifier-252ow37gij*',
        'az functionapp stop*--name pegasus-prod-worker-252ow37gij*',
        'az webapp stop*',
        'az rest*/health/live*',
        'azd env get-values*')
    Assert-True ((Get-CallCount $result.Calls 'az functionapp deployment source config-zip*') -eq 1) $case 'staged the Worker package more than once.' $result.Diagnostic
    Assert-True ((Get-CallIndex $result.Calls 'azd env get-values*') -gt (Get-CallIndex $result.Calls 'az rest*/health/live*')) $case 'reached the migration gate before the containment read-back.' $result.Diagnostic
    Assert-NoCall -Result $result -Case $case -Patterns @('az webapp deploy*', 'az webapp start*', 'az functionapp start*', 'azd provision*')
    $probe = @($result.Calls | Where-Object { $_ -like 'az rest*/health/live*' })[0]
    Assert-True ($probe.Contains("--url $($releaseWebOrigin)health/live")) $case 'probed something other than the origin plus health/live.' $result.Diagnostic

    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments ($executeDestructive + @('-ApprovedOldWebSourceSha', $oldWebSha)) `
        -Estate (New-Estate -NewSha $releaseSha -StickyServing $true) -MigrationHead 'old'
    $case = 'Destructive route, old Web still serving after stop'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
    Assert-True ($result.Output.Contains('did not read back Stopped and unserved')) $case 'did not name the containment failure.' $result.Diagnostic
    Assert-NoCall -Result $result -Case $case -Patterns @('azd *', 'az webapp deploy*', 'az webapp start*', 'az functionapp start*')

    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments ($executeDestructive + @('-ApprovedOldWebSourceSha', $oldWebSha)) `
        -Estate (New-Estate -NewSha $releaseSha -WebState 'Stopped' -WebServing $false -WorkerState 'Stopped' -WorkerDisabled 'true' -ClassifierState 'Stopped') -MigrationHead 'old'
    $case = 'Destructive route resumed before SQL'
    Assert-True ($result.ExitCode -ne 0) $case 'should stop at the fixture bundle.' $result.Diagnostic
    Assert-True ($result.Output.Contains("continuing from 'migrate'")) $case 'did not resume at the migration.' $result.Diagnostic
    Assert-NoCall -Result $result -Case $case -Patterns @('az webapp stop*', 'az functionapp stop*', 'az functionapp config appsettings set*', 'az webapp deploy*', 'az webapp start*', 'az functionapp start*')
    # The stage read calls azd first; the migration gate's read comes after the classifier Stopped read of the pre-SQL smoke.
    Assert-True ((Get-LastCallIndex $result.Calls 'azd env get-values*') -gt (Get-CallIndex $result.Calls 'az resource show*--name pegasus-prod-classifier-252ow37gij*')) $case 'reached the migration gate before the pre-SQL classifier read.' $result.Diagnostic

    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments ($executeDestructive + @('-ApprovedOldWebSourceSha', $oldWebSha)) `
        -Estate (New-Estate -NewSha $releaseSha -WebState 'Stopped' -WebServing $false -WorkerState 'Stopped' -WorkerDisabled 'true' -ClassifierState 'Stopped') -MigrationHead $identity
    $case = 'Destructive route resumed after SQL'
    Assert-True ($result.ExitCode -ne 0) $case 'should stop at the full smoke run-from-package read.' $result.Diagnostic
    Assert-True ($result.Output.Contains('WEBSITE_RUN_FROM_PACKAGE')) $case 'did not reach the full smoke.' $result.Diagnostic
    Assert-True ($result.Output.Contains("continuing from 'deploy-web'")) $case 'did not resume at the Web deploy.' $result.Diagnostic
    Assert-NoCall -Result $result -Case $case -Patterns @('az webapp stop*', 'az functionapp stop*', 'az functionapp config appsettings set*')
    Assert-Order -Result $result -Case $case -Patterns @(
        'az webapp deploy*--restart false*--track-status false*',
        'azd env set PEGASUS_WEB_ACTIVATION approved*',
        'azd env set PEGASUS_WORKER_ACTIVATION approved-live-worker*',
        'azd provision*',
        'az webapp start*',
        'az rest*/health/ready*',
        'az rest*/diagnostics/version*',
        'az functionapp start*--name pegasus-prod-worker-252ow37gij*',
        'az functionapp start*--name pegasus-prod-classifier-252ow37gij*',
        'az functionapp deployment source config-zip*--name pegasus-prod-classifier-252ow37gij*')
    Assert-True ((Get-CallCount $result.Calls 'azd provision*') -eq 1) $case 'provisioned other than once.' $result.Diagnostic
    Assert-True ((Get-CallIndex $result.Calls 'az webapp start*') -gt (Get-CallIndex $result.Calls 'az webapp deploy*')) $case 'started the site before the new package was on it.' $result.Diagnostic

    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments $executeNormal -Estate (New-Estate -NewSha $releaseSha) -MigrationHead $identity
    $case = 'Normal route without provision'
    Assert-True ($result.ExitCode -ne 0) $case 'should stop at the full smoke run-from-package read.' $result.Diagnostic
    Assert-True ($result.Output.Contains('WEBSITE_RUN_FROM_PACKAGE')) $case 'did not reach the full smoke.' $result.Diagnostic
    Assert-NoCall -Result $result -Case $case -Patterns @('azd provision*', 'azd env set*', 'az webapp stop*', 'az functionapp stop*', 'az functionapp config appsettings set*', '*--track-status*')
    Assert-Order -Result $result -Case $case -Patterns @(
        'az webapp deploy*--restart true*',
        'az rest*/health/ready*',
        'az rest*/diagnostics/version*',
        'az functionapp deployment source config-zip*--name pegasus-prod-worker-252ow37gij*',
        'az functionapp deployment source config-zip*--name pegasus-prod-classifier-252ow37gij*')
    $probe = @($result.Calls | Where-Object { $_ -like 'az rest*/health/ready*' })[0]
    Assert-True ($probe.Contains("--url $($releaseWebOrigin)health/ready")) $case 'probed something other than the origin plus health/ready.' $result.Diagnostic

    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments ($executeNormal + @('-Provision')) -Estate (New-Estate -NewSha $releaseSha) -MigrationHead $identity
    $case = 'Normal route with provision'
    Assert-True ($result.ExitCode -ne 0) $case 'should stop at the full smoke run-from-package read.' $result.Diagnostic
    Assert-True ($result.Output.Contains('WEBSITE_RUN_FROM_PACKAGE')) $case 'did not reach the full smoke.' $result.Diagnostic
    Assert-Order -Result $result -Case $case -Patterns @('azd env set PEGASUS_WEB_ACTIVATION approved*', 'azd provision*', 'az webapp deploy*--restart true*')
    Assert-True ((Get-CallCount $result.Calls 'azd provision*') -eq 1) $case 'provisioned other than once.' $result.Diagnostic

    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments $executeNormal -Estate (New-Estate -NewSha $releaseSha -ClassifierPresent $false) -MigrationHead $identity
    $case = 'Normal route without a classifier'
    Assert-True ($result.ExitCode -ne 0) $case 'should stop at the full smoke run-from-package read.' $result.Diagnostic
    Assert-True ($result.Output.Contains('WEBSITE_RUN_FROM_PACKAGE')) $case 'did not reach the full smoke.' $result.Diagnostic
    Assert-NoCall -Result $result -Case $case -Patterns @('*--name pegasus-prod-classifier-252ow37gij*')

    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments @('-PromoteOnly', '-ApprovedReleaseSha', $releaseSha) -Estate (New-Estate -NewSha $releaseSha)
    $case = 'Promotion already done'
    Assert-True ($result.ExitCode -eq 0) $case 'should pass.' $result.Diagnostic
    Assert-True ($result.Output.Contains('promotion was done')) $case 'did not report the promotion as done.' $result.Diagnostic
    Assert-True ($result.Calls.Count -eq 0) $case 'called a CLI.' $result.Diagnostic

    # dev moves after the approval: the approved SHA is stale and nothing is
    # pushed or written.
    Set-Content -LiteralPath (Join-Path $fixture.Work 'docs/later.md') -Value 'a later change' -Encoding ascii
    & git -C $fixture.Work @($fixture.Git) add -A
    & git -C $fixture.Work @($fixture.Git) commit --quiet -m 'docs: later'
    & git -C $fixture.Work push --quiet origin dev
    if ($LASTEXITCODE -ne 0) { throw 'Unable to move the fixture dev branch.' }
    $movedSha = (& git -C $fixture.Work rev-parse HEAD).Trim()
    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments @('-PromoteOnly', '-ApprovedReleaseSha', $releaseSha) -Estate (New-Estate -NewSha $releaseSha)
    $case = 'Promotion of a stale SHA'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
    Assert-True ($result.Output.Contains("origin/dev is at $movedSha")) $case 'did not name where dev is.' $result.Diagnostic
    Assert-True ((& git -C $fixture.Bare rev-parse main).Trim() -eq $releaseSha) $case 'moved main.' $result.Diagnostic
    $result = Invoke-ReleaseCase -WorkRoot $fixture.Work -Arguments $executeNormal -Estate (New-Estate -NewSha $releaseSha) -MigrationHead $identity
    $case = 'Execute with a stale SHA'
    Assert-True ($result.ExitCode -ne 0) $case 'should fail.' $result.Diagnostic
    Assert-True ($result.Output.Contains("origin/dev is at $movedSha")) $case 'did not name where dev is.' $result.Diagnostic
    Assert-NoCall -Result $result -Case $case -Patterns $writes

    & git -C $fixture.Work worktree remove --force $worktree

    Write-Output "Release validation behaviour tests passed ($script:caseIndex isolated runs)."
}
finally {
    if (Test-Path -LiteralPath $root) {
        $resolvedRoot = [IO.Path]::GetFullPath($root)
        $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
        if ((Split-Path $resolvedRoot -Parent) -ne $temporaryRoot -or
            (Split-Path $resolvedRoot -Leaf) -notmatch '^pegasus-release-validation-[0-9a-f]{32}$') {
            throw "Refusing to remove unexpected directory '$resolvedRoot'."
        }
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
