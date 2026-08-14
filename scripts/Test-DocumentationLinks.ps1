# Fails when a tracked Markdown file contains a relative link to a path that
# does not exist. External URLs and same-file anchors are not checked.
# Scope includes workspace documentation.
# Fenced code blocks and inline code spans are stripped before scanning: text
# there is a literal sample, not a link. ASCII wireframes are the usual case —
# a mockup row like `scan.tiff  12.8 MB  [Too large](x)` is column alignment,
# not Markdown, and must not be reported as a broken link.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$excluded = '^(node_modules|corpus|artifacts|\.git|\.claude|\.agents|\.codex|\.kanmer)/|^docs/temp-plans/(?!README\.md$)'
$linkPattern = [regex]'\[[^\]]*\]\(([^)\s]+)\)'
# A fence is ``` or ~~~ at up to three spaces of indent; it closes on the next
# fence of the same character. Blank the body but keep the line count so any
# future line-numbered reporting stays accurate.
$fencePattern = [regex]'(?m)^[ ]{0,3}(`{3,}|~{3,})[^\n]*\n(?<body>(?:[^\n]*\n)*?)[ ]{0,3}\1[^\n]*$'
$inlineCodePattern = [regex]'(?<!`)(`+)(?!`)(?:[^\n]*?)(?<!`)\1(?!`)'

function Remove-CodeSpans {
    param([string]$Text)
    $withoutFences = $fencePattern.Replace($Text, {
        param($match)
        $blankBody = ($match.Groups['body'].Value -replace '[^\n]', '')
        "`n$blankBody`n"
    })
    return $inlineCodePattern.Replace($withoutFences, { param($match) '' })
}

$files = git -C $repoRoot ls-files '*.md' | Where-Object { $_ -notmatch $excluded }
$broken = @()
foreach ($relative in $files) {
    $file = Join-Path $repoRoot $relative
    $text = Remove-CodeSpans (Get-Content -Raw -LiteralPath $file)
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
