[CmdletBinding()]
param(
    [ValidateSet('OfflineCandidate')]
    [string]$Profile = 'OfflineCandidate',

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
$evidenceRoot = Join-Path $repositoryRoot "artifacts/qdos-alpha-acceptance/$RunId"
$evidencePath = Join-Path $evidenceRoot 'evidence.json'
$evidenceTempPath = Join-Path $evidenceRoot 'evidence.json.tmp'
$resultsRoot = Join-Path $evidenceRoot 'test-results'
$capabilitiesRegisterPath = Join-Path $repositoryRoot 'docs/capabilities.md'
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


# --- Offline-candidate acceptance contract -----------------------------------
# The caller-evidence manifest: schemaVersion 1, this kind, the run's source
# revision and run id, capabilityObservations[] and externalGateEvidence[].
$acceptanceManifestKind = 'Pegasus.QdosAlpha.AcceptanceEvidence'
$alphaTargetVersion = '0.1.0-alpha.1'
# Observation outcomes: 'passed' (a local caller produced the evidence) or
# 'deferredToExternalGate' (the evidence is an external gate's).
$passedOutcome = 'passed'
$deferredOutcome = 'deferredToExternalGate'
# Capabilities whose alpha evidence is an external gate rather than a local
# caller (deployment and live verification): the offline candidate may defer
# them; release needs them passed.
$externalGateCapabilityIds = @('OPS-10', 'OPS-24', 'OPS-25')
$offlineGateIds = @('approved-capacity-dataset', 'accepted-genuine-route-evidence')
$releaseGateIds = @(
    'approved-capacity-dataset',
    'accepted-genuine-route-evidence',
    'graph-scope-and-contract',
    'box-scope-and-contract',
    'dvla-dvsa-contract',
    'azure-deployment-and-recovery',
    'exact-head-independent-review',
    'qdos-operator-acceptance',
    'collision-engineers-management-approval')

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
        [string]$callerManifest.kind -cne $acceptanceManifestKind) {
        throw "OfflineCandidate is blocked: caller evidence manifest must use schemaVersion 1 and kind '$acceptanceManifestKind'."
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
        CallerManifest = $callerManifest
        LocalRunManifestPath = $localRunManifestPath
        LocalRunManifestSha256 = (Get-FileHash -LiteralPath $localRunManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

# The alpha capability roster is read from the capability register, never
# hard-coded here: a copy drifted (it demanded the retired DOC-06 and missed
# fifteen later alpha rows). Rows whose "Target release" column equals the
# target are the capabilities an offline candidate must evidence.
function Get-AlphaCapabilityIds {
    if (-not (Test-Path -LiteralPath $capabilitiesRegisterPath -PathType Leaf)) {
        throw "OfflineCandidate is blocked: capability register '$capabilitiesRegisterPath' does not exist."
    }

    $ids = [System.Collections.Generic.List[string]]::new()
    foreach ($line in Get-Content -LiteralPath $capabilitiesRegisterPath) {
        if ($line -notmatch '^\| ([A-Z]+-\d+) \|') {
            continue
        }

        # "| ID | Durable outcome | Horizon | Target release | Canonical owner | Activation/boundary |"
        # splits into eight fields with empty ends; the version is the fifth.
        $cells = $line.Split('|') | ForEach-Object { $_.Trim() }
        if ($cells.Count -ne 8) {
            throw "OfflineCandidate is blocked: capability register row for '$($Matches[1])' does not have the six expected columns."
        }
        if ($cells[4] -ceq $alphaTargetVersion) {
            $ids.Add($Matches[1])
        }
    }

    if ($ids.Count -eq 0) {
        throw "OfflineCandidate is blocked: no capability in '$capabilitiesRegisterPath' targets version '$alphaTargetVersion'."
    }

    return $ids
}

function Test-LowerHex {
    param([string]$Value, [Parameter(Mandatory)][int]$Length)
    return $Value -cmatch "^[0-9a-f]{$Length}$"
}

function Add-Blocker {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Blockers,
        [Parameter(Mandatory)][string]$Blocker)
    if (-not $Blockers.Contains($Blocker)) {
        $Blockers.Add($Blocker)
    }
}

function Get-OrdinalSorted {
    param([Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Values)
    $sorted = [string[]]$Values.ToArray()
    [Array]::Sort($sorted, [System.StringComparer]::Ordinal)
    return ,$sorted
}

# Evidence references are assertions until the file behind each one is
# re-hashed here; a missing or altered file is a blocker, not a warning.
function Test-EvidenceFile {
    param(
        [Parameter(Mandatory)][string]$ManifestDirectory,
        [string]$Reference,
        [string]$ExpectedSha256,
        [Parameter(Mandatory)][string]$Subject,
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Blockers)

    if ([string]::IsNullOrWhiteSpace($Reference)) {
        Add-Blocker $Blockers "${Subject}:evidence-reference-missing"
        return
    }
    if (-not (Test-LowerHex -Value $ExpectedSha256 -Length 64)) {
        Add-Blocker $Blockers "${Subject}:evidence-hash-invalid"
        return
    }

    $path = if ([System.IO.Path]::IsPathRooted($Reference)) {
        [System.IO.Path]::GetFullPath($Reference)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $ManifestDirectory $Reference))
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Blocker $Blockers "${Subject}:evidence-file-missing"
        return
    }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $ExpectedSha256) {
        Add-Blocker $Blockers "${Subject}:evidence-hash-mismatch"
    }
}

