[CmdletBinding()]
param(
    [ValidateSet('CiPressure', 'OfflineCandidate')]
    [string]$Profile = 'CiPressure',

    [Parameter(Mandatory)]
    [ValidatePattern('^[a-fA-F0-9]{40}$')]
    [string]$SourceRevision,

    [ValidatePattern('^[a-f0-9]{32}$')]
    [string]$RunId = [Guid]::NewGuid().ToString('N'),

    [string]$CapacityDatasetManifest,

    [string]$CallerEvidenceManifest,

    [string]$LocalRunManifest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$integrationProject = Join-Path $repositoryRoot 'tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj'
$acceptanceSourceRoot = Split-Path $integrationProject -Parent
$pressureSourceRoot = Join-Path $repositoryRoot 'tests/Pegasus.PerformanceTests'
$stagingRoot = Join-Path $repositoryRoot 'tests/Pegasus.IntegrationTests/QdosPressure.Generated'
$evidenceRoot = Join-Path $repositoryRoot "artifacts/qdos-alpha-acceptance/$RunId"
$evidencePath = Join-Path $evidenceRoot 'evidence.json'
$evidenceTempPath = Join-Path $evidenceRoot 'evidence.json.tmp'
$resultsRoot = Join-Path $evidenceRoot 'test-results'
$startedUtc = [DateTimeOffset]::UtcNow

function Assert-OfflineCandidatePrerequisites {
    if ([string]::IsNullOrWhiteSpace($CapacityDatasetManifest)) {
        throw 'OfflineCandidate is blocked: -CapacityDatasetManifest is required for the operator-approved immutable 2,000-case dataset.'
    }

    if ([string]::IsNullOrWhiteSpace($CallerEvidenceManifest)) {
        throw 'OfflineCandidate is blocked: -CallerEvidenceManifest is required for the complete QDOS-owned capability observation map.'
    }

    if ([string]::IsNullOrWhiteSpace($LocalRunManifest)) {
        throw 'OfflineCandidate is blocked: -LocalRunManifest is required from the run-scoped DevelopmentOffline initialization.'
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

    $observedHash = (Get-FileHash -LiteralPath $datasetPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($observedHash -ne ([string]$manifest.datasetSha256).ToLowerInvariant()) {
        throw 'OfflineCandidate is blocked: approved dataset SHA-256 does not match its manifest.'
    }

    $callerManifestPath = [System.IO.Path]::GetFullPath($CallerEvidenceManifest)
    if (-not (Test-Path -LiteralPath $callerManifestPath -PathType Leaf)) {
        throw "OfflineCandidate is blocked: caller evidence manifest '$callerManifestPath' does not exist."
    }

    try {
        $callerManifest = Get-Content -LiteralPath $callerManifestPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "OfflineCandidate is blocked: caller evidence manifest '$callerManifestPath' is not valid JSON."
    }
    if ($callerManifest.schemaVersion -ne 1 -or
        [string]$callerManifest.kind -ne 'Pegasus.QdosAlpha.AcceptanceEvidence') {
        throw "OfflineCandidate is blocked: caller evidence manifest must use schemaVersion 1 and kind 'Pegasus.QdosAlpha.AcceptanceEvidence'."
    }

    if ([string]$callerManifest.sourceRevision -ne $SourceRevision.ToLowerInvariant() -or
        [string]$callerManifest.runId -ne $RunId) {
        throw 'OfflineCandidate is blocked: caller evidence manifest sourceRevision and runId must identify this exact acceptance invocation.'
    }

    $localRunManifestPath = [System.IO.Path]::GetFullPath($LocalRunManifest)
    if (-not (Test-Path -LiteralPath $localRunManifestPath -PathType Leaf)) {
        throw "OfflineCandidate is blocked: local run manifest '$localRunManifestPath' does not exist."
    }
    try {
        $localRun = Get-Content -LiteralPath $localRunManifestPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "OfflineCandidate is blocked: local run manifest '$localRunManifestPath' is not valid JSON."
    }
    if ($localRun.schemaVersion -ne 1 -or
        [string]$localRun.kind -ne 'Pegasus.LocalDevelopment.Run' -or
        [string]$localRun.runId -ne $RunId -or
        [string]$localRun.runtime.profile -ne 'DevelopmentOffline' -or
        [string]$localRun.runtime.environment -ne 'Development' -or
        [string]$localRun.replay.mode -ne 'deterministic-offline' -or
        [string]$localRun.replay.cloudOperations -ne 'disabled' -or
        $localRun.replay.workerStarts -ne $false) {
        throw 'OfflineCandidate is blocked: the local run manifest violates the run-scoped deterministic-offline contract.'
    }

    return [pscustomobject]@{
        CapacityManifestPath = $manifestPath
        CapacityManifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
        CallerManifestPath = $callerManifestPath
        CallerManifestSha256 = (Get-FileHash -LiteralPath $callerManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
        LocalRunManifestPath = $localRunManifestPath
        LocalRunManifestSha256 = (Get-FileHash -LiteralPath $localRunManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

if (Test-Path -LiteralPath $evidenceRoot) {
    throw "Refusing to overwrite the immutable acceptance run '$RunId' at '$evidenceRoot'. Use a new RunId."
}
[System.IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
$previousProfile = [Environment]::GetEnvironmentVariable('PEGASUS_QDOS_PRESSURE_PROFILE', 'Process')
$previousAcceptanceManifest = [Environment]::GetEnvironmentVariable('PEGASUS_QDOS_ACCEPTANCE_MANIFEST', 'Process')
$previousAcceptanceRevision = [Environment]::GetEnvironmentVariable('PEGASUS_QDOS_ACCEPTANCE_SOURCE_REVISION', 'Process')
$failure = $null
$result = 'failed'
$testResultHash = $null
$acceptanceResultHash = $null
$offlinePrerequisites = $null
$stagingCreated = $false

try {
    if ($Profile -eq 'OfflineCandidate') {
        $offlinePrerequisites = Assert-OfflineCandidatePrerequisites
    }

    if (-not (Test-Path -LiteralPath $integrationProject -PathType Leaf)) {
        throw "Integration test project '$integrationProject' does not exist."
    }

    [System.IO.Directory]::CreateDirectory($resultsRoot) | Out-Null
    if ($Profile -eq 'OfflineCandidate') {
        Set-Item -Path 'Env:PEGASUS_QDOS_ACCEPTANCE_MANIFEST' -Value $offlinePrerequisites.CallerManifestPath
        Set-Item -Path 'Env:PEGASUS_QDOS_ACCEPTANCE_SOURCE_REVISION' -Value $SourceRevision.ToLowerInvariant()
        & dotnet test $integrationProject --configuration Release --filter 'Category=QdosAlphaAcceptance' --results-directory $resultsRoot --logger 'trx;LogFileName=qdos-alpha-acceptance.trx'
        if ($LASTEXITCODE -ne 0) {
            throw "QDOS Core acceptance gate failed with exit code $LASTEXITCODE."
        }

        $acceptanceTrxPath = Join-Path $resultsRoot 'qdos-alpha-acceptance.trx'
        if (-not (Test-Path -LiteralPath $acceptanceTrxPath -PathType Leaf)) {
            throw 'QDOS Core acceptance gate completed without the required TRX evidence.'
        }
        $acceptanceResultHash = (Get-FileHash -LiteralPath $acceptanceTrxPath -Algorithm SHA256).Hash.ToLowerInvariant()
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
    $stagingCreated = $true
    foreach ($source in $sources) {
        Copy-Item -LiteralPath $source -Destination $stagingRoot
    }
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
    $result = if ($Profile -eq 'OfflineCandidate') {
        'offline-candidate-verified'
    }
    else {
        'ci-pressure-verified'
    }
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

    if ($null -eq $previousAcceptanceManifest) {
        Remove-Item -Path 'Env:PEGASUS_QDOS_ACCEPTANCE_MANIFEST' -ErrorAction SilentlyContinue
    }
    else {
        Set-Item -Path 'Env:PEGASUS_QDOS_ACCEPTANCE_MANIFEST' -Value $previousAcceptanceManifest
    }
    if ($null -eq $previousAcceptanceRevision) {
        Remove-Item -Path 'Env:PEGASUS_QDOS_ACCEPTANCE_SOURCE_REVISION' -ErrorAction SilentlyContinue
    }
    else {
        Set-Item -Path 'Env:PEGASUS_QDOS_ACCEPTANCE_SOURCE_REVISION' -Value $previousAcceptanceRevision
    }

    if ($stagingCreated -and (Test-Path -LiteralPath $stagingRoot -PathType Container)) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }

    $sourceHashes = [ordered]@{}
    foreach ($name in @('CapacitySoakTests.cs', 'FailureInjectionTests.cs')) {
        $path = Join-Path $pressureSourceRoot $name
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $sourceHashes[$name] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }

    $offlineMatrixSourceHashes = [ordered]@{}
    foreach ($name in @(
        'OfflineAcceptanceTests.cs',
        'LocalServiceSmokeTests.cs',
        'NegativeMatrixTests.cs',
        'RecoveryTests.cs')) {
        $path = Join-Path $acceptanceSourceRoot $name
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $offlineMatrixSourceHashes[$name] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
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
        offlineMatrixSourceSha256 = $offlineMatrixSourceHashes
        acceptanceTestResultSha256 = $acceptanceResultHash
        capacityDatasetManifestSha256 = if ($null -ne $offlinePrerequisites) {
            $offlinePrerequisites.CapacityManifestSha256
        }
        else {
            $null
        }
        callerEvidenceManifestSha256 = if ($null -ne $offlinePrerequisites) {
            $offlinePrerequisites.CallerManifestSha256
        }
        else {
            $null
        }
        localRunManifestSha256 = if ($null -ne $offlinePrerequisites) {
            $offlinePrerequisites.LocalRunManifestSha256
        }
        else {
            $null
        }
        failure = $failure
        limitation = if ($Profile -eq 'CiPressure') {
            'Deterministic in-process Web caller pressure only. This is not the approved 30-minute dataset soak, Worker/Azurite pressure, deployment, live verification, or operator/management acceptance.'
        }
        elseif ($result -eq 'offline-candidate-verified') {
            'The Core gate verified the QDOS-owned offline caller map, local caller/recovery/negative matrix, run-scoped deterministic-offline manifest, and approved immutable capacity evidence. Live adapter scopes, Azure deployment and recovery, exact-head review, QDOS operator acceptance, Collision Engineers management approval, release, and deployment remain separate fail-closed gates.'
        }
        else {
            'OfflineCandidate was not verified. Missing or invalid caller, capacity, approval, or evidence input remains fail closed.'
        }
    }
    $evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $evidenceTempPath -Encoding utf8NoBOM
    Move-Item -LiteralPath $evidenceTempPath -Destination $evidencePath
}

$evidenceHash = (Get-FileHash -LiteralPath $evidencePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($null -ne $failure) {
    throw "$failure Evidence: $evidencePath (sha256:$evidenceHash)"
}

if ($Profile -eq 'OfflineCandidate') {
    Write-Output "QDOS offline candidate verification passed for run '$RunId'."
}
else {
    Write-Output "QDOS CI pressure verification passed for run '$RunId'."
}
Write-Output "Evidence: $evidencePath"
Write-Output "Evidence SHA-256: $evidenceHash"
Write-Output 'This result is not release acceptance, deployment evidence, live verification, QDOS operator acceptance, or Collision Engineers management approval.'
