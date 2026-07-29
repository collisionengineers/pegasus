[CmdletBinding()]
param(
    [ValidatePattern('^[a-z0-9][a-z0-9-]{0,63}$')]
    [string]$RunId = 'qdos-alpha-local',
    [string]$ApprovalPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runRoot = Join-Path $repositoryRoot (Join-Path 'artifacts/local-development' $RunId)
$manifestPath = Join-Path $runRoot 'run-manifest.json'

function ConvertTo-DeterministicJson {
    param([Parameter(Mandatory)][object]$Value)

    $json = $Value | ConvertTo-Json -Depth 20
    return (($json -replace "`r`n?", "`n").TrimEnd([char[]]@("`n")) + "`n")
}

function Write-DeterministicJson {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][object]$Value
    )

    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    [System.IO.File]::WriteAllText(
        $Path,
        (ConvertTo-DeterministicJson -Value $Value),
        [System.Text.UTF8Encoding]::new($false))
}

$doctor = & (Join-Path $PSScriptRoot 'Invoke-Doctor.ps1') -ApprovalPath $ApprovalPath

$databaseDirectory = Join-Path $runRoot 'state'
$artifactDirectory = Join-Path $runRoot 'intake'
[System.IO.Directory]::CreateDirectory($databaseDirectory) | Out-Null
[System.IO.Directory]::CreateDirectory($artifactDirectory) | Out-Null

$webLocalDatabasePath = "../../artifacts/local-development/$RunId/state/pegasus.db"
$webLocalArtifactPath = "../../artifacts/local-development/$RunId/intake"
$manifest = [pscustomobject][ordered]@{
    schemaVersion = 1
    kind = 'Pegasus.LocalDevelopment.Run'
    runId = $RunId
    runtime = [ordered]@{
        profile = 'DevelopmentOffline'
        environment = 'Development'
        webLaunchProfile = 'http'
        url = 'http://localhost:5233'
    }
    database = [ordered]@{
        provider = 'Sqlite'
        localPath = $webLocalDatabasePath
    }
    intake = [ordered]@{
        localArtifactPath = $webLocalArtifactPath
    }
    replay = [ordered]@{
        mode = 'deterministic-offline'
        cloudOperations = 'disabled'
        workerStarts = $false
    }
    alphaActivation = $doctor.alphaActivation
}

Write-DeterministicJson -Path $manifestPath -Value $manifest
$manifestPath
