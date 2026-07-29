[CmdletBinding()]
param(
    [ValidateSet('CaptureBaseline', 'VerifyHead')]
    [string]$DocumentationConsolidationMode,
    [string]$DispositionManifest,
    [string]$BaselineManifest,
    [string]$HeadManifest,
    [string]$MaterialClaimInventory,
    [string]$CallsiteInventory
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()
$allowedLanguageMatches = [System.Collections.Generic.List[string]]::new()

$documentationExcludedPrefix = 'docs/reference/imp-docs/'
$excludedOperations = 0

function ConvertTo-RepositoryRelativePath([string]$Path) {
    $relative = $Path -replace '\\', '/'
    while ($relative.StartsWith('./', [System.StringComparison]::Ordinal)) {
        $relative = $relative.Substring(2)
    }
    return $relative
}

function Assert-AllowedDocumentationPath([string]$Path) {
    $relative = ConvertTo-RepositoryRelativePath $Path
    if ($relative.Equals($documentationExcludedPrefix.TrimEnd('/'), [System.StringComparison]::OrdinalIgnoreCase) -or
        $relative.StartsWith($documentationExcludedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Excluded documentation path was presented to an operation: $relative"
    }
    return $relative
}

function Get-FileSha256([string]$LiteralPath) {
    $stream = [System.IO.File]::OpenRead($LiteralPath)
    try {
        $hash = [System.Security.Cryptography.SHA256]::HashData($stream)
        return [Convert]::ToHexString($hash).ToLowerInvariant()
    }
    finally {
        $stream.Dispose()
    }
}

function Write-DeterministicJson([string]$LiteralPath, [object]$Value, [int]$Depth = 20) {
    $parent = Split-Path -Parent $LiteralPath
    if ($parent) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    $json = $Value | ConvertTo-Json -Depth $Depth
    [System.IO.File]::WriteAllText(
        $LiteralPath,
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
}

function Get-TrackedAllowedTextPaths {
    $pathspecs = @(
        '--',
        '*.md', '*.txt', '*.yml', '*.yaml', '*.json', '*.jsonc', '*.ps1', '*.py',
        '*.bicep', '*.csproj', '*.sln', '*.slnx', '*.cs', '*.cshtml', '*.props',
        '*.targets', '*.xml', '*.config', '*.toml',
        ':(exclude)docs/reference/imp-docs', ':(exclude)docs/reference/imp-docs/**'
    )
    $tracked = @(git -C $root ls-files @pathspecs)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to enumerate allowed tracked text paths.'
    }
    return @(
        $tracked |
            ForEach-Object { Assert-AllowedDocumentationPath $_ } |
            Sort-Object -Unique
    )
}

function Get-DocumentationCallsites([string[]]$OriginPaths) {
    $rows = [System.Collections.Generic.List[object]]::new()
    $markdownPattern = '(?<image>!?)\[(?<label>[^\]]*)\]\((?<target>[^)\s]+)(?:\s+["''][^"'']*["''])?\)'
    $referencePattern = '^\s*\[[^\]]+\]:\s*(?<target>\S+)'
    $htmlPattern = '(?<attribute>href|src)\s*=\s*["''](?<target>[^"'']+)["'']'
    $pathPattern = '(?<target>(?:\.{1,2}/|docs/|design/|workspaces/|src/|tests/|scripts/|\.github/|\.azure/)[A-Za-z0-9_./\\%{}() -]+\.(?:md|json|ya?ml|png|txt|csv|xlsx?|xlsm|docx?|cs|cshtml|ps1|bicep)(?:#[A-Za-z0-9_.%:-]+)?)'
    foreach ($relative in $OriginPaths) {
        $allowedRelative = Assert-AllowedDocumentationPath $relative
        $fullPath = Join-Path $root $allowedRelative
        if (-not [System.IO.File]::Exists($fullPath)) {
            continue
        }
        $lineNumber = 0
        foreach ($line in [System.IO.File]::ReadLines($fullPath)) {
            $lineNumber++
            $matches = @()
            foreach ($match in [regex]::Matches($line, $markdownPattern)) {
                $matches += [pscustomobject]@{
                    Syntax = $(if ($match.Groups['image'].Value) { 'md-image' } else { 'md-inline' })
                    Target = $match.Groups['target'].Value
                    Column = $match.Groups['target'].Index + 1
                }
            }
            foreach ($match in [regex]::Matches($line, $referencePattern)) {
                $matches += [pscustomobject]@{
                    Syntax = 'md-reference'
                    Target = $match.Groups['target'].Value
                    Column = $match.Groups['target'].Index + 1
                }
            }
            foreach ($match in [regex]::Matches($line, $htmlPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
                $matches += [pscustomobject]@{
                    Syntax = 'html'
                    Target = $match.Groups['target'].Value
                    Column = $match.Groups['target'].Index + 1
                }
            }
            foreach ($match in [regex]::Matches($line, $pathPattern)) {
                $matches += [pscustomobject]@{
                    Syntax = $(if ([System.IO.Path]::GetExtension($allowedRelative) -eq '.md') { 'code/path-literal' } else { 'source/config-string' })
                    Target = $match.Groups['target'].Value.Trim()
                    Column = $match.Groups['target'].Index + 1
                }
            }
            foreach ($match in $matches | Sort-Object Syntax, Target, Column -Unique) {
                $target = $match.Target.Trim().TrimStart('<').TrimEnd('>').Trim('"').Trim("'")
                $scheme = [uri]::IsWellFormedUriString($target, [System.UriKind]::Absolute)
                $disposition = if ($target -match '^(?i:https?|mailto|tel|file|data):' -or $target.StartsWith('//')) {
                    'retain'
                }
                elseif ((ConvertTo-RepositoryRelativePath $target).StartsWith($documentationExcludedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                    'opaque-excluded'
                }
                else {
                    'unproven'
                }
                $rows.Add([pscustomobject]@{
                    originPath = $allowedRelative
                    line = $lineNumber
                    column = $match.Column
                    syntaxClass = $match.Syntax
                    rawDestination = $target
                    normalizedAllowedDestination = $(if ($disposition -eq 'opaque-excluded' -or $scheme) { $null } else { $target -replace '\\', '/' })
                    disposition = $disposition
                    evidenceState = 'baseline'
                })
            }
        }
    }
    return @($rows)
}

function Get-ConsolidationManifest {
    if (-not $DispositionManifest -or -not [System.IO.File]::Exists($DispositionManifest)) {
        throw 'DispositionManifest is required and must exist.'
    }
    $manifest = Get-Content -LiteralPath $DispositionManifest -Raw | ConvertFrom-Json -Depth 30
    if ($manifest.schemaVersion -ne 2) {
        throw "Disposition manifest schemaVersion must be 2; found $($manifest.schemaVersion)."
    }
    if ($manifest.excludedPrefix -ne $documentationExcludedPrefix) {
        throw "Disposition manifest excludedPrefix must be '$documentationExcludedPrefix'."
    }
    return $manifest
}

function Get-MarkdownSectionEvidence([string]$RelativePath) {
    $text = [System.IO.File]::ReadAllText((Join-Path $root $RelativePath))
    [string[]]$lines = [regex]::Split($text, '\r?\n')
    $headings = [System.Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match '^(?<marks>#{1,6})\s+(?<title>.+?)\s*#*\s*$') {
            $headings.Add([pscustomobject]@{
                Index = $index
                Level = $Matches.marks.Length
                Title = $Matches.title.Trim()
            })
        }
    }
    $titleCounts = @{}
    foreach ($heading in $headings) {
        $titleCounts[$heading.Title] = 1 + [int]$titleCounts[$heading.Title]
    }
    $sections = @{}
    for ($headingIndex = 0; $headingIndex -lt $headings.Count; $headingIndex++) {
        $heading = $headings[$headingIndex]
        if ($titleCounts[$heading.Title] -ne 1) { continue }
        $end = $lines.Count
        for ($next = $headingIndex + 1; $next -lt $headings.Count; $next++) {
            if ($headings[$next].Level -le $heading.Level) {
                $end = $headings[$next].Index
                break
            }
        }
        $section = ([string]::Join("`n", $lines[$heading.Index..($end - 1)])).Trim()
        $sectionBytes = [System.Text.Encoding]::UTF8.GetBytes($section)
        $sections[$heading.Title] =
            [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($sectionBytes)).ToLowerInvariant()
    }
    return $sections
}

function ConvertTo-GitHubHeadingSlug([string]$Heading) {
    $value = [regex]::Replace($Heading, '<[^>]+>', '').Trim().ToLowerInvariant()
    $value = [regex]::Replace($value, '[^\p{L}\p{N}\p{M}\s_–-]', '')
    return $value.Replace(' ', '-')
}

function Assert-DocumentationGraph([string[]]$MarkdownPaths) {
    $markdownSet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $adjacency = @{}
    foreach ($path in $MarkdownPaths | Sort-Object -Unique) {
        $relative = Assert-AllowedDocumentationPath $path
        if (-not $markdownSet.Add($relative)) { continue }
        $adjacency[$relative] = [System.Collections.Generic.HashSet[string]]::new(
            [System.StringComparer]::OrdinalIgnoreCase)
    }

    $immutableBrokenLinks = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    @(
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|../operator-notes/',
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|../product/',
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|../history/product/project-discovery-questionnaire.md',
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|../history/product/feature-versioning-worksheet.md',
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|../product/capabilities.md',
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|../product/index.md',
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|../roadmap.md'
    ) | ForEach-Object { $null = $immutableBrokenLinks.Add($_) }
    $observedExceptions = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    $sectionCache = @{}
    $markdownPattern = '(?<image>!?)\[[^\]]*\]\((?<target><[^>]+>|[^)\s]+)(?:\s+["''][^"'']*["''])?\)'
    $referencePattern = '^\s*\[[^\]]+\]:\s*(?<target>\S+)'
    $htmlPattern = '(?:href|src)\s*=\s*["''](?<target>[^"'']+)["'']'
    $pathPattern = '(?<target>(?:\.{1,2}/|docs/|design/|workspaces/|src/|tests/|scripts/|\.github/|\.azure/)[A-Za-z0-9_./\\%{}() -]+\.md(?:#[A-Za-z0-9_.%:–-]+)?)'
    $codePathPattern = '`(?<target>[^`]+\.md(?:#[A-Za-z0-9_.%:–-]+)?)`'

    foreach ($origin in $markdownSet | Sort-Object) {
        $lineNumber = 0
        foreach ($line in [System.IO.File]::ReadLines((Join-Path $root $origin))) {
            $lineNumber++
            $targets = [System.Collections.Generic.List[object]]::new()
            foreach ($pattern in @($markdownPattern, $referencePattern, $htmlPattern)) {
                foreach ($match in [regex]::Matches($line, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
                    $targets.Add([pscustomobject]@{
                        Raw = $match.Groups['target'].Value.Trim().TrimStart('<').TrimEnd('>')
                        Strict = $true
                        RepositoryRooted = $false
                    })
                }
            }
            foreach ($literalPattern in @($pathPattern, $codePathPattern)) {
                foreach ($match in [regex]::Matches($line, $literalPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
                    $pathLiteral = $match.Groups['target'].Value.Trim()
                    $targets.Add([pscustomobject]@{
                        Raw = $pathLiteral
                        Strict = $false
                        RepositoryRooted = $pathLiteral -match '^(?:docs|design|workspaces|src|tests|scripts|\.github|\.azure)/'
                    })
                }
            }
            foreach ($targetRow in $targets) {
                $rawTarget = [string]$targetRow.Raw
                if ($rawTarget -match '^(?i:https?|mailto|tel|file|data):' -or
                    $rawTarget.StartsWith('//')) {
                    continue
                }
                $targetParts = $rawTarget.Split('#', 2)
                $pathPart = [uri]::UnescapeDataString($targetParts[0]).Replace('\', '/')
                $anchor = if ($targetParts.Count -eq 2) {
                    [uri]::UnescapeDataString($targetParts[1]).ToLowerInvariant()
                } else { $null }
                if (-not $pathPart) {
                    $destination = $origin
                }
                elseif ($pathPart.StartsWith('/') -or $targetRow.RepositoryRooted) {
                    $destination = ConvertTo-RepositoryRelativePath $pathPart.TrimStart('/')
                }
                else {
                    $originDirectory = Split-Path -Parent $origin
                    $combined = Join-Path (Join-Path $root $originDirectory) $pathPart
                    $fullDestination = [System.IO.Path]::GetFullPath($combined)
                    $relativeDestination = [System.IO.Path]::GetRelativePath($root, $fullDestination)
                    if ($relativeDestination.StartsWith('..')) {
                        if (-not $targetRow.Strict) { continue }
                        throw "Local documentation link escapes the repository at ${origin}:$lineNumber -> $rawTarget"
                    }
                    $destination = ConvertTo-RepositoryRelativePath $relativeDestination
                }
                if ($destination.Equals($documentationExcludedPrefix.TrimEnd('/'), [System.StringComparison]::OrdinalIgnoreCase) -or
                    $destination.StartsWith($documentationExcludedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                    continue
                }

                $fullTarget = Join-Path $root $destination
                if ([System.IO.Directory]::Exists($fullTarget)) {
                    $destination = ConvertTo-RepositoryRelativePath (Join-Path $destination 'README.md')
                    $fullTarget = Join-Path $root $destination
                }
                if (-not [System.IO.File]::Exists($fullTarget)) {
                    $exceptionKey = "$origin|$rawTarget"
                    if ($immutableBrokenLinks.Contains($exceptionKey)) {
                        $null = $observedExceptions.Add($exceptionKey)
                        continue
                    }
                    if (-not $targetRow.Strict) { continue }
                    throw "Broken local documentation link at ${origin}:$lineNumber -> $rawTarget ($destination)"
                }
                if ($markdownSet.Contains($destination)) {
                    $null = $adjacency[$origin].Add($destination)
                }
                if ($anchor -and [System.IO.Path]::GetExtension($destination) -eq '.md') {
                    if (-not $sectionCache.ContainsKey($destination)) {
                        $text = [System.IO.File]::ReadAllText($fullTarget)
                        $slugs = [System.Collections.Generic.HashSet[string]]::new(
                            [System.StringComparer]::OrdinalIgnoreCase)
                        $slugCounts = @{}
                        foreach ($match in [regex]::Matches($text, '^(?:#{1,6})\s+(?<title>.+?)\s*#*\s*$', [System.Text.RegularExpressions.RegexOptions]::Multiline)) {
                            $slug = ConvertTo-GitHubHeadingSlug $match.Groups['title'].Value
                            $ordinal = [int]$slugCounts[$slug]
                            $candidate = if ($ordinal -eq 0) { $slug } else { "$slug-$ordinal" }
                            $slugCounts[$slug] = $ordinal + 1
                            $null = $slugs.Add($candidate)
                        }
                        $sectionCache[$destination] = $slugs
                    }
                    if (-not $sectionCache[$destination].Contains($anchor)) {
                        throw "Missing documentation anchor at ${origin}:$lineNumber -> $rawTarget"
                    }
                }
            }
        }
    }
    if ($observedExceptions.Count -ne 7) {
        throw "Expected seven immutable broken Markdown-link exceptions; observed $($observedExceptions.Count)."
    }

    $roots = [System.Collections.Generic.List[string]]::new()
    @('README.md', 'docs/index.md', 'design/README.md', 'workspaces/README.md',
      '.github/pull_request_template.md', 'workspaces/document-extraction/PACKAGE.md') |
        ForEach-Object { $roots.Add($_) }
    foreach ($path in $markdownSet) {
        if ($path -like '.omp/agents/*.md' -or
            $path -eq 'AGENTS.md' -or
            $path.EndsWith('/AGENTS.md', [System.StringComparison]::Ordinal) -or
            $path.EndsWith('/SKILL.md', [System.StringComparison]::Ordinal)) {
            $roots.Add($path)
        }
    }
    $visited = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $queue = [System.Collections.Generic.Queue[string]]::new()
    foreach ($entry in $roots) {
        if ($markdownSet.Contains($entry) -and $visited.Add($entry)) { $queue.Enqueue($entry) }
    }
    while ($queue.Count -gt 0) {
        $current = $queue.Dequeue()
        foreach ($destination in $adjacency[$current]) {
            if ($visited.Add($destination)) { $queue.Enqueue($destination) }
        }
    }
    $unreachable = @($markdownSet | Where-Object { -not $visited.Contains($_) } | Sort-Object)
    if ($unreachable.Count -gt 0) {
        throw "Unreachable retained Markdown: $($unreachable -join ', ')"
    }
    return [pscustomobject]@{
        Markdown = $markdownSet.Count
        ImmutableBrokenLinks = $observedExceptions.Count
    }
}

function Assert-ConsolidationDisposition([object]$Manifest) {
    $baseline = @($Manifest.baseline)
    $creates = @($Manifest.creates)
    if ($baseline.Count -ne 512) {
        throw "Disposition baseline must contain 512 paths; found $($baseline.Count)."
    }
    if ($creates.Count -ne 13) {
        throw "Disposition creates must contain 13 paths; found $($creates.Count)."
    }
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $baseline) {
        $path = Assert-AllowedDocumentationPath $entry.path
        if (-not $seen.Add($path)) {
            throw "Duplicate disposition path: $path"
        }
        if ($entry.action -notin @('K', 'R', 'M', 'D')) {
            throw "Invalid disposition action '$($entry.action)' for $path."
        }
        $targets = @($entry.targets)
        if ($entry.action -eq 'M' -and $targets.Count -ne 1) {
            throw "Move disposition requires one target: $path"
        }
        foreach ($target in $targets) {
            $null = Assert-AllowedDocumentationPath $target
        }
    }
    foreach ($entry in $creates) {
        $path = Assert-AllowedDocumentationPath $entry.path
        if (-not $seen.Add($path)) {
            throw "Create path overlaps another disposition: $path"
        }
    }
    $counts = @{}
    foreach ($action in @('K', 'R', 'M', 'D')) {
        $counts[$action] = @($baseline | Where-Object action -eq $action).Count
    }
    if ($counts.K -ne 131 -or $counts.R -ne 92 -or $counts.M -ne 2 -or $counts.D -ne 287) {
        throw "Disposition action counts must be K=131 R=92 M=2 D=287; found K=$($counts.K) R=$($counts.R) M=$($counts.M) D=$($counts.D)."
    }
}

function Invoke-DocumentationCaptureBaseline {
    $manifest = Get-ConsolidationManifest
    Assert-ConsolidationDisposition $manifest
    $headCommit = (git -C $root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $headCommit -ne $manifest.baseCommit) {
        throw "Baseline HEAD must equal $($manifest.baseCommit); found $headCommit."
    }
    $artifacts = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in $manifest.baseline) {
        $relative = Assert-AllowedDocumentationPath $entry.path
        $fullPath = Join-Path $root $relative
        if (-not [System.IO.File]::Exists($fullPath)) {
            throw "Baseline artifact does not exist: $relative"
        }
        $fileInfo = [System.IO.FileInfo]::new($fullPath)
        $artifacts.Add([pscustomobject]@{
            path = $relative
            byteLength = $fileInfo.Length
            sha256 = Get-FileSha256 $fullPath
            workingTreeMarker = 'pre-consolidation'
        })
    }
    Write-DeterministicJson $BaselineManifest ([ordered]@{
        schemaVersion = 1
        baseCommit = $manifest.baseCommit
        artifacts = @($artifacts)
    })
    $origins = Get-TrackedAllowedTextPaths
    $callsites = Get-DocumentationCallsites $origins
    Write-DeterministicJson $CallsiteInventory ([ordered]@{
        schemaVersion = 1
        baseCommit = $manifest.baseCommit
        excludedPrefix = $documentationExcludedPrefix
        rows = @($callsites)
    })
    Write-Host "Documentation consolidation baseline captured: artifacts=512 excludedOperations=$excludedOperations unknownSyntax=0"
}

function Invoke-DocumentationVerifyHead {
    $manifest = Get-ConsolidationManifest
    Assert-ConsolidationDisposition $manifest
    if (-not $BaselineManifest -or -not [System.IO.File]::Exists($BaselineManifest)) {
        throw 'BaselineManifest is required and must exist.'
    }
    if (-not $MaterialClaimInventory -or -not [System.IO.File]::Exists($MaterialClaimInventory)) {
        throw 'MaterialClaimInventory is required and must exist.'
    }
    $baselineManifestValue = Get-Content -LiteralPath $BaselineManifest -Raw | ConvertFrom-Json -Depth 30
    $baselineByPath = @{}
    foreach ($artifact in @($baselineManifestValue.artifacts)) {
        $baselineByPath[$artifact.path] = $artifact
    }
    if ($baselineByPath.Count -ne 512) {
        throw "Baseline manifest must contain 512 unique paths; found $($baselineByPath.Count)."
    }
    $trackedAllowed = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    $trackedAllowedLines = @(git -C $root ls-files -- . ':(exclude)docs/reference/imp-docs' ':(exclude)docs/reference/imp-docs/**')
    if ($LASTEXITCODE -ne 0) { throw 'Unable to enumerate the allowed Git index.' }
    foreach ($trackedPath in $trackedAllowedLines) {
        $null = $trackedAllowed.Add((Assert-AllowedDocumentationPath $trackedPath))
    }

    $headPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $survivingSourceTargets = @{}
    foreach ($entry in $manifest.baseline) {
        $source = Assert-AllowedDocumentationPath $entry.path
        if ($entry.action -eq 'D') {
            if ($trackedAllowed.Contains($source)) {
                throw "Deleted disposition remains tracked: $source"
            }
            continue
        }
        $target = if ($entry.action -eq 'M') { Assert-AllowedDocumentationPath @($entry.targets)[0] } else { $source }
        if (-not $trackedAllowed.Contains($target) -or
            -not [System.IO.File]::Exists((Join-Path $root $target))) {
            throw "Retained or moved head artifact is not tracked and present: $target"
        }
        $survivingSourceTargets[$source] = $target
        if (-not $headPaths.Add($target)) {
            throw "Duplicate head path: $target"
        }
        if ($entry.action -eq 'K') {
            $currentHash = Get-FileSha256 (Join-Path $root $target)
            if ($currentHash -ne $baselineByPath[$source].sha256) {
                throw "Byte-retained artifact changed: $source"
            }
        }
    }
    foreach ($entry in $manifest.creates) {
        $path = Assert-AllowedDocumentationPath $entry.path
        if (-not $trackedAllowed.Contains($path) -or
            -not [System.IO.File]::Exists((Join-Path $root $path))) {
            throw "Created head artifact is not tracked and present: $path"
        }
        if (-not $headPaths.Add($path)) {
            throw "Duplicate created head path: $path"
        }
    }
    if ($headPaths.Count -ne 238) {
        throw "Head manifest must contain 238 unique paths; found $($headPaths.Count)."
    }

    if (-not $CallsiteInventory -or -not [System.IO.File]::Exists($CallsiteInventory)) {
        throw 'CallsiteInventory is required and must exist.'
    }
    $callsitesValue = Get-Content -LiteralPath $CallsiteInventory -Raw | ConvertFrom-Json -Depth 30
    if ($callsitesValue.schemaVersion -ne 1 -or
        $callsitesValue.excludedPrefix -ne $documentationExcludedPrefix) {
        throw 'Callsite inventory schema or excluded prefix is invalid.'
    }
    $expectedExceptions = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    @(
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|60|md-inline|../operator-notes/',
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|61|md-inline|../product/',
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|62|md-inline|../history/product/project-discovery-questionnaire.md',
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|63|md-inline|../history/product/feature-versioning-worksheet.md',
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|226|md-inline|../product/capabilities.md',
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|226|md-inline|../product/index.md',
        'docs/changes/2026-07-27-azure-workflow-onboarding.md|228|md-inline|../roadmap.md',
        'docs/decisions/ADR-0003-pdfpig-for-first-qdos-slice.md|25|code/path-literal|docs/evaluation/qdos-pdf-engine-benchmark.md'
    ) | ForEach-Object { $null = $expectedExceptions.Add($_) }
    $observedExceptions = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($row in @($callsitesValue.rows)) {
        if ($row.disposition -notin @('retain', 'migrate', 'immutable-baseline')) {
            throw "Unknown callsite disposition at $($row.originPath):$($row.line)."
        }
        if ($row.disposition -eq 'immutable-baseline') {
            $key = "$($row.originPath)|$($row.line)|$($row.syntaxClass)|$($row.rawDestination)"
            if (-not $expectedExceptions.Contains($key)) {
                throw "Unexpected immutable baseline callsite: $key"
            }
            $null = $observedExceptions.Add($key)
            continue
        }
        if ($row.disposition -eq 'migrate' -and
            $survivingSourceTargets.ContainsKey($row.originPath)) {
            $currentOrigin = $survivingSourceTargets[$row.originPath]
            $currentText = [System.IO.File]::ReadAllText((Join-Path $root $currentOrigin))
            if ($currentText.Contains([string]$row.rawDestination) -and
                $row.syntaxClass -in @('code/path-literal', 'source/config-string')) {
                throw "Unmigrated baseline path literal remains in $currentOrigin`: $($row.rawDestination)"
            }
        }
    }
    if ($observedExceptions.Count -ne 8 -or
        $observedExceptions.Count -ne $expectedExceptions.Count) {
        throw "Immutable baseline callsites must match the exact eight exceptions; observed $($observedExceptions.Count)."
    }

    $claimsValue = Get-Content -LiteralPath $MaterialClaimInventory -Raw | ConvertFrom-Json -Depth 40
    if ($claimsValue.schemaVersion -ne 1 -or @($claimsValue.rows).Count -ne 27728) {
        throw "Material claim inventory must contain the exact 27,728-row baseline."
    }
    $claimSectionCache = @{}
    $unassignedClaims = 0
    foreach ($claim in @($claimsValue.rows)) {
        if ($claim.disposition -notin @('preserve-exact', 'merge', 'supersede', 'resolve', 'retain-raw', 'duplicate') -or
            -not $baselineByPath.ContainsKey([string]$claim.sourcePath) -or
            -not $claim.targetPath -or -not $claim.targetHeading -or -not $claim.targetExcerptSha256 -or
            -not $headPaths.Contains([string]$claim.targetPath)) {
            $unassignedClaims++
            continue
        }
        $targetPath = [string]$claim.targetPath
        $actualTargetHash = if ($claim.targetHeading -eq '$blob') {
            Get-FileSha256 (Join-Path $root $targetPath)
        }
        else {
            if (-not $claimSectionCache.ContainsKey($targetPath)) {
                $claimSectionCache[$targetPath] = Get-MarkdownSectionEvidence $targetPath
            }
            if (-not $claimSectionCache[$targetPath].ContainsKey([string]$claim.targetHeading)) {
                $null
            } else {
                $claimSectionCache[$targetPath][[string]$claim.targetHeading]
            }
        }
        if (-not $actualTargetHash -or $actualTargetHash -ne $claim.targetExcerptSha256) {
            $unassignedClaims++
        }
        if ($claim.disposition -eq 'preserve-exact' -and
            ($claim.sourcePath -ne $claim.targetPath -or
             $baselineByPath[[string]$claim.sourcePath].sha256 -ne $actualTargetHash)) {
            $unassignedClaims++
        }
    }
    if ($unassignedClaims -gt 0) {
        throw "Material claim inventory has $unassignedClaims unassigned or unproved rows."
    }

    $markdownPaths = @($headPaths | Where-Object { [System.IO.Path]::GetExtension($_) -eq '.md' })
    $graphEvidence = Assert-DocumentationGraph $markdownPaths
    $headArtifacts = foreach ($path in $headPaths | Sort-Object) {
        $fileInfo = [System.IO.FileInfo]::new((Join-Path $root $path))
        [pscustomobject]@{
            path = $path
            byteLength = $fileInfo.Length
            sha256 = Get-FileSha256 $fileInfo.FullName
            workingTreeMarker = 'consolidated-head'
        }
    }
    Write-DeterministicJson $HeadManifest ([ordered]@{
        schemaVersion = 1
        baseCommit = $manifest.baseCommit
        artifacts = @($headArtifacts)
    })
    Write-Host "Documentation consolidation head verified: baseline=512 delete=287 retainOrMove=225 create=13 head=238 duplicatePairs=2 excludedOperations=$excludedOperations unmappedDeletedReferences=0 activeBrokenLinks=0 activeMissingAnchors=0 unreachableMarkdown=0 unknownSyntax=0 unassignedMaterialClaims=0 baselineExceptionPairs=$($observedExceptions.Count) markdown=$($graphEvidence.Markdown)"
}

if ($DocumentationConsolidationMode) {
    if ($DocumentationConsolidationMode -eq 'CaptureBaseline') {
        Invoke-DocumentationCaptureBaseline
    }
    else {
        Invoke-DocumentationVerifyHead
    }
    return
}

function Add-PolicyError([string]$Message) {
    $errors.Add($Message)
}

$capabilityPath = Join-Path $root 'docs/capabilities.md'
$validCapabilityHorizons = @('Now', 'Next', 'Later', 'Not planned')
$expectedTargetByHorizon = @{
    Now = '0.1.0-alpha.1'
    Next = 'unallocated'
    Later = 'unallocated'
    'Not planned' = 'unallocated'
}
$rows = @(
    foreach ($line in [System.IO.File]::ReadLines($capabilityPath)) {
        $columns = $line -split '\|'
        if ($columns.Count -lt 5) { continue }
        $id = $columns[1].Trim()
        if ($id -notmatch '^[A-Z]+-\d+$') { continue }
        [pscustomobject]@{
            Id = $id
            Horizon = $columns[3].Trim()
            TargetRelease = $columns[4].Trim()
        }
    }
)
foreach ($row in $rows) {
    if ($validCapabilityHorizons -notcontains $row.Horizon) {
        Add-PolicyError "Capability $($row.Id) has unknown horizon '$($row.Horizon)'."
    } elseif ($row.TargetRelease -ne $expectedTargetByHorizon[$row.Horizon]) {
        Add-PolicyError "Capability $($row.Id) target '$($row.TargetRelease)' is invalid for horizon '$($row.Horizon)'; expected '$($expectedTargetByHorizon[$row.Horizon])'."
    }
}

if ($rows.Count -ne 229) {
    Add-PolicyError "Capability inventory must contain 229 rows; found $($rows.Count)."
}
$duplicates = $rows | Group-Object Id | Where-Object Count -ne 1
if ($duplicates) {
    Add-PolicyError "Capability IDs must be unique: $($duplicates.Name -join ', ')."
}
$expectedHorizons = @{ Now = 128; Next = 32; Later = 40; 'Not planned' = 29 }
foreach ($entry in $expectedHorizons.GetEnumerator()) {
    $actual = @($rows | Where-Object Horizon -eq $entry.Key).Count
    if ($actual -ne $entry.Value) {
        Add-PolicyError "Capability horizon '$($entry.Key)' must contain $($entry.Value) rows; found $actual."
    }
}

$canonicalHorizons = @{}
foreach ($row in $rows) {
    $canonicalHorizons[$row.Id] = $row.Horizon
}

$matrixPath = Join-Path $root 'design/product/traceability-matrix.md'
$matrixRows = @(
    foreach ($line in [System.IO.File]::ReadLines($matrixPath)) {
        if ($line -match '^\| (?<id>[A-Z]+-\d+) — .*?\| (?<horizon>[^|]+) \|') {
            $matrixId = $Matches.id
            $matrixHorizonText = $Matches.horizon.Trim()
            $matrixHorizon = switch ($matrixHorizonText) {
                '`0.0.0-development` pre-alpha' { 'Now' }
                '`0.1.0-alpha.1` alpha gate' { 'Now' }
                'pre-`0.1.0-alpha.1` gate' { 'Now' }
                '`Next`/`unallocated`' { 'Next' }
                '`Next`/`unallocated`; precedes INT-05–INT-07 within the horizon' { 'Next' }
                '`Next` / unallocated' { 'Next' }
                '`Later`/`unallocated` release work' { 'Later' }
                '`Later` / unallocated' { 'Later' }
                '`Not planned`' { 'Not planned' }
                default {
                    Add-PolicyError "Traceability matrix capability $matrixId has unknown horizon '$matrixHorizonText'."
                    $null
                }
            }
            [pscustomobject]@{ Id = $matrixId; Horizon = $matrixHorizon }
        }
    }
)
if ($matrixRows.Count -ne 229) {
    Add-PolicyError "Traceability matrix must contain 229 capability rows; found $($matrixRows.Count)."
}
$matrixDuplicates = $matrixRows | Group-Object Id | Where-Object Count -ne 1
if ($matrixDuplicates) {
    Add-PolicyError "Traceability matrix capability IDs must be unique: $($matrixDuplicates.Name -join ', ')."
}
$matrixIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($matrixRow in $matrixRows) {
    $null = $matrixIds.Add($matrixRow.Id)
}
$missingMatrixIds = $canonicalHorizons.Keys | Where-Object { -not $matrixIds.Contains($_) } | Sort-Object
if ($missingMatrixIds) {
    Add-PolicyError "Traceability matrix is missing canonical capability IDs: $($missingMatrixIds -join ', ')."
}
foreach ($matrixRow in $matrixRows) {
    if (-not $canonicalHorizons.ContainsKey($matrixRow.Id)) {
        Add-PolicyError "Traceability matrix contains unknown capability ID: $($matrixRow.Id)."
    } elseif ($canonicalHorizons[$matrixRow.Id] -ne $matrixRow.Horizon) {
        Add-PolicyError "Capability horizon mismatch for $($matrixRow.Id): inventory=$($canonicalHorizons[$matrixRow.Id]), matrix=$($matrixRow.Horizon)."
    }
}

$forbiddenPaths = @(
    'CollisionSpike.slnx',
    'src/CollisionSpike.Core',
    'src/CollisionSpike.Infrastructure',
    'src/CollisionSpike.Web',
    'src/CollisionSpike.Worker',
    'tests/CollisionSpike.Core.Tests',
    'tests/CollisionSpike.IntegrationTests',
    'tests/CollisionSpike.ArchitectureTests',
    'docs/README.md',
    'docs/product',
    'docs/history',
    'docs/runbooks',
    'docs/agent-guidance',
    'docs/agent-notes',
    'docs/evaluation',
    'docs/operator-notes',
    'PROJECT_DISCOVERY_QUESTIONNAIRE.md',
    'FEATURE_VERSIONING.md',
    'INITIAL_SCAFFOLD_PLAN.md',
    'REPOSITORY_STRUCTURE_PLAN.md'
)
foreach ($relative in $forbiddenPaths) {
    if (Test-Path -LiteralPath (Join-Path $root $relative)) {
        Add-PolicyError "Obsolete path remains: $relative"
    }
}

$activeRoots = @(
    'README.md', 'AGENTS.md', 'Directory.Build.props', 'Pegasus.slnx',
    'package.json', 'package-lock.json', 'azure.yaml', '.azure', '.github',
    'design', 'infra', 'scripts', 'src', 'tests', 'workspaces',
    'docs/index.md', 'docs/requirements.md', 'docs/capabilities.md',
    'docs/open-decisions.md', 'docs/architecture.md', 'docs/operations.md',
    'docs/engineering.md', 'docs/operator-notes.md', 'docs/agent-mistakes.md',
    'docs/changes', 'docs/decisions', 'docs/azure',
    'docs/reference/README.md',
    'docs/reference/eva_information/eva_information.md'
)
$textExtensions = @('.md', '.txt', '.yml', '.yaml', '.json', '.jsonc', '.ps1', '.py', '.bicep', '.csproj', '.slnx', '.cs', '.cshtml', '.props', '.targets')
$activeFiles = foreach ($relative in $activeRoots) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $item = Get-Item -LiteralPath $path
    if (-not $item.PSIsContainer) { $item; continue }
    Get-ChildItem -LiteralPath $path -Recurse -File | Where-Object {
        $textExtensions -contains $_.Extension -and
        $_.FullName -notmatch '[\\/](?:bin|obj|node_modules|artifacts|corpus)[\\/]'
    }
}

$currentDocumentationOwners = @(
    'docs/index.md', 'docs/requirements.md', 'docs/capabilities.md',
    'docs/open-decisions.md', 'docs/architecture.md', 'docs/operations.md',
    'docs/engineering.md', 'docs/operator-notes.md'
)
foreach ($ownerPath in $currentDocumentationOwners) {
    if (-not [System.IO.File]::Exists((Join-Path $root $ownerPath))) {
        Add-PolicyError "Canonical documentation owner is missing: $ownerPath"
    }
}

$policyMarkdownPaths = @(
    git -C $root ls-files -- '*.md' ':(exclude)docs/reference/imp-docs' ':(exclude)docs/reference/imp-docs/**'
)
if ($LASTEXITCODE -ne 0) {
    Add-PolicyError 'Unable to enumerate tracked Markdown for documentation graph validation.'
}
else {
    try {
        $policyGraphEvidence = Assert-DocumentationGraph $policyMarkdownPaths
    }
    catch {
        Add-PolicyError $_.Exception.Message
    }
}

$evergreenCountPattern = '(?i)\b(?:512|238|274)\s+(?:artifact|file|document)s?\b'
foreach ($ownerPath in $currentDocumentationOwners) {
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines((Join-Path $root $ownerPath))) {
        $lineNumber++
        if ($line -match $evergreenCountPattern) {
            Add-PolicyError "Temporary consolidation census leaked into canonical owner at ${ownerPath}:$lineNumber"
        }
    }
}

$authorityBoundary = 'This package may produce evidence, candidates, or draft output only. `Pegasus.Core` and an authorised human own every accepted case fact, cost, category, outcome, legal position, and approval.'
$authoritySkills = @(
    'vehicle-assessment', 'total-loss-assessment', 'salvage-categorisation',
    'roadworthy-report', 'manufacturer-methods-evidence', 'diminution-report',
    'diminution-rebuttal', 'ce-cost-defence'
)
foreach ($skillName in $authoritySkills) {
    $skillPath = Join-Path $root "workspaces/ai-centre/skills/$skillName/SKILL.md"
    $skillText = [System.IO.File]::ReadAllText($skillPath)
    if (([regex]::Matches($skillText, [regex]::Escape($authorityBoundary))).Count -ne 1 -or
        $skillText -notmatch '(?m)^## Authority boundary\s*$') {
        Add-PolicyError "AI skill authority boundary is missing or duplicated: $skillName"
    }
}

$historicalOrTechnicalIdentity = '(?i)(predecessor|pre-cutover|legacy|historical|former|old application|rg-collisionspike-dev|collisionengineers/collisionspike_v2|CollisionSpikeCurrenttree|collisionspike-corpus-evaluation|ASP-rgcollisionspikedev|cespk-pg-dev|databases:.*collisionspike)'
$technicalVersionContext = '(?i)(schema|engine|API|token|taxonomy|storage|MSAL|package|version|filename|\.pdf|allocation evidence|normalized.*evidence|Feature maturity|provider-domains-v1|cedocumentmapper_v2|baseline-v2|v2\.0|engine-v2|v[12] webhooks|webhooks v2|names such as)'
$corruptedTechnicalHorizon = '(?i)(?:MSAL Browser|Rules Engine|taxonomy|access tokens?|general-purpose|engine-ready|QRD)\s+`(?:Next|Later)`/`unallocated`|`(?:Next|Later)`/`unallocated`\s+(?:access tokens?|schema|engine|storage)'
$obsoleteAllocationLanguage = '(?:^\s*#{1,6}\s+(?:Never|Conditional\s*/\s*Unclear)\s*$)|(?:\bEvery Never feature\b)|(?:\*\*(?:Never|Unclear):\*\*)'

foreach ($file in $activeFiles | Sort-Object FullName -Unique) {
    if ($file.FullName -eq $PSCommandPath) { continue }
    $relative = [System.IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        $lineNumber++

        if ($line -match '(?i)Pegasus\s+`(?:Next|Later)`/`unallocated`') {
            Add-PolicyError "Release horizon used as Pegasus product identity at ${relative}:$lineNumber"
        }
        if ($line -match $corruptedTechnicalHorizon) {
            Add-PolicyError "Release horizon replaced a technical version at ${relative}:$lineNumber"
        }
        if ($line -cmatch $obsoleteAllocationLanguage) {
            Add-PolicyError "Obsolete allocation label at ${relative}:$lineNumber"
        }
        if ($line -match 'CollisionSpike') {
            $isAllowedRetainedIdentity =
                ($relative -eq 'docs/decisions/ADR-0007-repository-local-codex-planning-plugin-boundaries.md' -and
                    $line -cmatch 'Owners: Alex and the CollisionSpike v2 development team') -or
                ($relative -eq 'docs/azure/current-inventory.md' -and
                    $line -cmatch 'no CollisionSpike ownership established')
            if ($relative.StartsWith('workspaces/', [System.StringComparison]::Ordinal) -or
                $isAllowedRetainedIdentity -or $line -match $historicalOrTechnicalIdentity) {
                $allowedLanguageMatches.Add("Allowed historical/technical identity at ${relative}:$lineNumber")
            } else {
                Add-PolicyError "Obsolete active product identity at ${relative}:$lineNumber"
            }
        }
        if ($file.Extension -in @('.md', '.txt', '.yml', '.yaml') -and
            $line -cmatch '\b(?:V0|V1(?:\.x)?|V2|V3\+?|first-MVP)\b') {
            if ($relative.StartsWith('workspaces/', [System.StringComparison]::Ordinal) -or
                $line -match $technicalVersionContext) {
                $allowedLanguageMatches.Add("Allowed technical/evidence version at ${relative}:$lineNumber")
            } else {
                Add-PolicyError "Obsolete release-stage prose at ${relative}:$lineNumber"
            }
        }
    }
}

$allowedLanguageMatches | Sort-Object -Unique | ForEach-Object { Write-Host $_ }

$solution = Get-Content -LiteralPath (Join-Path $root 'Pegasus.slnx') -Raw
if ($solution -match 'workspaces[/\\]') {
    Add-PolicyError 'Pegasus.slnx must not include a source workspace.'
}
$applicationProjects = Get-ChildItem -LiteralPath (Join-Path $root 'src'), (Join-Path $root 'tests') -Recurse -Filter '*.csproj' -File
$applicationBuildFiles = @($applicationProjects) + @(
    Get-ChildItem -LiteralPath (Join-Path $root 'src'), (Join-Path $root 'tests') -Recurse -File -Include '*.props', '*.targets'
)
foreach ($buildFile in $applicationBuildFiles | Sort-Object FullName -Unique) {
    if ((Get-Content -LiteralPath $buildFile.FullName -Raw) -match 'workspaces[/\\]') {
        $relativeBuildFile = [System.IO.Path]::GetRelativePath($root, $buildFile.FullName).Replace('\', '/')
        Add-PolicyError "Application MSBuild configuration references a source workspace: $relativeBuildFile"
    }
}

$trackedIndexLines = @(git -C $root ls-files --stage -- . ':(exclude)docs/reference/imp-docs/**')
if ($LASTEXITCODE -ne 0) {
    Add-PolicyError 'Unable to enumerate the Git index.'
}
$trackedIndexEntries = @(
    foreach ($indexLine in $trackedIndexLines) {
        if ($indexLine -notmatch '^(?<mode>\d{6}) (?<object>[0-9a-f]+) \d+\t(?<path>.+)$') {
            Add-PolicyError "Unable to parse Git index entry: $indexLine"
            continue
        }
        [pscustomobject]@{
            Mode = $Matches.mode
            ObjectId = $Matches.object
            Path = $Matches.path.Replace('\', '/')
        }
    }
)

$trackedCorpusPaths = @(
    $trackedIndexEntries |
        Where-Object Path -match '(^|/)corpus(?:/|$)' |
        ForEach-Object Path
)
if ($trackedCorpusPaths.Count -gt 0) {
    Add-PolicyError "Protected corpus material is tracked: $($trackedCorpusPaths -join ', ')."
}

$workspaceEntries = @(
    $trackedIndexEntries | Where-Object Path -like 'workspaces/*'
)
$workspaceGitlinks = @(
    $workspaceEntries | Where-Object Mode -eq '160000' | ForEach-Object Path
)
if ($workspaceGitlinks.Count -gt 0) {
    Add-PolicyError "Workspace gitlinks are forbidden: $($workspaceGitlinks -join ', ')."
}

$forbiddenWorkspaceNames = @('.git', 'node_modules', '.venv', '.pytest_cache', '__pycache__', '_dev', 'dist', 'coverage', 'artifacts', 'outputs', 'runs', 'checkpoints', 'sample-doc-files')
$forbiddenWorkspaceExtensions = @('.zip', '.skill', '.nupkg', '.snupkg', '.pt', '.pth', '.onnx', '.safetensors', '.ckpt')
foreach ($entry in $workspaceEntries) {
    $relative = $entry.Path
    $segments = $relative -split '/'
    $name = [System.IO.Path]::GetFileName($relative)
    $extension = [System.IO.Path]::GetExtension($relative)
    if (($segments | Where-Object { $forbiddenWorkspaceNames -contains $_ }).Count -gt 0 -or
        $forbiddenWorkspaceExtensions -contains $extension) {
        Add-PolicyError "Forbidden generated/private workspace material: $relative"
    }
    $isAllowedDevReferenceAgent =
        $name -eq 'AGENTS.md' -and
        $relative -like 'workspaces/ai-centre/skills/dev-ref/*/AGENTS.md'
    if ($name -in @('AGENTS.md', 'CLAUDE.md') -and
        $relative -ne 'workspaces/AGENTS.md' -and -not $isAllowedDevReferenceAgent) {
        Add-PolicyError "Upstream agent instruction remains in workspace: $relative"
    }
}

function Get-WorkspaceManifest([string]$Prefix, [scriptblock]$Exclude) {
    [string[]]$relativePaths = @(
        foreach ($entry in $workspaceEntries) {
            if ($entry.Mode -eq '160000' -or
                -not $entry.Path.StartsWith($Prefix, [System.StringComparison]::Ordinal)) {
                continue
            }
            $relative = $entry.Path.Substring($Prefix.Length)
            if (-not (& $Exclude $relative)) { $relative }
        }
    )
    [Array]::Sort($relativePaths, [System.StringComparer]::Ordinal)

    $hash = [System.Security.Cryptography.IncrementalHash]::CreateHash(
        [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    [long]$totalBytes = 0
    foreach ($relative in $relativePaths) {
        $fullPath = Join-Path $root ($Prefix + $relative)
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            Add-PolicyError "Tracked workspace path is absent from the working tree: $($Prefix + $relative)"
            continue
        }
        $pathBytes = [System.Text.Encoding]::UTF8.GetBytes($relative)
        $contentBytes = [System.IO.File]::ReadAllBytes($fullPath)
        $hash.AppendData($pathBytes)
        $hash.AppendData($contentBytes)
        $totalBytes += $contentBytes.LongLength
    }

    [pscustomobject]@{
        Files = $relativePaths.Count
        Bytes = $totalBytes
        Hash = [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
    }
}

$workspaceManifestRows = @{}
$workspaceReadmePath = Join-Path $root 'workspaces/README.md'
foreach ($line in [System.IO.File]::ReadLines($workspaceReadmePath)) {
    if ($line -match '^\| `(?<workspace>[^`]+/)` .* \| (?<files>\d+) files, (?<bytes>[\d,]+) bytes, SHA-256 `(?<hash>[0-9a-f]{64})` \|$') {
        $workspaceManifestRows[$Matches.workspace] = [pscustomobject]@{
            Files = [int]$Matches.files
            Bytes = [long]($Matches.bytes -replace ',', '')
            Hash = $Matches.hash
        }
    }
}

$cacheOrBuildNames = @('.git', 'node_modules', '.venv', '.pytest_cache', '__pycache__', 'dist', 'coverage', 'artifacts', 'outputs', 'runs', 'checkpoints')
$manifestDefinitions = @(
    [pscustomobject]@{
        Name = 'document-extraction/'
        Prefix = 'workspaces/document-extraction/'
        Exclude = { param($relative) $false }
    },
    [pscustomobject]@{
        Name = 'report-renderer/'
        Prefix = 'workspaces/report-renderer/'
        Exclude = { param($relative) $false }
    },
    [pscustomobject]@{
        Name = 'ai-centre/'
        Prefix = 'workspaces/ai-centre/'
        Exclude = {
            param($relative)
            $segments = $relative -split '/'
            $relative.StartsWith('skills/', [System.StringComparison]::Ordinal) -or
                $relative.StartsWith('ml-ops/data/', [System.StringComparison]::Ordinal) -or
                ($segments | Where-Object { $_ -eq '.github' -or $cacheOrBuildNames -contains $_ }).Count -gt 0
        }
    },
    [pscustomobject]@{
        Name = 'ai-centre/skills/'
        Prefix = 'workspaces/ai-centre/skills/'
        Exclude = {
            param($relative)
            $segments = $relative -split '/'
            ($segments | Where-Object { $_ -eq '.github' -or $cacheOrBuildNames -contains $_ }).Count -gt 0 -or
                $relative.StartsWith('assets/style-examples/', [System.StringComparison]::Ordinal) -or
                $relative.StartsWith('fixtures/style-examples/', [System.StringComparison]::Ordinal)
        }
    }
)
foreach ($definition in $manifestDefinitions) {
    if (-not $workspaceManifestRows.ContainsKey($definition.Name)) {
        Add-PolicyError "Missing workspace manifest row for $($definition.Name)."
        continue
    }
    $expected = $workspaceManifestRows[$definition.Name]
    $actual = Get-WorkspaceManifest $definition.Prefix $definition.Exclude
    if ($actual.Files -ne $expected.Files -or
        $actual.Bytes -ne $expected.Bytes -or
        $actual.Hash -ne $expected.Hash) {
        Add-PolicyError "Workspace manifest mismatch for $($definition.Name): expected $($expected.Files) files/$($expected.Bytes) bytes/$($expected.Hash), actual $($actual.Files) files/$($actual.Bytes) bytes/$($actual.Hash)."
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

Write-Host "Repository policy passed: 229 unique capabilities (Now 128, Next 32, Later 40, Not planned 29), Pegasus identity, Semantic Version language, and non-caller workspace boundaries."
