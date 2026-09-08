[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')

# Exercise the shared mapping without invoking a compiler, database or cloud.
$nativeBundle = Get-PegasusMigrationBundle
$expectedWorkerDisabledSettings = @(
    'AzureWebJobs.PendingWorkRecoveryFunction.Disabled',
    'AzureWebJobs.UnifiedWorkFunction.Disabled',
    'AzureWebJobs.UnifiedWorkPoisonFunction.Disabled',
    'AzureWebJobs.StagedArtifactReconciliationFunction.Disabled',
    'AzureWebJobs.InboxRecoveryFunction.Disabled',
    'AzureWebJobs.SentEvidencePollFunction.Disabled',
    'AzureWebJobs.DueWorkSweepFunction.Disabled'
)
$actualWorkerDisabledSettings = @(Get-PegasusWorkerDisabledSettingNames)
if ($actualWorkerDisabledSettings.Count -ne $expectedWorkerDisabledSettings.Count -or
    @($actualWorkerDisabledSettings | Where-Object { $_ -notin $expectedWorkerDisabledSettings }).Count -ne 0 -or
    @($actualWorkerDisabledSettings | Select-Object -Unique).Count -ne $actualWorkerDisabledSettings.Count) {
    throw 'Worker Disabled setting producer must return the exact distinct seven-name census.'
}
foreach ($consumer in @('Test-AzureDeploymentPlan.ps1', 'Invoke-ProductionSmoke.ps1')) {
    $consumerSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot $consumer)
    if ($consumerSource -notmatch '\@\(Get-PegasusWorkerDisabledSettingNames\)') {
        throw "$consumer must consume the canonical Worker Disabled setting producer."
    }
}
$productionSmokeSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Invoke-ProductionSmoke.ps1')
foreach ($requiredContract in @(
    'if \(-not \$ActivationOnly -and -not \$censusIsExact\)',
    'The live Worker disabled-setting census differs from the exact seven-function release contract\.',
    'if \(-not \$valuesAreExact\)',
    'The live Worker settings do not match the intended'
)) {
    if ($productionSmokeSource -notmatch $requiredContract) {
        throw 'Production Worker smoke must retain its fail-closed exact census and value contracts.'
    }
}
$platformResolver = ${function:Get-PegasusPlatform}
try {
    foreach ($hostKind in @('Windows', 'Linux')) {
        function Get-PegasusPlatform {
            [pscustomobject]@{ Kind = $hostKind; IsWindows = $hostKind -eq 'Windows'; IsLinux = $hostKind -eq 'Linux' }
        }
        $bundle = Get-PegasusMigrationBundle
        $expectedRuntime = if ($hostKind -eq 'Windows') { 'win-x64' } else { 'linux-x64' }
        $expectedName = if ($hostKind -eq 'Windows') { 'efbundle.exe' } else { 'efbundle' }
        if ($bundle.RuntimeIdentifier -cne $expectedRuntime -or $bundle.Name -cne $expectedName -or
            $bundle.IsLinux -ne ($hostKind -eq 'Linux')) {
            throw "Incorrect migration bundle identity for $hostKind."
        }
        $orasHint = Get-PegasusRepairHint -Id 'oras'
        if ($orasHint -cne 'Install ORAS 1.3.4 from https://oras.land/docs/installation/') {
            throw "Incorrect ORAS installation guidance for $hostKind."
        }
    }
}
finally {
    ${function:Get-PegasusPlatform} = $platformResolver
}

