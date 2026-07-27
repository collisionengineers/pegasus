#requires -Version 5.1
<#
.SYNOPSIS
  Render every bundled sample document to artifacts/rendered-samples/ for a quick visual check.
.DESCRIPTION
  Builds the CLI if needed, ensures the Chromium engine is installed, then renders
  each template from its sample payload. Useful after changing a template or the
  stylesheet to confirm the house style is intact.
#>
param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$exe = Join-Path $root "src/CollisionRenderer.Cli/bin/$Configuration/net8.0/collisionrenderer.exe"
if (-not (Test-Path $exe)) {
    Write-Host 'Building CLI...' -ForegroundColor Cyan
    dotnet build src/CollisionRenderer.Cli/CollisionRenderer.Cli.csproj -c $Configuration | Out-Null
}

# Ensure Chromium is present (no-op if already installed).
& $exe install-browser | Out-Null

$samples = Join-Path $root 'src/CollisionRenderer.Core/Assets/samples'
$out = Join-Path $root 'artifacts/rendered-samples'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$templates = @(
    'market-valuation-evidence',
    'advert-evidence-pack',
    'fee-note',
    'expert-report'
)

foreach ($t in $templates) {
    $file = $t.Replace('-', '_')
    & $exe render --template $t --data (Join-Path $samples "$file.json") --out (Join-Path $out "$file.pdf")
}

Write-Host "Done. PDFs are in $out" -ForegroundColor Green
