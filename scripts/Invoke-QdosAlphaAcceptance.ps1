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
# These two source-only pressure files deliberately have no project. This
# orchestrator stages them into the compiled integration-test project below,
# so their sole build and caller owner remains this revision-bound gate.
$pressureSourceRoot = Join-Path $repositoryRoot 'tests/Pegasus.PerformanceTests'
$stagingRoot = Join-Path $repositoryRoot 'tests/Pegasus.IntegrationTests/QdosPressure.Generated'
$evidenceRoot = Join-Path $repositoryRoot "artifacts/qdos-alpha-acceptance/$RunId"
$evidencePath = Join-Path $evidenceRoot 'evidence.json'
$evidenceTempPath = Join-Path $evidenceRoot 'evidence.json.tmp'
$resultsRoot = Join-Path $evidenceRoot 'test-results'
$startedUtc = [DateTimeOffset]::UtcNow

function Resolve-RepositorySourceRevision {
    param([Parameter(Mandatory)][string]$RequestedRevision)

    $git = Get-Command -Name git -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $git) {
        throw 'Git is required to bind acceptance evidence to the executed checkout.'
    }

    $headOutput = @(& $git.Source -C $repositoryRoot rev-parse --verify 'HEAD^{commit}' 2>$null)
    if ($LASTEXITCODE -ne 0 -or $headOutput.Count -ne 1) {
        throw "The repository HEAD could not be resolved at '$repositoryRoot'."
    }

    $headRevision = ([string]$headOutput[0]).Trim().ToLowerInvariant()
    if ($headRevision -notmatch '^[0-9a-f]{40}$') {
        throw "The repository HEAD is not a 40-character Git source revision: '$headRevision'."
    }

    $requested = $RequestedRevision.ToLowerInvariant()
    $requestedOutput = @(
        & $git.Source -C $repositoryRoot rev-parse --verify "$requested^{commit}" 2>$null
    )
    if ($LASTEXITCODE -ne 0 -or $requestedOutput.Count -ne 1) {
        throw "SourceRevision '$RequestedRevision' does not identify a commit in the executed checkout."
    }

    $resolvedRequested = ([string]$requestedOutput[0]).Trim().ToLowerInvariant()
    if ($resolvedRequested -cne $headRevision) {
        throw "SourceRevision '$RequestedRevision' identifies '$resolvedRequested', but the executed checkout HEAD is '$headRevision'."
    }

    $workingTreeState = @(
        & $git.Source -C $repositoryRoot status --porcelain=v1 --untracked-files=all -- . 2>$null
    )
    if ($LASTEXITCODE -ne 0) {
        throw "The working-tree state could not be verified at '$repositoryRoot'."
    }
    if ($workingTreeState.Count -ne 0) {
        throw "The executed checkout has tracked or untracked changes. Commit or remove them before producing revision-bound acceptance evidence."
    }

    return $headRevision
}
function Assert-JsonProperties {
    param(
        [Parameter(Mandatory)][object]$Value,
        [Parameter(Mandatory)][string[]]$Names,
        [Parameter(Mandatory)][string]$Label
    )

    foreach ($name in $Names) {
        if ($Value.PSObject.Properties.Name -notcontains $name) {
            throw "OfflineCandidate is blocked: $Label is missing '$name'."
        }
    }
}

function ConvertTo-EvidenceTimestamp {
    param(
        [Parameter(Mandatory)][string]$Value,
        [Parameter(Mandatory)][string]$Label
    )

    try {
        return [DateTimeOffset]::ParseExact(
            $Value,
            'O',
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::RoundtripKind)
    }
    catch {
        throw "OfflineCandidate is blocked: $Label must be an exact round-trip timestamp."
    }
}

