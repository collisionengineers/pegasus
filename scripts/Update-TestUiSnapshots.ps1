[CmdletBinding()]
param(
    [switch]$Verify,
    [switch]$SkipCapture
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$mode = if ($Verify) { 'verify' } else { 'update' }
$previousMode = $env:PEGASUS_TEST_UI_MODE
$previousCaptureDirectory = $env:PEGASUS_TEST_UI_CAPTURE_DIR
$captureDirectory = Join-Path $repoRoot 'artifacts/test-ui-capture'

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
    if (-not $SkipCapture) {
        $env:PEGASUS_TEST_UI_MODE = $null
        dotnet test "$repoRoot/tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj" `
            --configuration Release `
            --no-restore `
            --filter '(FullyQualifiedName~WebTests|Category=Browser|FullyQualifiedName~StaffSignInSecurityTests|FullyQualifiedName~TestUiFocusedRenderTests|FullyQualifiedName~QdosCustodialWebTests|FullyQualifiedName~AutomationConnectorAuthorizationTests)'
        if ($LASTEXITCODE -ne 0) {
            throw "The integration capture suite failed with exit code $LASTEXITCODE."
        }
    }

    $env:PEGASUS_TEST_UI_MODE = $mode
    dotnet test "$repoRoot/tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj" `
        --configuration Release `
        --no-restore `
        --filter 'FullyQualifiedName~TestUiSnapshotTests'
    if ($LASTEXITCODE -ne 0) {
        throw "Test UI snapshot $mode failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:PEGASUS_TEST_UI_MODE = $previousMode
    $env:PEGASUS_TEST_UI_CAPTURE_DIR = $previousCaptureDirectory
}
