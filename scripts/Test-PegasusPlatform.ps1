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
    'AzureWebJobs.SentEvidencePollFunction.Disabled'
)
$actualWorkerDisabledSettings = @(Get-PegasusWorkerDisabledSettingNames)
if ($actualWorkerDisabledSettings.Count -ne $expectedWorkerDisabledSettings.Count -or
    @($actualWorkerDisabledSettings | Where-Object { $_ -notin $expectedWorkerDisabledSettings }).Count -ne 0 -or
    @($actualWorkerDisabledSettings | Select-Object -Unique).Count -ne $actualWorkerDisabledSettings.Count) {
    throw 'Worker Disabled setting producer must return the exact distinct five-name census.'
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
        foreach ($cloudTool in @('az', 'azd', 'bicep')) {
            if ([string]::IsNullOrWhiteSpace((Get-PegasusRepairHint -Id $cloudTool))) {
                throw "Missing $cloudTool installation guidance for $hostKind."
            }
        }
    }
}
finally {
    ${function:Get-PegasusPlatform} = $platformResolver
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("pegasus-release-contract-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
try {
    # ADR-0049: the manifest validator must never shell out; web.zip is a plain
    # publish and there is no image tooling to call.
    $script:NativeCalls = 0
    function oras { $script:NativeCalls++; throw 'ORAS must not be invoked.' }
    function docker { $script:NativeCalls++; throw 'docker must not be invoked.' }

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
    $nestedWebSource = Join-Path $fixtureRoot 'nested-web-archive'
    New-Item -ItemType Directory -Path $webArchiveSource -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $workerArchiveSource '.azurefunctions') -Force | Out-Null
    New-Item -ItemType Directory -Path $missingRootSource -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $nestedWebSource 'web') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $webArchiveSource 'Pegasus.Web.dll') -Value 'fixture' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $webArchiveSource 'Pegasus.Web.runtimeconfig.json') -Value '{}' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $workerArchiveSource '.azurefunctions/fixture.txt') -Value 'fixture' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $missingRootSource 'fixture.txt') -Value 'fixture' -Encoding utf8NoBOM
    # A publish zipped one directory too high: the files exist but not at the root.
    Set-Content -LiteralPath (Join-Path $nestedWebSource 'web/Pegasus.Web.dll') -Value 'fixture' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $nestedWebSource 'web/Pegasus.Web.runtimeconfig.json') -Value '{}' -Encoding utf8NoBOM
    $webZipPath = Join-Path $fixtureRoot 'web.zip'
    $workerZipPath = Join-Path $fixtureRoot 'worker.zip'
    New-ZipFixture -Source $webArchiveSource -Archive $webZipPath
    New-ZipFixture -Source $workerArchiveSource -Archive $workerZipPath
    Set-Content -LiteralPath (Join-Path $fixtureRoot $nativeBundle.Name) -Value 'artifact contract fixture' -Encoding utf8NoBOM
    $artifacts = @('web.zip', 'worker.zip', $nativeBundle.Name) | ForEach-Object {
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
        webPackage = @{ name = 'web.zip'; runtimeIdentifier = 'linux-x64'; selfContained = $false;
            readyToRun = $true; hostStack = 'DOTNETCORE|10.0' }
    }
    $manifestPath = Join-Path $fixtureRoot 'release-manifest.json'
    function Assert-Manifest {
        param([string]$ExpectedError)
        $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
        $script:NativeCalls = 0
        $failure = $null
        try { Test-PegasusArtifactManifest -Path $manifestPath }
        catch { $failure = $_.Exception.Message }
        if ($script:NativeCalls -ne 0) {
            throw "The manifest validator invoked image tooling ($script:NativeCalls calls)."
        }
        if ($ExpectedError) {
            if (-not $failure -or -not $failure.Contains($ExpectedError)) {
                throw "Expected rejection '$ExpectedError'; got '$failure'."
            }
        }
        elseif ($failure) {
            throw "Expected a valid manifest; got '$failure'."
        }
    }

    function Set-ArtifactIdentity {
        param([Parameter(Mandatory)][int] $Index, [Parameter(Mandatory)][string] $Path)
        $manifest.artifacts[$Index].sizeBytes = (Get-Item -LiteralPath $Path).Length
        $manifest.artifacts[$Index].sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    }

    Assert-Manifest
    New-ZipFixture -Source $missingRootSource -Archive $workerZipPath
    Set-ArtifactIdentity -Index 1 -Path $workerZipPath
    Assert-Manifest -ExpectedError 'worker.zip must contain .azurefunctions/'
    New-ZipFixture -Source $workerArchiveSource -Archive $workerZipPath
    Set-ArtifactIdentity -Index 1 -Path $workerZipPath
    New-ZipFixture -Source $missingRootSource -Archive $webZipPath
    Set-ArtifactIdentity -Index 0 -Path $webZipPath
    Assert-Manifest -ExpectedError 'web.zip must contain Pegasus.Web.dll at its root'
    New-ZipFixture -Source $nestedWebSource -Archive $webZipPath
    Set-ArtifactIdentity -Index 0 -Path $webZipPath
    Assert-Manifest -ExpectedError 'web.zip must contain Pegasus.Web.dll at its root'
    New-ZipFixture -Source $webArchiveSource -Archive $webZipPath
    Set-ArtifactIdentity -Index 0 -Path $webZipPath
    $manifest.webPackage.hostStack = 'DOTNETCORE|9.0'
    Assert-Manifest -ExpectedError 'Web package identity is incomplete or invalid'
    $manifest.webPackage.hostStack = 'DOTNETCORE|10.0'
    $manifest.webPackage.selfContained = $true
    Assert-Manifest -ExpectedError 'Web package identity is incomplete or invalid'
    $manifest.webPackage.selfContained = $false
    $manifest.webPackage.readyToRun = $false
    Assert-Manifest -ExpectedError 'Web package identity is incomplete or invalid'
    $manifest.webPackage.readyToRun = $true
    $manifest.artifacts = @($manifest.artifacts) + @(@{ name = 'web-image.tar.gz'; sizeBytes = 1; sha256 = 'c' * 64 })
    Assert-Manifest -ExpectedError 'exactly the Web ZIP, Worker ZIP, and migration bundle'
    $manifest.artifacts = @($manifest.artifacts | Select-Object -First 3)
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

