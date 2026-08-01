#requires -Version 5.1
<#
.SYNOPSIS
  Render every generated starter document to artifacts/rendered-starters/ for a quick visual check.
.DESCRIPTION
  Builds the CLI if needed, ensures the Chromium engine is installed, then renders
  each template from its generated overwriteable starter. Useful after changing a
  template or the stylesheet to confirm the house style is intact.
#>
param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$exeName = if ($IsWindows) { 'collisionrenderer.exe' } else { 'collisionrenderer' }
$exe = Join-Path $root "src/CollisionRenderer.Cli/bin/$Configuration/net8.0/$exeName"
if (-not (Test-Path $exe)) {
    Write-Host 'Building CLI...' -ForegroundColor Cyan
    dotnet build src/CollisionRenderer.Cli/CollisionRenderer.Cli.csproj -c $Configuration | Out-Null
}

# Ensure Chromium is present (no-op if already installed).
& $exe install-browser | Out-Null

$out = Join-Path $root 'artifacts/rendered-starters'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$templates = @(
    'market-valuation-evidence',
    'advert-evidence-pack',
    'fee-note',
    'expert-report'
)

foreach ($t in $templates) {
    $file = $t.Replace('-', '_')
    $payload = Join-Path $out "$file.json"
    & $exe forms starter --template $t --out $payload
    & $exe render --template $t --data $payload --out (Join-Path $out "$file.pdf")
}

Write-Host "Done. PDFs are in $out" -ForegroundColor Green
