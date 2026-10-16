# Fails when a repository Markdown file contains a relative link to a path that
# does not exist, or to a #fragment that its target Markdown file does not
# define. A fragment matches a heading slug (GitHub's rule, repeated slugs
# suffixed -1, -2) or the id or name of an <a> tag; a link that starts with #
# is checked against its own file. External URLs are not checked.
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
$excluded = '^(node_modules|corpus|artifacts|\.git|\.claude|\.codex|docs/external-component-documents)/'
$linkPattern = [regex]'\[[^\]]*\]\(([^)\s]+)\)'
# A fence is ``` or ~~~ at up to three spaces of indent; it closes on the next
# fence of the same character. Blank the body but keep the line count so any
# future line-numbered reporting stays accurate.
$fencePattern = [regex]'(?m)^[ ]{0,3}(`{3,}|~{3,})[^\n]*\n(?<body>(?:[^\n]*\n)*?)[ ]{0,3}\1[^\n]*$'
$inlineCodePattern = [regex]'(?<!`)(`+)(?!`)(?:[^\n]*?)(?<!`)\1(?!`)'
# An ATX heading: up to three spaces of indent, one to six #, the text, and an
# optional closing run of #.
$headingPattern = [regex]'^ {0,3}#{1,6}[ \t]+(?<text>.*?)(?:[ \t]+#+)?[ \t]*$'
# What a heading renders as text: a code span keeps its content, a link keeps
# its text, an HTML tag disappears. GitHub then drops every character that is
# not a letter, mark, number, connector punctuation, hyphen or space.
$headingMarkupPattern = [regex]'(?<ticks>`+)(?<code>.*?)\k<ticks>|\[(?<text>[^\]]*)\]\([^)]*\)|<[^>]*>'
$slugDropPattern = [regex]'[^\p{L}\p{M}\p{N}\p{Pc}\- ]'
$anchorTagPattern = [regex]'<a\s[^>]*>'
$anchorAttributePattern = [regex]'(?i)(?<![\w-])(?:id|name)\s*=\s*(?:"(?<value>[^"]*)"|''(?<value>[^'']*)'')'
# Anchors per target file, by full path, filled when a link first needs them.
$anchorCache = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.HashSet[string]]]::new([System.StringComparer]::Ordinal)

function Remove-CodeFences {
    param([string]$Text)
    return $fencePattern.Replace($Text, {
        param($match)
        $blankBody = ($match.Groups['body'].Value -replace '[^\n]', '')
        "`n$blankBody`n"
    })
}

function Remove-CodeSpans {
    param([string]$Text)
    return $inlineCodePattern.Replace((Remove-CodeFences $Text), { param($match) '' })
}

function Get-HeadingSlug {
    param([string]$Heading)
    $rendered = $headingMarkupPattern.Replace($Heading, { param($match) $match.Groups['code'].Value + $match.Groups['text'].Value })
    return $slugDropPattern.Replace($rendered.ToLowerInvariant(), '').Replace(' ', '-')
}

# True when the Markdown file at $Path has a heading or an <a> tag that the
# case-sensitive $Fragment names. Headings are read with their inline code,
# which is part of the heading text; <a> tags are not read from code.
function Test-FileAnchor {
    param([string]$Path, [string]$Fragment)
    if (-not $anchorCache.ContainsKey($Path)) {
        $raw = Get-Content -Raw -LiteralPath $Path
        $anchors = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($line in ((Remove-CodeFences $raw) -split '\r?\n')) {
            $heading = $headingPattern.Match($line)
            if (-not $heading.Success) { continue }
            $slug = Get-HeadingSlug $heading.Groups['text'].Value
            $candidate = $slug
            $suffix = 0
            while (-not $anchors.Add($candidate)) {
                $suffix++
                $candidate = "$slug-$suffix"
            }
        }
        foreach ($tag in $anchorTagPattern.Matches((Remove-CodeSpans $raw))) {
            foreach ($attribute in $anchorAttributePattern.Matches($tag.Value)) {
                [void]$anchors.Add($attribute.Groups['value'].Value)
            }
        }
        $anchorCache[$Path] = $anchors
    }
    return $anchorCache[$Path].Contains($Fragment)
}

$files = @(git -C $repoRoot -c core.quotePath=false ls-files --cached --others --exclude-standard '*.md' | Sort-Object -Unique | Where-Object { $_ -notmatch $excluded -and (Test-Path -LiteralPath (Join-Path $repoRoot $_) -PathType Leaf) })
$broken = @()
foreach ($relative in $files) {
    $file = Join-Path $repoRoot $relative
    $text = Remove-CodeSpans (Get-Content -Raw -LiteralPath $file)
    foreach ($match in $linkPattern.Matches($text)) {
        $target = $match.Groups[1].Value
        if ($target -match '^(https?:|mailto:)') { continue }
        $parts = $target -split '#', 2
        $path = [uri]::UnescapeDataString($parts[0])
        $fragment = if ($parts.Count -gt 1) { [uri]::UnescapeDataString($parts[1]) } else { '' }
        $resolved = if ($path -eq '') { $file } else { Join-Path (Split-Path -Parent $file) $path }
        if (-not (Test-Path -LiteralPath $resolved)) {
            $broken += "${relative}: $target"
            continue
        }
        if ($fragment -eq '' -or $resolved -notlike '*.md') { continue }
        if (-not (Test-FileAnchor ([System.IO.Path]::GetFullPath($resolved)) $fragment)) {
            $targetRelative = [System.IO.Path]::GetRelativePath($repoRoot, $resolved) -replace '\\', '/'
            $broken += "${relative}: $target (no '$fragment' heading or anchor in $targetRelative)"
        }
    }
}

if ($broken.Count -gt 0) {
    $broken | ForEach-Object { Write-Host "BROKEN $_" -ForegroundColor Red }
    Write-Error "$($broken.Count) broken relative Markdown link(s) or anchor(s)."
}
Write-Host "All relative Markdown links and anchors resolve ($($files.Count) files checked)."
