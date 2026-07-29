[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$doctorScript = Join-Path $repositoryRoot 'scripts/Invoke-Doctor.ps1'
$playwrightScript = Join-Path $HOME '.nuget/packages/microsoft.playwright/1.61.0/tools/net8.0/any/playwright.ps1'

function Invoke-RequiredCommand {
    param(
        [Parameter(Mandatory)] [string]$Description,
        [Parameter(Mandatory)] [scriptblock]$Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    Invoke-RequiredCommand -Description 'dotnet tool restore' -Command { dotnet tool restore }
    Invoke-RequiredCommand -Description 'locked .NET restore' -Command { dotnet restore ./Pegasus.slnx --locked-mode }
    Invoke-RequiredCommand -Description 'npm ci' -Command { npm ci }

    if (-not (Test-Path -LiteralPath $playwrightScript -PathType Leaf)) {
        throw "Microsoft.Playwright 1.61.0 was not restored at '$playwrightScript'. Restore must retain the committed Playwright package before browser installation."
    }
    Invoke-RequiredCommand -Description 'pinned Playwright browser installation' -Command { & $playwrightScript install }

    & $doctorScript -Profile Offline
    if ($LASTEXITCODE -ne 0) {
        throw "Offline doctor failed with exit code $LASTEXITCODE. Apply only its printed repair commands and rerun initialization."
    }

    Invoke-RequiredCommand -Description 'LocalDB startup' -Command { sqllocaldb start MSSQLLocalDB }

    $localDevelopmentRoot = Join-Path $repositoryRoot 'artifacts/local-development'
    [System.IO.Directory]::CreateDirectory($localDevelopmentRoot) | Out-Null
    Write-Output "Local development prerequisites are initialized. Run state will be created under '$localDevelopmentRoot'."
}
finally {
    Pop-Location
}