function Assert-LocalRunEvidence {
    param(
        [Parameter(Mandatory)][object]$LocalRun,
        [Parameter(Mandatory)][string]$ManifestPath,
        [Parameter(Mandatory)][string]$ExpectedSourceRevision
    )

    $expectedManifestPath = [System.IO.Path]::GetFullPath(
        (Join-Path $repositoryRoot "artifacts/local-development/$RunId/run-manifest.json"))
    if (-not $ManifestPath.Equals(
            $expectedManifestPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "OfflineCandidate is blocked: local run manifest must be the exact run-owned manifest '$expectedManifestPath'."
    }

    Assert-JsonProperties `
        -Value $LocalRun `
        -Names @(
            'schemaVersion',
            'kind',
            'runId',
            'state',
            'startAttempt',
            'createdUtc',
            'sourceSha',
            'ownership',
            'runtime',
            'identity',
            'verification') `
        -Label 'local run manifest'
    Assert-JsonProperties `
        -Value $LocalRun.ownership `
        -Names @('repositoryRoot', 'cloudOperations') `
        -Label 'local run ownership'
    Assert-JsonProperties `
        -Value $LocalRun.runtime `
        -Names @('profile', 'environment', 'artifacts') `
        -Label 'local run runtime'
    Assert-JsonProperties `
        -Value $LocalRun.identity `
        -Names @(
            'initializationCompleted',
            'subjectId',
            'userName',
            'role') `
        -Label 'local run identity'
    Assert-JsonProperties `
        -Value $LocalRun.verification `
        -Names @('readiness', 'smoke') `
        -Label 'local run verification'
    Assert-JsonProperties `
        -Value $LocalRun.verification.readiness `
        -Names @(
            'result',
            'startAttempt',
            'observedUtc',
            'azuriteReady',
            'webReady',
            'functionsRunning') `
        -Label 'local run readiness evidence'
    Assert-JsonProperties `
        -Value $LocalRun.verification.smoke `
        -Names @(
            'result',
            'startAttempt',
            'observedUtc',
            'sourceSha',
            'webReady',
            'functionsRunning',
            'identityInitialized',
            'httpsOriginValidated',
            'administratorRouteValidated') `
        -Label 'local run smoke evidence'

    $ownedRepositoryRoot = [System.IO.Path]::GetFullPath(
        [string]$LocalRun.ownership.repositoryRoot)
    if ($LocalRun.schemaVersion -ne 1 -or
        [string]$LocalRun.kind -cne 'Pegasus.LocalDevelopment.Run' -or
        [string]$LocalRun.runId -cne $RunId -or
        [string]$LocalRun.state -cne 'Running' -or
        $LocalRun.startAttempt -lt 1 -or
        [string]$LocalRun.sourceSha -cne $ExpectedSourceRevision -or
        -not $ownedRepositoryRoot.Equals(
            $repositoryRoot,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]$LocalRun.ownership.cloudOperations -cne 'disabled' -or
        [string]$LocalRun.runtime.profile -cne 'DevelopmentOffline' -or
        [string]$LocalRun.runtime.environment -cne 'Development' -or
        $LocalRun.identity.initializationCompleted -isnot [bool] -or
        -not $LocalRun.identity.initializationCompleted -or
        [string]$LocalRun.identity.subjectId -cne 'd47fbbae-ea22-4ca6-b983-01e2ed1fbd13' -or
        [string]$LocalRun.identity.userName -cne 'development-offline-administrator' -or
        [string]$LocalRun.identity.role -cne 'Administrator') {
        throw 'OfflineCandidate is blocked: the local run manifest is not the successful exact-source DevelopmentOffline run for this acceptance invocation.'
    }

    $readiness = $LocalRun.verification.readiness
    $smoke = $LocalRun.verification.smoke
    if ([string]$readiness.result -cne 'Passed' -or
        $readiness.startAttempt -ne $LocalRun.startAttempt -or
        $readiness.azuriteReady -isnot [bool] -or
        -not $readiness.azuriteReady -or
        $readiness.webReady -isnot [bool] -or
        -not $readiness.webReady -or
        $readiness.functionsRunning -isnot [bool] -or
        -not $readiness.functionsRunning -or
        [string]$smoke.result -cne 'Passed' -or
        $smoke.startAttempt -ne $LocalRun.startAttempt -or
        [string]$smoke.sourceSha -cne $ExpectedSourceRevision -or
        $smoke.webReady -isnot [bool] -or
        -not $smoke.webReady -or
        $smoke.functionsRunning -isnot [bool] -or
        -not $smoke.functionsRunning -or
        $smoke.identityInitialized -isnot [bool] -or
        -not $smoke.identityInitialized -or
        $smoke.httpsOriginValidated -isnot [bool] -or
        -not $smoke.httpsOriginValidated -or
        $smoke.administratorRouteValidated -isnot [bool] -or
        -not $smoke.administratorRouteValidated) {
        throw 'OfflineCandidate is blocked: the local run manifest has no successful current-attempt readiness and smoke evidence.'
    }

    $createdUtc = ConvertTo-EvidenceTimestamp `
        -Value ([string]$LocalRun.createdUtc) `
        -Label 'local run creation'
    $readinessUtc = ConvertTo-EvidenceTimestamp `
        -Value ([string]$readiness.observedUtc) `
        -Label 'local run readiness evidence'
    $smokeUtc = ConvertTo-EvidenceTimestamp `
        -Value ([string]$smoke.observedUtc) `
        -Label 'local run smoke evidence'
    if ($readinessUtc -lt $createdUtc -or $smokeUtc -lt $readinessUtc) {
        throw 'OfflineCandidate is blocked: local run readiness and smoke evidence are not in run order.'
    }

    $expectedArtifacts = [ordered]@{
        web = 'src/Pegasus.Web/bin/Debug/net10.0/Pegasus.Web.dll'
        worker = 'src/Pegasus.Worker/bin/Debug/net10.0/Pegasus.Worker.dll'
    }
    foreach ($name in $expectedArtifacts.Keys) {
        $property = $LocalRun.runtime.artifacts.PSObject.Properties[$name]
        if ($null -eq $property) {
            throw "OfflineCandidate is blocked: local run runtime is missing the '$name' artifact record."
        }

        $record = $property.Value
        Assert-JsonProperties `
            -Value $record `
            -Names @('relativePath', 'byteLength', 'sha256') `
            -Label "local run '$name' artifact"
        if ([string]$record.relativePath -cne $expectedArtifacts[$name] -or
            [string]$record.byteLength -notmatch '^\d+$' -or
            [long]$record.byteLength -le 0 -or
            [string]$record.sha256 -cnotmatch '^[0-9a-f]{64}$') {
            throw "OfflineCandidate is blocked: local run '$name' artifact record is invalid."
        }

        $artifactPath = Join-Path $repositoryRoot ([string]$record.relativePath)
        if (-not [System.IO.File]::Exists($artifactPath)) {
            throw "OfflineCandidate is blocked: local run '$name' runtime artifact is missing."
        }

        $artifact = [System.IO.FileInfo]::new($artifactPath)
        $observedHash = (Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($artifact.Length -ne [long]$record.byteLength -or
            $observedHash -cne [string]$record.sha256) {
            throw "OfflineCandidate is blocked: local run '$name' runtime artifact differs from the initialized bytes."
        }
    }
}


function Assert-OfflineCandidatePrerequisites {
    param([Parameter(Mandatory)][string]$ExpectedSourceRevision)

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

    if ([string]$callerManifest.sourceRevision -cne $ExpectedSourceRevision -or
        [string]$callerManifest.runId -cne $RunId) {
        throw 'OfflineCandidate is blocked: caller evidence manifest sourceRevision and runId must identify this exact acceptance invocation.'
    }

    $localRunManifestPath = [System.IO.Path]::GetFullPath($LocalRunManifest)
    if (-not (Test-Path -LiteralPath $localRunManifestPath -PathType Leaf)) {
        throw "OfflineCandidate is blocked: local run manifest '$localRunManifestPath' does not exist."
    }
    try {
        $localRun = Get-Content -LiteralPath $localRunManifestPath -Raw |
            ConvertFrom-Json -DateKind String
    }
    catch {
        throw "OfflineCandidate is blocked: local run manifest '$localRunManifestPath' is not valid JSON."
    }
    Assert-LocalRunEvidence `
        -LocalRun $localRun `
        -ManifestPath $localRunManifestPath `
        -ExpectedSourceRevision $ExpectedSourceRevision

    return [pscustomobject]@{
        CapacityManifestPath = $manifestPath
        CapacityManifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
        CallerManifestPath = $callerManifestPath
        CallerManifestSha256 = (Get-FileHash -LiteralPath $callerManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
        LocalRunManifestPath = $localRunManifestPath
        LocalRunManifestSha256 = (Get-FileHash -LiteralPath $localRunManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$resolvedSourceRevision = Resolve-RepositorySourceRevision -RequestedRevision $SourceRevision
$previousProfile = [Environment]::GetEnvironmentVariable('PEGASUS_QDOS_PRESSURE_PROFILE', 'Process')
$previousAcceptanceManifest = [Environment]::GetEnvironmentVariable('PEGASUS_QDOS_ACCEPTANCE_MANIFEST', 'Process')
$previousAcceptanceRevision = [Environment]::GetEnvironmentVariable('PEGASUS_QDOS_ACCEPTANCE_SOURCE_REVISION', 'Process')
if (-not [string]::IsNullOrWhiteSpace($previousAcceptanceRevision)) {
    $environmentRevision = $previousAcceptanceRevision.ToLowerInvariant()
    if ($environmentRevision -notmatch '^[0-9a-f]{40}$' -or
        $environmentRevision -cne $resolvedSourceRevision) {
        throw "PEGASUS_QDOS_ACCEPTANCE_SOURCE_REVISION identifies '$previousAcceptanceRevision', but the executed checkout HEAD is '$resolvedSourceRevision'."
    }
}

$offlinePrerequisites = $null
if ($Profile -eq 'OfflineCandidate') {
    $offlinePrerequisites = Assert-OfflineCandidatePrerequisites -ExpectedSourceRevision $resolvedSourceRevision
}
$sourceRevisionProperty = "/p:SourceRevisionId=$resolvedSourceRevision"
$includeSourceRevisionProperty = '/p:IncludeSourceRevisionInInformationalVersion=true'

if (Test-Path -LiteralPath $evidenceRoot) {
    throw "Refusing to overwrite the immutable acceptance run '$RunId' at '$evidenceRoot'. Use a new RunId."
}
[System.IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
$failure = $null
$result = 'failed'
$testResultHash = $null
$acceptanceResultHash = $null
$stagingCreated = $false

try {
    if (-not (Test-Path -LiteralPath $integrationProject -PathType Leaf)) {
        throw "Integration test project '$integrationProject' does not exist."
    }

    [System.IO.Directory]::CreateDirectory($resultsRoot) | Out-Null
    if ($Profile -eq 'OfflineCandidate') {
        Set-Item -Path 'Env:PEGASUS_QDOS_ACCEPTANCE_MANIFEST' -Value $offlinePrerequisites.CallerManifestPath
        Set-Item -Path 'Env:PEGASUS_QDOS_ACCEPTANCE_SOURCE_REVISION' -Value $resolvedSourceRevision
        & dotnet test $integrationProject --configuration Release --filter 'Category=QdosAlphaAcceptance' --results-directory $resultsRoot --logger 'trx;LogFileName=qdos-alpha-acceptance.trx' $includeSourceRevisionProperty $sourceRevisionProperty
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

    & dotnet test $integrationProject --configuration Release --filter 'Category=QdosPressure' --results-directory $resultsRoot --logger 'trx;LogFileName=qdos-pressure.trx' $includeSourceRevisionProperty $sourceRevisionProperty
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
        sourceRevision = $resolvedSourceRevision
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