# Local live-integration settings: the parser, the host split and the flag
# derivation, without a run, a vendor or a secret.
$settingsRoot = Join-Path ([IO.Path]::GetTempPath()) ("pegasus-local-settings-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $settingsRoot | Out-Null
try {
    function Write-SettingsFixture {
        param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][AllowEmptyString()][string[]]$Lines, [switch]$Shared)
        $path = Join-Path $settingsRoot $Name
        [IO.File]::WriteAllText($path, (($Lines -join "`n") + "`n"))
        if ($IsLinux) {
            $mode = [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite
            if ($Shared) { $mode = $mode -bor [IO.UnixFileMode]::GroupRead }
            [IO.File]::SetUnixFileMode($path, $mode)
        }
        return $path
    }
    function Assert-SettingsRejected {
        param([Parameter(Mandatory)][string]$Case, [Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$ExpectedError)
        $failure = $null
        try { Read-PegasusLocalSettingsFile -Path $Path | Out-Null }
        catch { $failure = $_.Exception.Message }
        if (-not $failure -or -not $failure.Contains($ExpectedError)) {
            throw "$Case expected rejection '$ExpectedError'; got '$failure'."
        }
    }

    $absent = Read-PegasusLocalSettingsFile -Path (Join-Path $settingsRoot 'absent.env')
    if ($absent.Count -ne 0) { throw 'An absent settings file must read as empty.' }
    $emptyFlags = Get-PegasusLiveIntegrationFlags -Settings $absent
    if ((Format-PegasusLiveIntegrationFlags -Flags $emptyFlags) -ne 'none' -or @($emptyFlags.Values | Where-Object { $_ }).Count -ne 0) {
        throw 'No settings must derive no live integration flags.'
    }

    $good = Write-SettingsFixture -Name 'good.env' -Lines @(
        '# comment line',
        '',
        'Features__LiveVehicleLookup=true',
        'Features__LiveBoxCustody=TRUE',
        'Features__LiveGlass=false',
        'Box__ConfigJson={"boxAppSettings":{"clientID":"a=b"},"enterpriseID":"1"}',
        'Box__RootFolderId=425169015650',
        'Dvla__ApiKey=key=with=equals',
        'Glass__RepairProfileId=4063',
        'DevelopmentOffline__AdministratorPassword=p#ss=word',
        'GitHub__ProblemReports__Token=')
    $settings = Read-PegasusLocalSettingsFile -Path $good
    if ($settings.Count -ne 9) { throw "Expected 9 parsed settings; got $($settings.Count)." }
    if ($settings['Box__ConfigJson'] -cne '{"boxAppSettings":{"clientID":"a=b"},"enterpriseID":"1"}' -or
        $settings['Dvla__ApiKey'] -cne 'key=with=equals') {
        throw 'The first = must split a settings line and the rest is the verbatim value.'
    }
    $flags = Get-PegasusLiveIntegrationFlags -Settings $settings
    if (-not $flags.vehicleLookup -or -not $flags.boxCustody -or $flags.glass -or $flags.passwordSignIn -or
        $flags.automationMcp -or $flags.principalApi -or $flags.problemReports) {
        throw 'Flag derivation does not follow the Features__ names (case-insensitive true, token presence).'
    }
    if ((Format-PegasusLiveIntegrationFlags -Flags $flags) -cne 'vehicleLookup,boxCustody') {
        throw "Unexpected flag summary '$(Format-PegasusLiveIntegrationFlags -Flags $flags)'."
    }
    if ((Format-PegasusLiveIntegrationFlags -Flags ([pscustomobject]$flags)) -cne 'vehicleLookup,boxCustody') {
        throw 'The flag summary must read a manifest-shaped (PSCustomObject) flag set too.'
    }
    $web = Split-PegasusLocalSettings -Settings $settings -HostKind Web
    $worker = Split-PegasusLocalSettings -Settings $settings -HostKind Worker
    if ($web.Contains('Dvla__ApiKey') -or -not $web.Contains('Glass__RepairProfileId') -or
        -not $web.Contains('DevelopmentOffline__AdministratorPassword') -or -not $web.Contains('Box__ConfigJson')) {
        throw 'The Web split must carry Features, Box, Glass, GitHub, DevelopmentOffline and AutomationMcp keys only.'
    }
    if ($worker.Contains('Glass__RepairProfileId') -or $worker.Contains('DevelopmentOffline__AdministratorPassword') -or
        -not $worker.Contains('Dvla__ApiKey') -or -not $worker.Contains('Box__ConfigJson') -or -not $worker.Contains('Features__LiveVehicleLookup')) {
        throw 'The Worker split must carry Features, Box, Dvla and Dvsa keys only.'
    }

    Assert-SettingsRejected -Case 'unknown prefix' -ExpectedError 'not a recognised Web or Worker setting prefix' -Path (
        Write-SettingsFixture -Name 'unknown.env' -Lines @('Eva__ClientId=x'))
    Assert-SettingsRejected -Case 'reserved key' -ExpectedError 'owned by the run lifecycle' -Path (
        Write-SettingsFixture -Name 'reserved.env' -Lines @('Glass__CallbackBaseUri=https://example.test/'))
    Assert-SettingsRejected -Case 'reserved local custody key' -ExpectedError 'owned by the run lifecycle' -Path (
        Write-SettingsFixture -Name 'reserved2.env' -Lines @('Features__LocalDocumentCustody=true'))
    Assert-SettingsRejected -Case 'duplicate key' -ExpectedError 'set more than once' -Path (
        Write-SettingsFixture -Name 'dup.env' -Lines @('Features__LiveGlass=true', 'Features__LiveGlass=false'))
    Assert-SettingsRejected -Case 'not KEY=VALUE' -ExpectedError 'is not KEY=VALUE' -Path (
        Write-SettingsFixture -Name 'bare.env' -Lines @('Features__LiveGlass'))
    Assert-SettingsRejected -Case 'invalid key' -ExpectedError 'has an invalid key' -Path (
        Write-SettingsFixture -Name 'badkey.env' -Lines @('Features:LiveGlass=true'))
    if ($IsLinux) {
        Assert-SettingsRejected -Case 'group-readable file' -ExpectedError 'readable only by its owner' -Path (
            Write-SettingsFixture -Name 'shared.env' -Lines @('Features__LiveGlass=true') -Shared)
    }
    Write-Output 'Local live-integration settings contract passed.'
}
finally {
    Remove-Item -LiteralPath $settingsRoot -Recurse -Force
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