# The offline-candidate coverage check: every alpha capability in the register
# has exactly one observation with a caller and hashed evidence; a capability
# may defer to an external gate only where the register's external evidence
# stands in; the offline external gates carry approval and hashed evidence.
# Release acceptance additionally needs every external gate and passed evidence
# for the external-gate capabilities; that verdict is recorded, not enforced,
# because release is a separate fail-closed decision.
function Assert-AlphaCapabilityCoverage {
    param(
        [Parameter(Mandatory)]$CallerManifest,
        [Parameter(Mandatory)][string]$CallerManifestPath,
        [Parameter(Mandatory)][string[]]$RequiredCapabilityIds)

    $manifestDirectory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($CallerManifestPath))
    $blockers = [System.Collections.Generic.List[string]]::new()
    $required = [System.Collections.Generic.HashSet[string]]::new([string[]]$RequiredCapabilityIds, [System.StringComparer]::Ordinal)

    # Observations: one per required capability, each with a caller and hashed evidence.
    $observations = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
    foreach ($observation in @($CallerManifest.capabilityObservations)) {
        if ($null -eq $observation) {
            Add-Blocker $blockers 'capability:null'
            continue
        }
        $capabilityId = [string]$observation.capabilityId
        if (-not $required.Contains($capabilityId)) {
            Add-Blocker $blockers "capability:${capabilityId}:not-qdos-owned"
            continue
        }
        if ($observations.ContainsKey($capabilityId)) {
            Add-Blocker $blockers "capability:${capabilityId}:duplicate"
            continue
        }
        $observations[$capabilityId] = $observation
    }

    foreach ($capabilityId in $RequiredCapabilityIds) {
        if (-not $observations.ContainsKey($capabilityId)) {
            Add-Blocker $blockers "capability:${capabilityId}:missing"
            continue
        }
        $observation = $observations[$capabilityId]
        $outcome = [string]$observation.outcome
        if ($outcome -cne $passedOutcome -and $outcome -cne $deferredOutcome) {
            Add-Blocker $blockers "capability:${capabilityId}:invalid-outcome"
        }
        if ($outcome -ceq $deferredOutcome -and $capabilityId -cnotin $externalGateCapabilityIds) {
            Add-Blocker $blockers "capability:${capabilityId}:cannot-defer"
        }
        if ([string]::IsNullOrWhiteSpace([string]$observation.caller)) {
            Add-Blocker $blockers "capability:${capabilityId}:caller-missing"
        }
        Test-EvidenceFile -ManifestDirectory $manifestDirectory -Reference ([string]$observation.evidenceReference) `
            -ExpectedSha256 ([string]$observation.evidenceSha256) -Subject "capability:$capabilityId" -Blockers $blockers
    }

    # External gates: validate each one that is present exactly once, then ask
    # which required gates are absent for the offline and release verdicts.
    $gateBlockers = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.List[string]]]::new([System.StringComparer]::Ordinal)
    foreach ($evidence in @($CallerManifest.externalGateEvidence)) {
        if ($null -eq $evidence) {
            Add-Blocker $blockers 'external-gate:null'
            continue
        }
        $gateId = [string]$evidence.gateId
        if ($gateId -cnotin $releaseGateIds) {
            Add-Blocker $blockers "external-gate:${gateId}:unknown"
            continue
        }
        if ($gateBlockers.ContainsKey($gateId)) {
            Add-Blocker $blockers "external-gate:${gateId}:duplicate"
            continue
        }
        $own = [System.Collections.Generic.List[string]]::new()
        if ([string]::IsNullOrWhiteSpace([string]$evidence.approvalReference)) {
            Add-Blocker $own "external-gate:${gateId}:approval-reference-missing"
        }
        Test-EvidenceFile -ManifestDirectory $manifestDirectory -Reference ([string]$evidence.evidenceReference) `
            -ExpectedSha256 ([string]$evidence.evidenceSha256) -Subject "external-gate:$gateId" -Blockers $own
        $gateBlockers[$gateId] = $own
    }

    foreach ($gateId in $offlineGateIds) {
        if ($gateBlockers.ContainsKey($gateId)) {
            foreach ($blocker in $gateBlockers[$gateId]) { Add-Blocker $blockers $blocker }
        }
        else {
            Add-Blocker $blockers "external-gate:${gateId}:missing"
        }
    }

    $releaseBlockers = [System.Collections.Generic.List[string]]::new($blockers)
    foreach ($capabilityId in $externalGateCapabilityIds) {
        if ($observations.ContainsKey($capabilityId) -and ([string]$observations[$capabilityId].outcome) -cne $passedOutcome) {
            Add-Blocker $releaseBlockers "capability:${capabilityId}:external-evidence-required"
        }
    }
    foreach ($gateId in $releaseGateIds) {
        if ($gateBlockers.ContainsKey($gateId)) {
            foreach ($blocker in $gateBlockers[$gateId]) { Add-Blocker $releaseBlockers $blocker }
        }
        else {
            Add-Blocker $releaseBlockers "external-gate:${gateId}:missing"
        }
    }

    $sortedBlockers = Get-OrdinalSorted $blockers
    if ($sortedBlockers.Count -gt 0) {
        throw "OfflineCandidate is blocked by $($sortedBlockers.Count) coverage blocker(s): $($sortedBlockers -join ', ')"
    }

    return [pscustomobject]@{
        RequiredCapabilityCount = $RequiredCapabilityIds.Count
        ReleaseAccepted = $releaseBlockers.Count -eq 0
        ReleaseBlockers = Get-OrdinalSorted $releaseBlockers
    }
}

