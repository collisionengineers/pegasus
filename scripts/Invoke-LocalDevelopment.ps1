[CmdletBinding()]
param(
    [ValidateSet('Migrate', 'Run')]
    [string]$Action = 'Run',
    [ValidatePattern('^[a-z0-9][a-z0-9-]{0,63}$')]
    [string]$RunId = 'qdos-alpha-local',
    [string]$ApprovalPath,
    [switch]$ActivateQdosAlpha
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$webProject = Join-Path $repositoryRoot 'src/Pegasus.Web/Pegasus.Web.csproj'

if (-not [System.IO.File]::Exists($webProject)) {
    throw "The local web project is missing: $webProject"
}

$manifestPath = & (Join-Path $PSScriptRoot 'Initialize-LocalDevelopment.ps1') -RunId $RunId -ApprovalPath $ApprovalPath
$manifestPath = [string]($manifestPath | Select-Object -Last 1)
try {
    $manifest = [System.IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
}
catch {
    throw "The local development manifest is invalid: $manifestPath"
}

if ($manifest.schemaVersion -ne 1 -or
    $manifest.kind -ne 'Pegasus.LocalDevelopment.Run' -or
    $manifest.runId -ne $RunId -or
    $manifest.runtime.profile -ne 'DevelopmentOffline' -or
    $manifest.runtime.environment -ne 'Development' -or
    $manifest.database.provider -ne 'Sqlite' -or
    $manifest.replay.mode -ne 'deterministic-offline' -or
    $manifest.replay.cloudOperations -ne 'disabled' -or
    $manifest.replay.workerStarts -ne $false) {
    throw "The local development manifest violates the offline replay contract: $manifestPath"
}

if ($ActivateQdosAlpha) {
    & (Join-Path $PSScriptRoot 'Invoke-Doctor.ps1') -ApprovalPath $ApprovalPath -RequireQdosAlphaActivation | Out-Null
    if (-not $manifest.alphaActivation.activationAllowed) {
        throw "QDOS alpha activation is blocked by $($manifest.alphaActivation.gate)."
    }
}

$environment = [ordered]@{
    ASPNETCORE_ENVIRONMENT = $manifest.runtime.environment
    Runtime__Profile = $manifest.runtime.profile
    Database__Provider = $manifest.database.provider
    Database__LocalPath = $manifest.database.localPath
    Intake__LocalArtifactPath = $manifest.intake.localArtifactPath
    Features__LocalIntake = 'true'
}
$previousEnvironment = @{}
foreach ($name in $environment.Keys) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, [string]$environment[$name], 'Process')
}

try {
    & dotnet run --no-launch-profile --project $webProject -- --migrate-development
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if ($Action -eq 'Migrate') {
        return
    }

    & dotnet run --project $webProject --launch-profile $manifest.runtime.webLaunchProfile
    exit $LASTEXITCODE
}
finally {
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
}
