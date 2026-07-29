[CmdletBinding()]
param(
    [ValidateSet('CiPressure', 'OfflineCandidate')]
    [string]$Profile = 'CiPressure',

    [Parameter(Mandatory)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$SourceRevision,

    [ValidatePattern('^[a-f0-9]{32}$')]
    [string]$RunId = [Guid]::NewGuid().ToString('N'),

    [string]$CapacityDatasetManifest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$integrationProject = Join-Path $repositoryRoot 'tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj'
$pressureSourceRoot = Join-Path $repositoryRoot 'tests/Pegasus.PerformanceTests'
$stagingRoot = Join-Path $repositoryRoot 'tests/Pegasus.IntegrationTests/QdosPressure.Generated'
$evidenceRoot = Join-Path $repositoryRoot "artifacts/qdos-alpha-acceptance/$RunId"
$evidencePath = Join-Path $evidenceRoot 'evidence.json'
$resultsRoot = Join-Path $evidenceRoot 'test-results'
$startedUtc = [DateTimeOffset]::UtcNow

function Assert-OfflineCandidatePrerequisites {
    if ([string]::IsNullOrWhiteSpace($CapacityDatasetManifest)) {
        throw 'OfflineCandidate is blocked: -CapacityDatasetManifest is required for the operator-approved immutable 2,000-case dataset.'
    }

    $manifestPath = [System.IO.Path]::GetFullPath($CapacityDatasetManifest)
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "OfflineCandidate is blocked: capacity dataset manifest '$manifestPath' does not exist."
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or $manifest.caseCount -ne 2000) {
        throw 'OfflineCandidate is blocked: the capacity manifest must use schemaVersion 1 and declare exactly 2,000 cases.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$manifest.approvalReference)) {
        throw 'OfflineCandidate is blocked: the capacity manifest has no operator approval reference.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$manifest.datasetPath) -or
        [string]$manifest.datasetSha256 -notmatch '^[a-fA-F0-9]{64}$') {
        throw 'OfflineCandidate is blocked: the capacity manifest requires a dataset path and SHA-256.'
    }

    $datasetPath = if ([System.IO.Path]::IsPathRooted([string]$manifest.datasetPath)) {
        [System.IO.Path]::GetFullPath([string]$manifest.datasetPath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path (Split-Path $manifestPath -Parent) ([string]$manifest.datasetPath)))
    }
    if (-not (Test-Path -LiteralPath $datasetPath -PathType Leaf)) {
        throw "OfflineCandidate is blocked: approved dataset '$datasetPath' does not exist."
    }

    $observedHash = (Get-FileHash -LiteralPath $datasetPath -Algorithm SHA256).Hash
    if ($observedHash -ne [string]$manifest.datasetSha256) {
        throw 'OfflineCandidate is blocked: approved dataset SHA-256 does not match its manifest.'
    }

    throw 'OfflineCandidate is blocked: the Core QdosAlphaAcceptanceGate and complete 128-capability caller observation map are not implemented. CI pressure verification cannot be promoted to alpha acceptance.'
}

[System.IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
$previousProfile = [Environment]::GetEnvironmentVariable('PEGASUS_QDOS_PRESSURE_PROFILE', 'Process')
$failure = $null
$result = 'failed'
$testResultHash = $null

try {
    if ($Profile -eq 'OfflineCandidate') {
        Assert-OfflineCandidatePrerequisites
    }

    if (-not (Test-Path -LiteralPath $integrationProject -PathType Leaf)) {
        throw "Integration test project '$integrationProject' does not exist."
    }

    $sources = @(
        Join-Path $pressureSourceRoot 'CapacitySoakTests.cs'
        Join-Path $pressureSourceRoot 'FailureInjectionTests.cs'
    )
    foreach ($source in $sources) {
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Required pressure source '$source' does not exist."
        }
    }

    if (Test-Path -LiteralPath $stagingRoot) {
        throw "Refusing to replace unexpected pressure staging directory '$stagingRoot'."
    }

    [System.IO.Directory]::CreateDirectory($stagingRoot) | Out-Null
    foreach ($source in $sources) {
        Copy-Item -LiteralPath $source -Destination $stagingRoot
    }
    [System.IO.Directory]::CreateDirectory($resultsRoot) | Out-Null
    Set-Item -Path 'Env:PEGASUS_QDOS_PRESSURE_PROFILE' -Value 'CiPressure'

    & dotnet test $integrationProject --configuration Release --filter 'Category=QdosPressure' --results-directory $resultsRoot --logger 'trx;LogFileName=qdos-pressure.trx'
    if ($LASTEXITCODE -ne 0) {
        throw "QDOS caller pressure tests failed with exit code $LASTEXITCODE."
    }

    $trxPath = Join-Path $resultsRoot 'qdos-pressure.trx'
    if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
        throw 'QDOS caller pressure tests completed without the required TRX evidence.'
    }

    $testResultHash = (Get-FileHash -LiteralPath $trxPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $result = 'ci-pressure-verified'
}
catch {
    $failure = $_.Exception.Message
}
finally {
    if ($null -eq $previousProfile) {
        Remove-Item -Path 'Env:PEGASUS_QDOS_PRESSURE_PROFILE' -ErrorAction SilentlyContinue
    }
    else {
        Set-Item -Path 'Env:PEGASUS_QDOS_PRESSURE_PROFILE' -Value $previousProfile
    }

    if (Test-Path -LiteralPath $stagingRoot -PathType Container) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }

    $sourceHashes = [ordered]@{}
    foreach ($name in @('CapacitySoakTests.cs', 'FailureInjectionTests.cs')) {
        $path = Join-Path $pressureSourceRoot $name
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $sourceHashes[$name] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }

    $evidence = [ordered]@{
        schemaVersion = 1
        runId = $RunId
        profile = $Profile
        evidenceState = $result
        sourceRevision = $SourceRevision.ToLowerInvariant()
        startedUtc = $startedUtc.ToString('O')
        completedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        testResultSha256 = $testResultHash
        pressureSourceSha256 = $sourceHashes
        failure = $failure
        limitation = if ($Profile -eq 'CiPressure') {
            'Deterministic in-process Web caller pressure only. This is not the approved 30-minute dataset soak, Worker/Azurite pressure, deployment, live verification, or operator/management acceptance.'
        }
        else {
            'OfflineCandidate remains fail-closed until the Core acceptance gate, complete caller observations, and approved immutable capacity evidence exist.'
        }
    }
    $evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $evidencePath -Encoding utf8NoBOM
}

$evidenceHash = (Get-FileHash -LiteralPath $evidencePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($null -ne $failure) {
    throw "$failure Evidence: $evidencePath (sha256:$evidenceHash)"
}

Write-Output "QDOS CI pressure verification passed for run '$RunId'."
Write-Output "Evidence: $evidencePath"
Write-Output "Evidence SHA-256: $evidenceHash"
Write-Output 'This result is CI pressure verification only; it is not OfflineCandidate or release acceptance.'
