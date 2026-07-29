[CmdletBinding()]
param(
    [string]$ApprovalPath,
    [switch]$RequireQdosAlphaActivation
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Test-SourceMarkers {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string[]]$Markers
    )

    if (-not [System.IO.File]::Exists($Path)) {
        return $false
    }

    $content = [System.IO.File]::ReadAllText($Path)
    return @($Markers | Where-Object { -not $content.Contains($_, [System.StringComparison]::Ordinal) }).Count -eq 0
}

function Test-ActivationApproval {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return [pscustomobject]@{ IsApproved = $false; Detail = 'No activation approval was supplied.' }
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $excludedRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'docs/reference/imp-docs'))
    if ($fullPath.Equals($excludedRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($excludedRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Activation approval must not be read from docs/reference/imp-docs.'
    }
    if (-not [System.IO.File]::Exists($fullPath)) {
        throw "Activation approval does not exist: $fullPath"
    }

    try {
        $approval = [System.IO.File]::ReadAllText($fullPath) | ConvertFrom-Json
    }
    catch {
        throw "Activation approval is not valid JSON: $fullPath"
    }

    if ($approval.schemaVersion -ne 1 -or
        $approval.kind -ne 'Pegasus.QdosAlpha.ActivationApproval' -or
        $approval.scope -ne 'offline-qdos-alpha-acceptance-capacity' -or
        $approval.decision -ne 'Approved' -or
        [string]::IsNullOrWhiteSpace([string]$approval.approvalId) -or
        [string]::IsNullOrWhiteSpace([string]$approval.approvedUtc)) {
        throw "Activation approval does not satisfy the Pegasus QDOS alpha approval contract: $fullPath"
    }

    $approvedUtc = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse([string]$approval.approvedUtc, [ref]$approvedUtc)) {
        throw "Activation approval has an invalid approvedUtc value: $fullPath"
    }

    return [pscustomobject]@{
        IsApproved = $true
        Detail = "Approved by $($approval.approvalId) at $($approvedUtc.ToUniversalTime().ToString('O'))."
    }
}

$webProgram = Join-Path $repositoryRoot 'src/Pegasus.Web/Program.cs'
$workerProgram = Join-Path $repositoryRoot 'src/Pegasus.Worker/Program.cs'
$qdosReplayTests = Join-Path $repositoryRoot 'tests/Pegasus.IntegrationTests/QdosIntakeWebTests.cs'
$capacityProject = Join-Path $repositoryRoot 'tests/Pegasus.PerformanceTests/Pegasus.PerformanceTests.csproj'
$capacitySoak = Join-Path $repositoryRoot 'tests/Pegasus.PerformanceTests/CapacitySoakTests.cs'
$failureInjection = Join-Path $repositoryRoot 'tests/Pegasus.PerformanceTests/FailureInjectionTests.cs'

$offlineStartupImplemented = Test-SourceMarkers -Path $webProgram -Markers @(
    'DevelopmentOffline',
    '--migrate-development',
    'Features:LocalIntake',
    'UseSqlite'
)
$replayImplemented = Test-SourceMarkers -Path $qdosReplayTests -Markers @(
    'GenuineQdosCorpusFact',
    'ParallelDistinctConfirmedInputsPersistUniquePreCaseReceiptsInFileBackedSqlite'
)
$workerHostImplemented = Test-SourceMarkers -Path $workerProgram -Markers @('ConfigureFunctionsWorkerDefaults')
$capacityRunnerImplemented = (Test-Path -LiteralPath $capacityProject -PathType Leaf) -and
    (Test-Path -LiteralPath $capacitySoak -PathType Leaf) -and
    (Test-Path -LiteralPath $failureInjection -PathType Leaf)
$approval = Test-ActivationApproval -Path $ApprovalPath

$activationGate = $null
if (-not $offlineStartupImplemented -or -not $replayImplemented) {
    $activationGate = 'QDOS-OFFLINE-REPLAY-CONTRACT-MISSING'
}
elseif (-not $capacityRunnerImplemented) {
    $activationGate = 'QDOS-ALPHA-CAPACITY-RUNNER-MISSING'
}
elseif (-not $approval.IsApproved) {
    $activationGate = 'QDOS-ALPHA-EXTERNAL-APPROVAL-MISSING'
}

$report = [pscustomobject][ordered]@{
    schemaVersion = 1
    kind = 'Pegasus.LocalDevelopment.Doctor'
    offlineReplay = [ordered]@{
        status = $(if ($offlineStartupImplemented -and $replayImplemented) { 'Ready' } else { 'Blocked' })
        webStartupImplemented = $offlineStartupImplemented
        qdosReplayImplemented = $replayImplemented
        workerHostImplemented = $workerHostImplemented
        workerStartsForOfflineReplay = $false
    }
    alphaActivation = [ordered]@{
        status = $(if ($null -eq $activationGate) { 'Ready' } else { 'Blocked' })
        activationAllowed = $null -eq $activationGate
        gate = $activationGate
        capacityRunnerImplemented = $capacityRunnerImplemented
        approval = $approval.Detail
    }
}

if ($RequireQdosAlphaActivation -and $activationGate) {
    throw "QDOS alpha activation is blocked by $activationGate."
}

$report