# Load the real manifest function only: full validation also compiles Bicep.
$parseErrors = $null
$validationAst = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'Test-AzureDeploymentPlan.ps1'), [ref]$null, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw 'Deployment-plan script does not parse.' }
$manifestFunction = $validationAst.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Test-ArtifactManifest'
}, $false)
if ($null -eq $manifestFunction) { throw 'Manifest validator is missing.' }
. ([scriptblock]::Create($manifestFunction.Extent.Text))

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("pegasus-release-contract-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
try {
    $digest = 'sha256:' + ('a' * 64)
    $script:OrasCalls = 0
    function oras {
        $script:OrasCalls++
        $global:LASTEXITCODE = 0
        if ($args[0] -eq 'blob') { return '{"os":"linux","architecture":"amd64"}' }
        if ($args -contains '--descriptor') { return (@{ digest = $digest } | ConvertTo-Json -Compress) }
        return (@{ config = @{ digest = $digest } } | ConvertTo-Json -Compress)
    }

    function New-ZipFixture {
        param(
            [Parameter(Mandatory)][string] $Source,
            [Parameter(Mandatory)][string] $Archive
        )

        if ([IO.File]::Exists($Archive)) {
            [IO.File]::Delete($Archive)
        }
        [IO.Compression.ZipFile]::CreateFromDirectory($Source, $Archive)
    }

    $webArchiveSource = Join-Path $fixtureRoot 'web-archive'
    $workerArchiveSource = Join-Path $fixtureRoot 'worker-archive'
    $missingRootSource = Join-Path $fixtureRoot 'missing-root-archive'
    New-Item -ItemType Directory -Path (Join-Path $webArchiveSource '.playwright') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $workerArchiveSource '.azurefunctions') -Force | Out-Null
    New-Item -ItemType Directory -Path $missingRootSource -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $webArchiveSource '.playwright/fixture.txt') -Value 'fixture' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $workerArchiveSource '.azurefunctions/fixture.txt') -Value 'fixture' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $missingRootSource 'fixture.txt') -Value 'fixture' -Encoding utf8NoBOM
    $webZipPath = Join-Path $fixtureRoot 'web.zip'
    $workerZipPath = Join-Path $fixtureRoot 'worker.zip'
    New-ZipFixture -Source $webArchiveSource -Archive $webZipPath
    New-ZipFixture -Source $workerArchiveSource -Archive $workerZipPath
    foreach ($name in @('web-image.tar.gz', $nativeBundle.Name)) {
        $path = Join-Path $fixtureRoot $name
        Set-Content -LiteralPath $path -Value 'artifact contract fixture' -Encoding utf8NoBOM
    }
    $artifacts = @('web.zip', 'web-image.tar.gz', 'worker.zip', $nativeBundle.Name) | ForEach-Object {
        $path = Join-Path $fixtureRoot $_
        @{ name = $_; sizeBytes = (Get-Item -LiteralPath $path).Length;
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    }
    $bundlePath = Join-Path $fixtureRoot $nativeBundle.Name
    if ($IsLinux) {
        [IO.File]::SetUnixFileMode($bundlePath, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserExecute)
    }
    $manifest = @{
        schemaVersion = 3; sourceRevision = 'a' * 40; sourceStatus = 'clean'; artifacts = $artifacts
        migrationRuntimeIdentifier = $nativeBundle.RuntimeIdentifier; migrationBundleName = $nativeBundle.Name
        webImage = @{ repository = 'pegasus/web'; tag = 'a' * 40; digest = $digest;
            platform = 'linux/amd64'; archive = 'web-image.tar.gz' }
    }
    $manifestPath = Join-Path $fixtureRoot 'release-manifest.json'
    function Assert-Manifest {
        param([string]$ExpectedError)
        $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
        $script:OrasCalls = 0
        $failure = $null
        try { Test-ArtifactManifest -Path $manifestPath }
        catch { $failure = $_.Exception.Message }
        if ($ExpectedError) {
            if (-not $failure -or -not $failure.Contains($ExpectedError) -or $script:OrasCalls -ne 0) {
                throw "Expected rejection '$ExpectedError' before ORAS; got '$failure' ($script:OrasCalls calls)."
            }
        }
        elseif ($failure -or $script:OrasCalls -ne 3) {
            throw "Expected valid manifest and OCI inspection; got '$failure' ($script:OrasCalls calls)."
        }
    }

    Assert-Manifest
    New-ZipFixture -Source $missingRootSource -Archive $workerZipPath
    $manifest.artifacts[2].sizeBytes = (Get-Item -LiteralPath $workerZipPath).Length
    $manifest.artifacts[2].sha256 = (Get-FileHash -LiteralPath $workerZipPath -Algorithm SHA256).Hash
    Assert-Manifest -ExpectedError 'worker.zip must contain .azurefunctions/'
    New-ZipFixture -Source $workerArchiveSource -Archive $workerZipPath
    $manifest.artifacts[2].sizeBytes = (Get-Item -LiteralPath $workerZipPath).Length
    $manifest.artifacts[2].sha256 = (Get-FileHash -LiteralPath $workerZipPath -Algorithm SHA256).Hash
    New-ZipFixture -Source $missingRootSource -Archive $webZipPath
    $manifest.artifacts[0].sizeBytes = (Get-Item -LiteralPath $webZipPath).Length
    $manifest.artifacts[0].sha256 = (Get-FileHash -LiteralPath $webZipPath -Algorithm SHA256).Hash
    Assert-Manifest -ExpectedError 'web.zip must contain .playwright/'
    New-ZipFixture -Source $webArchiveSource -Archive $webZipPath
    $manifest.artifacts[0].sizeBytes = (Get-Item -LiteralPath $webZipPath).Length
    $manifest.artifacts[0].sha256 = (Get-FileHash -LiteralPath $webZipPath -Algorithm SHA256).Hash
    foreach ($wrongName in @('../efbundle', 'wrong.exe')) {
        $manifest.migrationBundleName = $wrongName
        Assert-Manifest -ExpectedError 'for this workstation'
    }
    $manifest.migrationBundleName = $nativeBundle.Name
    $manifest.migrationRuntimeIdentifier = if ($IsWindows) { 'linux-x64' } else { 'win-x64' }
    Assert-Manifest -ExpectedError 'for this workstation'
    $manifest.migrationBundleName = if ($IsWindows) { 'efbundle' } else { 'efbundle.exe' }
    Assert-Manifest -ExpectedError 'for this workstation'
    $manifest.migrationRuntimeIdentifier = $nativeBundle.RuntimeIdentifier
    $manifest.migrationBundleName = $nativeBundle.Name
    $manifest.artifacts[0].sha256 = 'b' * 64
    Assert-Manifest -ExpectedError 'Release artifact identity mismatch'
    $manifest.artifacts[0].sha256 = (Get-FileHash -LiteralPath (Join-Path $fixtureRoot 'web.zip') -Algorithm SHA256).Hash
    if ($IsLinux) {
        [IO.File]::SetUnixFileMode($bundlePath, [IO.UnixFileMode]::UserRead)
        Assert-Manifest -ExpectedError 'must be executable by its owner'
    }
    Write-Output "Release workstation and manifest contract passed ($($nativeBundle.RuntimeIdentifier)); Windows/Linux mappings checked."
}
finally {
    $resolvedFixtureRoot = (Resolve-Path -LiteralPath $fixtureRoot).Path
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not $resolvedFixtureRoot.StartsWith($temporaryRoot + [IO.Path]::DirectorySeparatorChar, (Get-PegasusPathComparison)) -or
        [IO.Path]::GetFileName($resolvedFixtureRoot) -notmatch '^pegasus-release-contract-[0-9a-f]{32}$') {
        throw 'Refusing to remove an unexpected fixture path.'
    }
    Remove-Item -LiteralPath $resolvedFixtureRoot -Recurse -Force
}

