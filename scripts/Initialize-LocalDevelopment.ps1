[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$localDevelopmentRoot = Join-Path $repositoryRoot 'artifacts/local-development'
$initializationPath = Join-Path $localDevelopmentRoot '.initialized.json'
$solutionPath = Join-Path $repositoryRoot 'Pegasus.slnx'
$webAssemblyRelativePath = 'src/Pegasus.Web/bin/Debug/net10.0/Pegasus.Web.dll'
$workerAssemblyRelativePath = 'src/Pegasus.Worker/bin/Debug/net10.0/Pegasus.Worker.dll'
$playwrightPath = Join-Path $repositoryRoot 'tests/Pegasus.IntegrationTests/bin/Debug/net10.0/playwright.ps1'

. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')

function Get-RequiredApplication {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [string]$Repair
    )

    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $command) {
        throw "$Name is required. Repair: $Repair"
    }

    return [string]$command.Source
}

function Invoke-RequiredCommand {
    param(
        [Parameter(Mandatory)]
        [string]$Command,
        [string[]]$Arguments = @(),
        [Parameter(Mandatory)]
        [string]$Description
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function ConvertTo-DeterministicJson {
    param([Parameter(Mandatory)][object]$Value)

    $json = $Value | ConvertTo-Json -Depth 10
    return (($json -replace "`r`n?", "`n").TrimEnd([char[]]@("`n")) + "`n")
}

function Write-AtomicJson {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [object]$Value
    )

    $parent = Split-Path -Parent $Path
    [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    $temporaryPath = Join-Path $parent (".{0}.{1}.tmp" -f
        [System.IO.Path]::GetFileName($Path),
        [Guid]::NewGuid().ToString('N'))
    try {
        [System.IO.File]::WriteAllText(
            $temporaryPath,
            (ConvertTo-DeterministicJson -Value $Value),
            [System.Text.UTF8Encoding]::new($false))
        [System.IO.File]::Move($temporaryPath, $Path, $true)
    }
    finally {
        if ([System.IO.File]::Exists($temporaryPath)) {
            [System.IO.File]::Delete($temporaryPath)
        }
    }
}
function Get-Sha256 {
    param([Parameter(Mandatory)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Resolve-RepositorySourceRevision {
    param([Parameter(Mandatory)][string]$Git)

    $headOutput = @(
        & $Git -C $repositoryRoot rev-parse --verify 'HEAD^{commit}' 2>$null
    )
    if ($LASTEXITCODE -ne 0 -or $headOutput.Count -ne 1) {
        throw 'The exact 40-character source revision could not be read from the local checkout.'
    }

    $revision = ([string]$headOutput[0]).Trim().ToLowerInvariant()
    if ($revision -notmatch '^[0-9a-f]{40}$') {
        throw "The repository HEAD is not a 40-character Git source revision: '$revision'."
    }

    return $revision
}

function Assert-CleanRepositoryRevision {
    param(
        [Parameter(Mandatory)][string]$Git,
        [Parameter(Mandatory)][string]$ExpectedRevision
    )

    $observedRevision = Resolve-RepositorySourceRevision -Git $Git
    if ($observedRevision -cne $ExpectedRevision) {
        throw "The checked-out source revision changed during local initialization from '$ExpectedRevision' to '$observedRevision'."
    }

    $workingTreeState = @(
        & $Git -C $repositoryRoot status --porcelain=v1 --untracked-files=all -- . 2>$null
    )
    if ($LASTEXITCODE -ne 0) {
        throw "The working-tree state could not be verified at '$repositoryRoot'."
    }
    if ($workingTreeState.Count -ne 0) {
        throw 'Local initialization requires a clean checkout before and after the build. Commit or remove tracked and untracked changes, then retry.'
    }
}

function Get-RuntimeArtifactRecord {
    param([Parameter(Mandatory)][string]$RelativePath)

    $path = Join-Path $repositoryRoot $RelativePath
    if (-not [System.IO.File]::Exists($path)) {
        throw "The local runtime build artifact is missing: $RelativePath"
    }

    $file = [System.IO.FileInfo]::new($path)
    if ($file.Length -le 0) {
        throw "The local runtime build artifact is empty: $RelativePath"
    }

    return [ordered]@{
        relativePath = $RelativePath
        byteLength = $file.Length
        sha256 = Get-Sha256 -Path $file.FullName
    }
}


$platform = Get-PegasusPlatform

$git = Get-RequiredApplication `
    -Name 'git' `
    -Repair (Get-PegasusRepairHint -Id 'git')

$dotnet = Get-RequiredApplication `
    -Name 'dotnet' `
    -Repair (Get-PegasusRepairHint -Id 'dotnet-sdk')
$npm = Get-RequiredApplication `
    -Name 'npm' `
    -Repair (Get-PegasusRepairHint -Id 'node')
$databaseCommand = Get-RequiredApplication `
    -Name (Get-PegasusDatabaseCommandName) `
    -Repair (Get-PegasusRepairHint -Id 'database-engine')
Get-RequiredApplication `
    -Name 'func' `
    -Repair (Get-PegasusRepairHint -Id 'func') |
    Out-Null
$sourceRevision = Resolve-RepositorySourceRevision -Git $git
Assert-CleanRepositoryRevision -Git $git -ExpectedRevision $sourceRevision


Push-Location $repositoryRoot
try {
    Invoke-RequiredCommand `
        -Command $npm `
        -Arguments @('ci', '--prefix', $repositoryRoot) `
        -Description 'Pinned npm restoration'
    Invoke-RequiredCommand `
        -Command $dotnet `
        -Arguments @('tool', 'restore') `
        -Description 'Pinned .NET tool restoration'
    Invoke-RequiredCommand `
        -Command $dotnet `
        -Arguments @('restore', $solutionPath, '--locked-mode') `
        -Description 'Locked .NET package restoration'
    Assert-CleanRepositoryRevision -Git $git -ExpectedRevision $sourceRevision
    Invoke-RequiredCommand `
        -Command $dotnet `
        -Arguments @(
            'build',
            $solutionPath,
            '--configuration',
            'Debug',
            '--no-restore',
            '--no-incremental',
            "/p:SourceRevisionId=$sourceRevision"
        ) `
        -Description 'Deterministic local application build'
    Assert-CleanRepositoryRevision -Git $git -ExpectedRevision $sourceRevision

    if (-not [System.IO.File]::Exists($playwrightPath)) {
        throw "The package-pinned Playwright command was not generated: $playwrightPath"
    }
    Invoke-RequiredCommand `
        -Command $playwrightPath `
        -Arguments @('install', 'chromium') `
        -Description 'Pinned Playwright Chromium installation'

    if ($platform.IsWindows) {
        & $dotnet dev-certs https --check --trust | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Invoke-RequiredCommand `
                -Command $dotnet `
                -Arguments @('dev-certs', 'https', '--trust') `
                -Description 'Development HTTPS certificate trust'
        }
    }
    else {
        # Ensure the certificate exists, which is all Kestrel requires. Trusting
        # it on Linux writes to per-user NSS and OpenSSL stores and can prompt,
        # and initialization is a non-interactive contract, so trust is left to
        # the operator and reported by the doctor as an advisory check.
        & $dotnet dev-certs https --check *> $null
        if ($LASTEXITCODE -ne 0) {
            Invoke-RequiredCommand `
                -Command $dotnet `
                -Arguments @('dev-certs', 'https') `
                -Description 'Development HTTPS certificate creation'
        }
    }

    if ($platform.IsWindows) {
        & $databaseCommand info 'MSSQLLocalDB' *> $null
        if ($LASTEXITCODE -ne 0) {
            Invoke-RequiredCommand `
                -Command $databaseCommand `
                -Arguments @('create', 'MSSQLLocalDB') `
                -Description 'Default LocalDB instance creation'
        }
        Invoke-RequiredCommand `
            -Command $databaseCommand `
            -Arguments @('start', 'MSSQLLocalDB') `
            -Description 'Default LocalDB instance start'
    }
    else {
        # Materialise the pinned image now so that an initialized run's Start
        # path never contacts a registry.
        $databaseImage = Get-PegasusDatabaseImageReference
        & $databaseCommand image inspect $databaseImage *> $null
        if ($LASTEXITCODE -ne 0) {
            Invoke-RequiredCommand `
                -Command $databaseCommand `
                -Arguments @('pull', $databaseImage) `
                -Description 'Pinned SQL Server image acquisition'
        }
    }

    & (Join-Path $PSScriptRoot 'Invoke-Doctor.ps1') -Profile Offline

    Assert-CleanRepositoryRevision -Git $git -ExpectedRevision $sourceRevision
    $runtimeArtifacts = [ordered]@{
        web = Get-RuntimeArtifactRecord -RelativePath $webAssemblyRelativePath
        worker = Get-RuntimeArtifactRecord -RelativePath $workerAssemblyRelativePath
    }
    Assert-CleanRepositoryRevision -Git $git -ExpectedRevision $sourceRevision

    [System.IO.Directory]::CreateDirectory($localDevelopmentRoot) | Out-Null
    $packageLockPath = Join-Path $repositoryRoot 'package-lock.json'
    $packageLockSha256 = Get-Sha256 -Path $packageLockPath
    Write-AtomicJson -Path $initializationPath -Value ([pscustomobject][ordered]@{
        schemaVersion = 2
        kind = 'Pegasus.LocalDevelopment.Initialization'
        profile = 'Offline'
        platform = $platform.Kind
        sdkVersion = '10.0.302'
        azuriteVersion = '3.36.0'
        functionsCoreToolsVersion = '4.12.1'
        databaseEngine = Get-PegasusDatabaseEngineKind
        databaseImage = $(if ($platform.IsWindows) { $null } else { Get-PegasusDatabaseImageReference })
        packageLockSha256 = $packageLockSha256
        sourceSha = $sourceRevision
        runtimeArtifacts = $runtimeArtifacts
    })
}
finally {
    Pop-Location
}

Write-Host "Pegasus Offline local development is initialized at $localDevelopmentRoot."
