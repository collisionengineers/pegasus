[CmdletBinding()]
param(
    [switch]$Verify,
    [switch]$SkipCapture,
    [string]$Scope,
    [string]$CaptureFilter = 'FullyQualifiedName~WebTests|Category=Browser|FullyQualifiedName~StaffSignInSecurityTests|FullyQualifiedName~QdosCustodialWebTests|FullyQualifiedName~AutomationConnectorAuthorizationTests|FullyQualifiedName~ImageViewingWebTests'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$mode = if ($Verify) { 'verify' } else { 'update' }
$previousMode = $env:PEGASUS_TEST_UI_MODE
$previousCaptureDirectory = $env:PEGASUS_TEST_UI_CAPTURE_DIR
$previousScope = $env:PEGASUS_TEST_UI_SCOPE
$captureDirectory = Join-Path $repoRoot 'artifacts/test-ui-capture'
$testProject = "$repoRoot/tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj"
$effectiveCaptureFilter = "($CaptureFilter|FullyQualifiedName~TestUiFocusedRenderTests)"

function Invoke-TestUiPhase {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Filter,
        [switch]$NoBuild,
        [int]$MaxParallelThreads
    )

    $arguments = @(
        'test',
        $testProject,
        '--configuration', 'Release',
        '--no-restore'
    )
    if ($NoBuild) {
        $arguments += '--no-build'
    }
    $arguments += @('--filter', $Filter)
    if ($MaxParallelThreads -gt 0) {
        $arguments += @('--', "xUnit.MaxParallelThreads=$MaxParallelThreads")
    }

    Write-Host "Test UI phase: $Name"
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    & dotnet @arguments
    $exitCode = $LASTEXITCODE
    $timer.Stop()
    Write-Host "Test UI phase completed: $Name ($($timer.Elapsed.ToString('hh\:mm\:ss')))"
    if ($exitCode -ne 0) {
        throw "Test UI phase '$Name' failed with exit code $exitCode."
    }
}

try {
    if (-not $SkipCapture) {
        if (Test-Path -LiteralPath $captureDirectory) {
            Remove-Item -LiteralPath $captureDirectory -Recurse -Force
        }
        New-Item -ItemType Directory -Path $captureDirectory | Out-Null
    }
    elseif (-not (Test-Path -LiteralPath $captureDirectory -PathType Container)) {
        throw "No retained Test UI capture exists at $captureDirectory."
    }
    $env:PEGASUS_TEST_UI_CAPTURE_DIR = $captureDirectory
    $env:PEGASUS_TEST_UI_SCOPE = if ([string]::IsNullOrWhiteSpace($Scope)) { $null } else { $Scope }
    if (-not $SkipCapture) {
        $env:PEGASUS_TEST_UI_MODE = $null
        # Each browser test starts Chromium, Kestrel and its own database, so
        # this half keeps the browser lane's cap. The non-browser half inherits
        # the integration project's proven default cap.
        Invoke-TestUiPhase `
            -Name 'Capture browser responses' `
            -Filter "$effectiveCaptureFilter&Category!=Corpus&Category=Browser" `
            -MaxParallelThreads 2
        Invoke-TestUiPhase `
            -Name 'Capture non-browser responses' `
            -Filter "$effectiveCaptureFilter&Category!=Corpus&Category!=Browser" `
            -NoBuild
    }

    $env:PEGASUS_TEST_UI_MODE = $mode
    Invoke-TestUiPhase `
        -Name "Snapshot $mode" `
        -Filter 'FullyQualifiedName~TestUiSnapshotTests' `
        -NoBuild
}
finally {
    $env:PEGASUS_TEST_UI_MODE = $previousMode
    $env:PEGASUS_TEST_UI_CAPTURE_DIR = $previousCaptureDirectory
    $env:PEGASUS_TEST_UI_SCOPE = $previousScope
}