if (-not $IsWindows) {
    Write-Output 'LocalDB state classification skipped: Windows-only branch.'
    return
}

$script:RequestedInstance = 'PegasusDevelopment_PLAT014_contract'
$script:FixtureOutput = ''
$script:FixtureExitCode = 0

function Invoke-LocalDbFixture {
    $script:FixtureOutput
    $global:LASTEXITCODE = $script:FixtureExitCode
}

function Assert-DatabaseState {
    param(
        [Parameter(Mandatory)][string]$Case,
        [Parameter(Mandatory)][string]$Output,
        [Parameter(Mandatory)][int]$ExitCode,
        [Parameter(Mandatory)][string]$Expected
    )

    $script:FixtureOutput = $Output
    $script:FixtureExitCode = $ExitCode
    $actual = Get-PegasusDatabaseState `
        -InstanceName $script:RequestedInstance `
        -Command 'Invoke-LocalDbFixture'

    if ($actual -ne $Expected) {
        throw "$Case expected '$Expected'; got '$actual'."
    }
}

$missingFixture = "Printing of LocalDB instance `"$script:RequestedInstance`" information failed because of the following error:`r`n`r`nLocalDB instance `"$script:RequestedInstance`" doesn't exist! "

Assert-DatabaseState -Case 'zero-exit explicit missing instance' -Output $missingFixture -ExitCode 0 -Expected 'Missing'
Assert-DatabaseState -Case 'zero-exit missing different instance' -Output 'LocalDB instance "PegasusDevelopment_other" doesn''t exist! ' -ExitCode 0 -Expected 'Unknown'
Assert-DatabaseState -Case 'zero-exit wrapper-only failure' -Output "Printing of LocalDB instance `"$script:RequestedInstance`" information failed because of the following error:" -ExitCode 0 -Expected 'Unknown'
Assert-DatabaseState -Case 'zero-exit unrecognized response' -Output 'LocalDB returned an unrecognized diagnostic.' -ExitCode 0 -Expected 'Unknown'
Assert-DatabaseState -Case 'running state' -Output 'State: Running' -ExitCode 0 -Expected 'Running'
Assert-DatabaseState -Case 'stopped state' -Output 'State: Stopped' -ExitCode 0 -Expected 'Stopped'
Assert-DatabaseState -Case 'contradictory state and missing response' -Output "State: Running`r`nLocalDB instance `"$script:RequestedInstance`" doesn't exist! " -ExitCode 0 -Expected 'Unknown'
Assert-DatabaseState -Case 'non-zero response' -Output 'LocalDB command failed.' -ExitCode 1 -Expected 'Missing'

$global:LASTEXITCODE = 0
Write-Output 'Pegasus platform LocalDB state classification passed.'