$resolvedSourceRevision = Resolve-RepositorySourceRevision -RequestedRevision $SourceRevision

$offlinePrerequisites = Assert-OfflineCandidatePrerequisites -ExpectedSourceRevision $resolvedSourceRevision
$acceptanceCoverage = Assert-AlphaCapabilityCoverage `
    -CallerManifest $offlinePrerequisites.CallerManifest `
    -CallerManifestPath $offlinePrerequisites.CallerManifestPath `
    -RequiredCapabilityIds (Get-AlphaCapabilityIds)
$sourceRevisionProperty = "/p:SourceRevisionId=$resolvedSourceRevision"
$includeSourceRevisionProperty = '/p:IncludeSourceRevisionInInformationalVersion=true'

if (Test-Path -LiteralPath $evidenceRoot) {
    throw "Refusing to overwrite the immutable acceptance run '$RunId' at '$evidenceRoot'. Use a new RunId."
}
[System.IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
$failure = $null
$result = 'failed'
$acceptanceResultHash = $null

try {
    if (-not (Test-Path -LiteralPath $integrationProject -PathType Leaf)) {
        throw "Integration test project '$integrationProject' does not exist."
    }

    [System.IO.Directory]::CreateDirectory($resultsRoot) | Out-Null
    # The acceptance test lane: the recovery and triage tests that carry
    # the QdosAlphaAcceptance trait, compiled at this exact revision.
    & dotnet test $integrationProject --configuration Release --filter 'Category=QdosAlphaAcceptance' --results-directory $resultsRoot --logger 'trx;LogFileName=qdos-alpha-acceptance.trx' $includeSourceRevisionProperty $sourceRevisionProperty
    if ($LASTEXITCODE -ne 0) {
        throw "QDOS alpha acceptance test lane failed with exit code $LASTEXITCODE."
    }

    $acceptanceTrxPath = Join-Path $resultsRoot 'qdos-alpha-acceptance.trx'
    if (-not (Test-Path -LiteralPath $acceptanceTrxPath -PathType Leaf)) {
        throw 'QDOS alpha acceptance test lane completed without the required TRX evidence.'
    }
    $acceptanceResultHash = (Get-FileHash -LiteralPath $acceptanceTrxPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $result = 'offline-candidate-verified'
}
catch {
    $failure = $_.Exception.Message
}
finally {
    $offlineMatrixSourceHashes = [ordered]@{}
    foreach ($name in @(
        'RecoveryTests.cs',
        'QdosTriageIntegrationTests.cs',
        'QdosTriageReplayIntegrationTests.cs',
        'QdosTriageCaseAssociationIntegrationTests.cs')) {
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
        offlineMatrixSourceSha256 = $offlineMatrixSourceHashes
        acceptanceTestResultSha256 = $acceptanceResultHash
        acceptanceCoverage = [ordered]@{
            capabilityRegister = 'docs/capabilities.md'
            targetVersion = $alphaTargetVersion
            requiredCapabilityCount = $acceptanceCoverage.RequiredCapabilityCount
            offlineAccepted = $true
            releaseAccepted = $acceptanceCoverage.ReleaseAccepted
            releaseBlockers = $acceptanceCoverage.ReleaseBlockers
        }
        capacityDatasetManifestSha256 = $offlinePrerequisites.CapacityManifestSha256
        callerEvidenceManifestSha256 = $offlinePrerequisites.CallerManifestSha256
        localRunManifestSha256 = $offlinePrerequisites.LocalRunManifestSha256
        failure = $failure
        limitation = if ($result -eq 'offline-candidate-verified') {
            'This runner verified the QDOS-owned offline caller map against the capability register, the acceptance test lane, the run-scoped deterministic-offline manifest, and approved immutable capacity evidence. Live adapter scopes, Azure deployment and recovery, exact-head review, QDOS operator acceptance, Collision Engineers management approval, release, and deployment remain separate fail-closed gates.'
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

Write-Output "QDOS offline candidate verification passed for run '$RunId'."
Write-Output "Evidence: $evidencePath"
Write-Output "Evidence SHA-256: $evidenceHash"
Write-Output 'This result is not release acceptance, deployment evidence, live verification, QDOS operator acceptance, or Collision Engineers management approval.'
