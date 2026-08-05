<#
.SYNOPSIS
  Build a local .mcpb bundle for the CollisionRenderer stdio MCP host.

.DESCRIPTION
  Publishes a self-contained single-file executable (the .NET runtime, templates and
  brand assets are bundled in the exe) together with Playwright's loose `.playwright`
  driver directory — which single-file publish cannot embed and the host needs at
  runtime to drive Chromium. Chromium's HEADLESS SHELL is then installed INTO the bundle
  (`chromium --only-shell` → ms-playwright/chromium_headless_shell-<rev>/), the smallest
  browser that still supports PdfAsync. The manifest points PLAYWRIGHT_BROWSERS_PATH at
  that bundled dir and sets COLLISIONRENDERER_SKIP_BROWSER_INSTALL=1, so rendering runs
  straight from the bundle with NO runtime download.

  Layout of the produced .mcpb (a zip):
    manifest.json
    bin/collisionrenderer-mcp.exe
    bin/.playwright/...                              (node driver)
    ms-playwright/chromium_headless_shell-<rev>/...  (bundled browser)

.NOTES
  Windows-only (win-x64). The bundled shell is win-x64; for macOS/Linux add the matching
  RID and install the shell for that platform, building one .mcpb per platform.
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows -and $Runtime -eq "win-x64") {
    throw "build-mcpb.ps1 produces a win-x64 bundle with a win32_x64 Playwright driver and must run on Windows. To build for this platform, supply a matching -Runtime and install the Playwright shell for it."
}

$projectDir = $PSScriptRoot
$project = Join-Path $projectDir "CollisionRenderer.Mcp.csproj"
$manifestPath = Join-Path $projectDir "manifest.json"
$manifestJson = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$repoRoot = (Resolve-Path (Join-Path $projectDir "../..")).Path
$dist = Join-Path $repoRoot "dist"
$staging = Join-Path $dist "mcpb-staging"
$mcpbPath = Join-Path $dist "collisionrenderer-mcp-$($manifestJson.version).mcpb"

Write-Host "==> Publishing self-contained single-file ($Runtime)…"
dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

$publishDir = Join-Path $projectDir "bin/$Configuration/net10.0/$Runtime/publish"
if (-not (Test-Path (Join-Path $publishDir "collisionrenderer-mcp.exe"))) {
    throw "expected published exe not found under $publishDir"
}
# The non-single-file build output (the pre-publish RID dir) keeps the COMPLETE loose
# .playwright driver. It backs both the driver overlay below and the browser install.
$installerDir = Split-Path $publishDir -Parent

Write-Host "==> Staging bundle…"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $staging "bin") -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination (Join-Path $staging "bin") -Recurse -Force
# Single-file publish silently drops the driver's node runtime
# (.playwright\node\win32_x64\node.exe) from the publish dir — a playwright-dotnet
# limitation. Without it EVERY install fails "Driver not found" at first render, and
# install_browser cannot run either (the installer needs the same driver). Replace the
# staged (incomplete) driver with the complete one from the non-single-file output.
$stagedDriver = Join-Path $staging "bin/.playwright"
if (Test-Path $stagedDriver) { Remove-Item $stagedDriver -Recurse -Force }
Copy-Item -Path (Join-Path $installerDir ".playwright") -Destination $stagedDriver -Recurse -Force
# Debug symbols are not needed at runtime; keep the bundle lean.
Get-ChildItem (Join-Path $staging "bin") -Recurse -Include *.pdb -File | Remove-Item -Force
Copy-Item $manifestPath (Join-Path $staging "manifest.json") -Force

Write-Host "==> Installing Chromium headless shell into bundle (chromium --only-shell)…"
# Install the headless shell INTO the bundle using the staged Playwright driver, so its
# revision matches the bundled Microsoft.Playwright exactly. The manifest points the
# runtime PLAYWRIGHT_BROWSERS_PATH at this same ms-playwright dir.
$browsers = Join-Path $staging "ms-playwright"
New-Item -ItemType Directory -Path $browsers -Force | Out-Null
# Drive the install from the NON-single-file build output (the pre-publish RID dir),
# which keeps the loose Microsoft.Playwright.dll that playwright.ps1 needs alongside
# the driver. The staged single-file publish strips the dll, so its own copy cannot run.
$pwScript = Join-Path $installerDir "playwright.ps1"
if (-not (Test-Path $pwScript)) { throw "playwright.ps1 not found at $pwScript (needed to install the shell)" }
$prevBrowsers = $env:PLAYWRIGHT_BROWSERS_PATH
$env:PLAYWRIGHT_BROWSERS_PATH = $browsers
try {
    & pwsh -NoProfile -File $pwScript install chromium --only-shell
    if ($LASTEXITCODE -ne 0) { throw "playwright install chromium --only-shell failed (exit $LASTEXITCODE)" }
} finally {
    if ($null -eq $prevBrowsers) { Remove-Item Env:PLAYWRIGHT_BROWSERS_PATH -ErrorAction SilentlyContinue }
    else { $env:PLAYWRIGHT_BROWSERS_PATH = $prevBrowsers }
}
$shellDir = Get-ChildItem $browsers -Directory -Filter "chromium_headless_shell-*" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $shellDir) { throw "headless shell did not install into $browsers (no chromium_headless_shell-* dir)" }
Write-Host "==> Bundled browser: ms-playwright\$($shellDir.Name)"

# Completeness gate: the exact file Playwright's driver resolution probes at runtime.
# 0.2.3/0.2.4 shipped without it (single-file publish dropped it) and every install was
# dead on arrival with no runtime recovery path — never let that ship silently again.
$driverNode = Join-Path $staging "bin\.playwright\node\win32_x64\node.exe"
if (-not (Test-Path $driverNode)) { throw "bundle incomplete: $driverNode missing" }
Write-Host "==> Driver verified: bin\.playwright\node\win32_x64\node.exe"

Write-Host "==> Zipping -> $mcpbPath"
if (Test-Path $mcpbPath) { Remove-Item $mcpbPath -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
# CreateFromDirectory writes entries relative to $staging, so manifest.json + bin/ sit at the zip root.
[System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $mcpbPath)
Remove-Item $staging -Recurse -Force

$sizeMB = [math]::Round((Get-Item $mcpbPath).Length / 1MB, 1)
Write-Host "==> Built $mcpbPath ($sizeMB MB). Chromium headless shell is bundled — no runtime download."
