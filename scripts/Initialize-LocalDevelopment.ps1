[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$localDevelopmentRoot = Join-Path $repositoryRoot 'artifacts/local-development'
$initializationPath = Join-Path $localDevelopmentRoot '.initialized.json'
$solutionPath = Join-Path $repositoryRoot 'Pegasus.slnx'
$playwrightPath = Join-Path $repositoryRoot 'tests/Pegasus.IntegrationTests/bin/Debug/net10.0/playwright.ps1'

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

if (-not $IsWindows) {
    throw 'Pegasus local development initialization is supported only on Windows 11.'
}

$git = Get-RequiredApplication `
    -Name 'git' `
    -Repair 'winget install --exact --id Git.Git --scope user'

$dotnet = Get-RequiredApplication `
    -Name 'dotnet' `
    -Repair 'winget install --exact --id Microsoft.DotNet.SDK.10 --version 10.0.302 --scope user'
$npm = Get-RequiredApplication `
    -Name 'npm' `
    -Repair 'winget install --exact --id OpenJS.NodeJS --version 24.0.0 --scope user'
$localDb = Get-RequiredApplication `
    -Name 'sqllocaldb' `
    -Repair 'winget install --exact --id Microsoft.SQLServer.2022.Express --override "/ACTION=Install /QUIET /IACCEPTSQLSERVERLICENSETERMS /FEATURES=LocalDB"'
Get-RequiredApplication `
    -Name 'func' `
    -Repair 'winget install --exact --id Microsoft.Azure.FunctionsCoreTools --version 4.12.1 --scope user' |
    Out-Null
$sourceRevision = (& $git -C $repositoryRoot rev-parse --verify HEAD 2>$null | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceRevision -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'The exact 40-character source revision could not be read from the local checkout.'
}
$sourceRevision = $sourceRevision.ToLowerInvariant()


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
    Invoke-RequiredCommand `
        -Command $dotnet `
        -Arguments @(
            'build',
            $solutionPath,
            '--configuration',
            'Debug',
            '--no-restore',
            "/p:SourceRevisionId=$sourceRevision"
        ) `
        -Description 'Deterministic local application build'

    if (-not [System.IO.File]::Exists($playwrightPath)) {
        throw "The package-pinned Playwright command was not generated: $playwrightPath"
    }
    Invoke-RequiredCommand `
        -Command $playwrightPath `
        -Arguments @('install', 'chromium') `
        -Description 'Pinned Playwright Chromium installation'

    & $dotnet dev-certs https --check --trust | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Invoke-RequiredCommand `
            -Command $dotnet `
            -Arguments @('dev-certs', 'https', '--trust') `
            -Description 'Development HTTPS certificate trust'
    }

    & $localDb info 'MSSQLLocalDB' *> $null
    if ($LASTEXITCODE -ne 0) {
        Invoke-RequiredCommand `
            -Command $localDb `
            -Arguments @('create', 'MSSQLLocalDB') `
            -Description 'Default LocalDB instance creation'
    }
    Invoke-RequiredCommand `
        -Command $localDb `
        -Arguments @('start', 'MSSQLLocalDB') `
        -Description 'Default LocalDB instance start'

    & (Join-Path $PSScriptRoot 'Invoke-Doctor.ps1') -Profile Offline

    [System.IO.Directory]::CreateDirectory($localDevelopmentRoot) | Out-Null
    $packageLockPath = Join-Path $repositoryRoot 'package-lock.json'
    $packageLockSha256 = Convert.ToHexString(
        [System.Security.Cryptography.SHA256]::HashData(
            [System.IO.File]::ReadAllBytes($packageLockPath)))
    Write-AtomicJson -Path $initializationPath -Value ([pscustomobject][ordered]@{
        schemaVersion = 1
        kind = 'Pegasus.LocalDevelopment.Initialization'
        profile = 'Offline'
        sdkVersion = '10.0.302'
        azuriteVersion = '3.36.0'
        functionsCoreToolsVersion = '4.12.1'
        packageLockSha256 = $packageLockSha256
        sourceSha = $sourceRevision
    })
}
finally {
    Pop-Location
}

Write-Host "Pegasus Offline local development is initialized at $localDevelopmentRoot."
