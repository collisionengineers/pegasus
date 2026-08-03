# Fails when a tracked Markdown file contains a relative link to a path that
# does not exist. External URLs and same-file anchors are not checked.
# Scope includes workspaces/ documentation; the protected skill packages under
# workspaces/ai-centre/skills/ remain excluded as immutable imported source.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$excluded = '^(node_modules|corpus|artifacts|\.git|\.claude|\.agents|\.codex)/|^docs/temp-plans/(?!README\.md$)|^workspaces/ai-centre/skills/(?!README\.md$)'
$linkPattern = [regex]'\[[^\]]*\]\(([^)\s]+)\)'

$files = git -C $repoRoot ls-files '*.md' | Where-Object { $_ -notmatch $excluded }
$broken = @()
foreach ($relative in $files) {
    $file = Join-Path $repoRoot $relative
    $text = Get-Content -Raw -LiteralPath $file
    foreach ($match in $linkPattern.Matches($text)) {
        $target = $match.Groups[1].Value
        if ($target -match '^(https?:|mailto:|#)') { continue }
        $path = [uri]::UnescapeDataString(($target -split '#')[0])
        if ([string]::IsNullOrEmpty($path)) { continue }
        $resolved = Join-Path (Split-Path -Parent $file) $path
        if (-not (Test-Path -LiteralPath $resolved)) {
            $broken += "${relative}: $target"
        }
    }
}

if ($broken.Count -gt 0) {
    $broken | ForEach-Object { Write-Host "BROKEN $_" -ForegroundColor Red }
    Write-Error "$($broken.Count) broken relative Markdown link(s)."
}
Write-Host "All relative Markdown links resolve ($($files.Count) files checked)."
