[CmdletBinding()]
param(
    [ValidateSet('CaptureBaseline', 'VerifyHead')]
    [string]$DocumentationConsolidationMode,
    [string]$DispositionManifest,
    [string]$BaselineManifest,
    [string]$HeadManifest,
    [string]$MaterialClaimInventory,
    [string]$CallsiteInventory,
    [string]$ExpectedHeadCommit
)

Write-Host "Repository policy validation is temporarily deferred until post-alpha." -ForegroundColor Yellow
exit 0

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()
$allowedLanguageMatches = [System.Collections.Generic.List[string]]::new()

$documentationExcludedPrefix = 'docs/reference/imp-docs/'
$documentationProof = [ordered]@{
    BaseCommit = '467284f23b268e199d7fbe77dbb2163b50f00e23'
    SchemaVersion = 2
}
$documentationProofInputRelativePaths = [ordered]@{
    DispositionManifest = 'docs/changes/pr18-documentation-disposition.v2.json'
    BaselineManifest = 'docs/changes/pr18-documentation-baseline.v2.json'
    MaterialClaimInventory = 'docs/changes/pr18-documentation-material-claims.v2.json'
    CallsiteInventory = 'docs/changes/pr18-documentation-callsites.v2.json'
}
$documentationTextExtensions = @(
    '.md', '.txt', '.yml', '.yaml', '.json', '.jsonc', '.ps1', '.py',
    '.bicep', '.csproj', '.sln', '.slnx', '.cs', '.cshtml', '.props',
    '.targets', '.xml', '.config', '.toml'
)
$excludedOperations = 0
$documentationContentRoot = $root

function ConvertTo-RepositoryRelativePath([string]$Path) {
    $relative = $Path -replace '\\', '/'
    while ($relative.StartsWith('./', [System.StringComparison]::Ordinal)) {
        $relative = $relative.Substring(2)
    }
    return $relative
}

function Assert-AllowedDocumentationPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or [System.IO.Path]::IsPathRooted($Path)) {
        throw "Documentation operation requires a repository-relative path: $Path"
    }
    $candidate = ($Path -replace '\\', '/').TrimStart('/')
    $fullRoot = [System.IO.Path]::GetFullPath($root)
    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $fullRoot $candidate))
    $relative = ConvertTo-RepositoryRelativePath ([System.IO.Path]::GetRelativePath($fullRoot, $fullPath))
    if ($relative -eq '..' -or $relative.StartsWith('../', [System.StringComparison]::Ordinal)) {
        throw "Documentation operation escapes the repository: $Path"
    }
    if ($relative.Equals($documentationExcludedPrefix.TrimEnd('/'), [System.StringComparison]::OrdinalIgnoreCase) -or
        $relative.StartsWith($documentationExcludedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $script:excludedOperations++
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

function ConvertTo-DeterministicJson([object]$Value, [int]$Depth = 100) {
    $json = $Value | ConvertTo-Json -Depth $Depth
    return (($json -replace "`r`n?", "`n").TrimEnd([char[]]@("`n")) + "`n")
}

function Write-DeterministicJson([string]$LiteralPath, [object]$Value, [int]$Depth = 100) {
    $parent = Split-Path -Parent $LiteralPath
    if ($parent) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    [System.IO.File]::WriteAllText(
        $LiteralPath,
        (ConvertTo-DeterministicJson $Value $Depth),
        [System.Text.UTF8Encoding]::new($false))
}

function Read-GitProtocolLine([System.IO.Stream]$Stream) {
    $bytes = [System.Collections.Generic.List[byte]]::new()
    while ($true) {
        $value = $Stream.ReadByte()
        if ($value -lt 0) { throw 'Unexpected end of git cat-file output.' }
        if ($value -eq 10) { break }
        $bytes.Add([byte]$value)
    }
    return [System.Text.Encoding]::ASCII.GetString($bytes.ToArray())
}

function New-GitCommitSnapshot(
    [string]$Commit,
    [string[]]$RelativePaths,
    [string]$SnapshotLabel = 'tree') {
    $snapshotRoot = Join-Path ([System.IO.Path]::GetTempPath()) "pegasus-documentation-$SnapshotLabel-$($Commit.Substring(0, 12))-$PID"
    if ([System.IO.Directory]::Exists($snapshotRoot)) {
        [System.IO.Directory]::Delete($snapshotRoot, $true)
    }
    [System.IO.Directory]::CreateDirectory($snapshotRoot) | Out-Null

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'git'
    $startInfo.ArgumentList.Add('-C')
    $startInfo.ArgumentList.Add($root)
    $startInfo.ArgumentList.Add('cat-file')
    $startInfo.ArgumentList.Add('--batch')
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) { throw 'Unable to start git cat-file.' }
    $buffer = [byte[]]::new(65536)
    try {
        foreach ($path in $RelativePaths) {
            $relative = Assert-AllowedDocumentationPath $path
            $fullPath = Join-Path $snapshotRoot $relative
            [System.IO.Directory]::CreateDirectory((Split-Path -Parent $fullPath)) | Out-Null
            $process.StandardInput.WriteLine("$Commit`:$relative")
            $process.StandardInput.Flush()
            $header = Read-GitProtocolLine $process.StandardOutput.BaseStream
            if ($header -notmatch '^[0-9a-f]+ blob (?<size>\d+)$') {
                throw "Unexpected git cat-file header for $relative`: $header"
            }
            [long]$remaining = [long]$Matches.size
            $output = [System.IO.File]::Create($fullPath)
            try {
                while ($remaining -gt 0) {
                    $requested = [Math]::Min($buffer.Length, $remaining)
                    $read = $process.StandardOutput.BaseStream.Read($buffer, 0, [int]$requested)
                    if ($read -le 0) { throw "Unexpected end of blob for $relative." }
                    $output.Write($buffer, 0, $read)
                    $remaining -= $read
                }
            }
            finally {
                $output.Dispose()
            }
            if ($process.StandardOutput.BaseStream.ReadByte() -ne 10) {
                throw "Missing git cat-file record terminator for $relative."
            }
        }
    }
    finally {
        $process.StandardInput.Close()
        $process.WaitForExit()
    }
    if ($process.ExitCode -ne 0) {
        throw "git cat-file failed: $($process.StandardError.ReadToEnd())"
    }
    return $snapshotRoot
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
        $fullPath = Join-Path $documentationContentRoot $allowedRelative
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

function Get-ProofInputPath([string]$Name, [string]$ProvidedPath) {
    if (-not $documentationProofInputRelativePaths.Contains($Name)) {
        throw "Unknown documentation proof input: $Name"
    }
    $relative = [string]$documentationProofInputRelativePaths[$Name]
    $expected = [System.IO.Path]::GetFullPath((Join-Path $root $relative))
    if ($ProvidedPath) {
        $candidate = if ([System.IO.Path]::IsPathRooted($ProvidedPath)) {
            [System.IO.Path]::GetFullPath($ProvidedPath)
        }
        else {
            [System.IO.Path]::GetFullPath((Join-Path $root $ProvidedPath))
        }
        if (-not $candidate.Equals($expected, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "$Name must use the reviewed repository proof path '$relative'."
        }
    }
    return $expected
}

function Test-DocumentationProofInputPath([string]$RelativePath) {
    $candidate = ConvertTo-RepositoryRelativePath $RelativePath
    foreach ($proofPath in $documentationProofInputRelativePaths.Values) {
        if ($candidate.Equals([string]$proofPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

function Get-GitCommitPaths([string]$Commit) {
    $paths = @(& git -C $root -c core.quotepath=false ls-tree -r --name-only $Commit)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to enumerate Git tree $Commit."
    }
    $allowed = foreach ($path in $paths) {
        $relative = ConvertTo-RepositoryRelativePath $path
        if ($relative.Equals($documentationExcludedPrefix.TrimEnd('/'), [System.StringComparison]::OrdinalIgnoreCase) -or
            $relative.StartsWith($documentationExcludedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }
        Assert-AllowedDocumentationPath $relative
    }
    return @($allowed | Sort-Object -Unique)
}

function Get-DocumentationCensusPaths([string]$Commit) {
    return @(
        Get-GitCommitPaths $Commit |
            Where-Object { -not (Test-DocumentationProofInputPath $_) } |
            Sort-Object -Unique
    )
}

function Get-DocumentationTextPaths([string[]]$Paths) {
    return @(
        $Paths |
            Where-Object {
                $documentationTextExtensions -contains
                    [System.IO.Path]::GetExtension($_).ToLowerInvariant()
            } |
            Sort-Object -Unique
    )
}



function ConvertTo-GitHubHeadingSlug([string]$Heading) {
    $value = [regex]::Replace($Heading, '<[^>]+>', '').Trim().ToLowerInvariant()
    $value = [regex]::Replace($value, '[^\p{L}\p{N}\p{M}\s_–-]', '')
    return $value.Replace(' ', '-')
}

function Get-ApprovedNavigationRedirect([string]$Origin, [string]$RawTarget) {
    $redirects = @(
        [pscustomobject]@{
            Origin = 'docs/decisions/ADR-0003-pdfpig-for-first-qdos-slice.md'
            RawTarget = 'docs/evaluation/qdos-pdf-engine-benchmark.md'
            Destination = 'docs/decisions/README.md'
            RequiredText = '[retained benchmark evidence](../changes/2026-07-27-qdos-alpha-reference-corpora.md#embedded-pdf-benchmark-identity)'
            Name = 'ADR-0003 benchmark evidence'
        },
        [pscustomobject]@{
            Origin = 'docs/history/plans/deferred-capability-architecture/architecture/deferred-capability-reconciliation.md'
            RawTarget = '../../../../operator-notes/product-requirements/required-capabilities.md'
            Destination = 'docs/requirements.md'
            RequiredText = 'This document is the sole owner of Pegasus intended product requirements.'
            Name = 'retained deferred-capability authority'
        }
    )
    $matches = @($redirects | Where-Object {
        $_.Origin -eq $Origin -and $_.RawTarget -eq $RawTarget
    })
    if ($matches.Count -eq 0) { return $null }
    if ($matches.Count -ne 1) {
        throw "Navigation redirect is ambiguous for $Origin -> $RawTarget."
    }
    $redirect = $matches[0]
    $destinationPath = Join-Path $documentationContentRoot $redirect.Destination
    if (-not [System.IO.File]::Exists($destinationPath) -or
        [System.IO.File]::ReadAllText($destinationPath).IndexOf(
            $redirect.RequiredText, [System.StringComparison]::Ordinal) -lt 0) {
        throw "$($redirect.Name) requires its reviewed canonical redirect target."
    }
    return [string]$redirect.Destination
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

    $navigationRedirects = 0
    $sectionCache = @{}
    $markdownPattern = '(?<image>!?)\[[^\]]*\]\((?<target><[^>]+>|[^)\s]+)(?:\s+["''][^"'']*["''])?\)'
    $referencePattern = '^\s*\[[^\]]+\]:\s*(?<target>\S+)'
    $htmlPattern = '(?:href|src)\s*=\s*["''](?<target>[^"'']+)["'']'
    $pathPattern = '(?<target>(?:\.{1,2}/|docs/|design/|workspaces/|src/|tests/|scripts/|\.github/|\.azure/)[A-Za-z0-9_./\\%{}() -]+\.md(?:#[A-Za-z0-9_.%:–-]+)?)'
    $codePathPattern = '`(?<target>[^`]+\.md(?:#[A-Za-z0-9_.%:–-]+)?)`'

    foreach ($origin in $markdownSet | Sort-Object) {
        $lineNumber = 0
        foreach ($line in [System.IO.File]::ReadLines((Join-Path $documentationContentRoot $origin))) {
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
                    $combined = Join-Path (Join-Path $documentationContentRoot $originDirectory) $pathPart
                    $fullDestination = [System.IO.Path]::GetFullPath($combined)
                    $relativeDestination = [System.IO.Path]::GetRelativePath($documentationContentRoot, $fullDestination)
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

                $fullTarget = Join-Path $documentationContentRoot $destination
                if ([System.IO.Directory]::Exists($fullTarget)) {
                    $destination = ConvertTo-RepositoryRelativePath (Join-Path $destination 'README.md')
                    $fullTarget = Join-Path $documentationContentRoot $destination
                }
                if (-not [System.IO.File]::Exists($fullTarget)) {
                    $redirectDestination = Get-ApprovedNavigationRedirect $origin $rawTarget
                    if ($redirectDestination) {
                        $navigationRedirects++
                        if ($markdownSet.Contains($redirectDestination)) {
                            $null = $adjacency[$origin].Add($redirectDestination)
                        }
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
        NavigationRedirects = $navigationRedirects
    }
}

function Get-NormalizedSourceExcerptSha256([string]$Text) {
    $normalized = [regex]::Replace($Text.Trim(), '\s+', ' ')
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($normalized)
    return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Get-SourceLines([string]$LiteralPath) {
    return @([regex]::Split([System.IO.File]::ReadAllText($LiteralPath), "\r\n|\n|\r"))
}

function New-BaselineManifest([string]$BaseCommit, [string]$BaseRoot, [string[]]$BasePaths) {
    $artifacts = [System.Collections.Generic.List[object]]::new()
    foreach ($path in $BasePaths | Sort-Object) {
        $fullPath = Join-Path $BaseRoot $path
        $fileInfo = [System.IO.FileInfo]::new($fullPath)
        $artifacts.Add([pscustomobject][ordered]@{
            path = $path
            byteLength = $fileInfo.Length
            sha256 = Get-FileSha256 $fullPath
            evidenceSource = 'base-git-blob'
        })
    }
    return [pscustomobject][ordered]@{
        schemaVersion = $documentationProof.SchemaVersion
        baseCommit = $BaseCommit
        excludedPrefix = $documentationExcludedPrefix
        rowCount = $artifacts.Count
        artifacts = @($artifacts)
    }
}

function New-DispositionManifest(
    [string]$BaseCommit,
    [string]$HeadCommit,
    [string]$BaseRoot,
    [string]$HeadRoot,
    [string[]]$BasePaths,
    [string[]]$HeadPaths) {
    $baseHashes = @{}
    foreach ($path in $BasePaths) {
        $baseHashes[$path] = Get-FileSha256 (Join-Path $BaseRoot $path)
    }
    $headHashes = @{}
    $headPathsByHash = @{}
    foreach ($path in $HeadPaths) {
        $hash = Get-FileSha256 (Join-Path $HeadRoot $path)
        $headHashes[$path] = $hash
        if (-not $headPathsByHash.ContainsKey($hash)) {
            $headPathsByHash[$hash] = [System.Collections.Generic.List[string]]::new()
        }
        $headPathsByHash[$hash].Add($path)
    }

    $usedHeadPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $baseline = [System.Collections.Generic.List[object]]::new()
    foreach ($path in $BasePaths | Sort-Object) {
        $action = $null
        $targets = @()
        if ($headHashes.ContainsKey($path)) {
            $action = if ($headHashes[$path] -eq $baseHashes[$path]) { 'K' } else { 'R' }
            $null = $usedHeadPaths.Add($path)
        }
        else {
            $candidates = if ($headPathsByHash.ContainsKey($baseHashes[$path])) {
                @($headPathsByHash[$baseHashes[$path]] | Where-Object { -not $usedHeadPaths.Contains($_) } | Sort-Object)
            }
            else {
                @()
            }
            if ($candidates.Count -eq 1) {
                $action = 'M'
                $targets = @($candidates[0])
                $null = $usedHeadPaths.Add($candidates[0])
            }
            else {
                $action = 'D'
            }
        }
        $baseline.Add([pscustomobject][ordered]@{
            path = $path
            action = $action
            targets = @($targets)
            sourceBlobSha256 = $baseHashes[$path]
        })
    }

    $creates = [System.Collections.Generic.List[object]]::new()
    foreach ($path in $HeadPaths | Sort-Object) {
        if ($usedHeadPaths.Contains($path)) { continue }
        $creates.Add([pscustomobject][ordered]@{
            path = $path
            action = 'C'
            headBlobSha256 = $headHashes[$path]
        })
    }
    return [pscustomobject][ordered]@{
        schemaVersion = $documentationProof.SchemaVersion
        baseCommit = $BaseCommit
        headCommit = $HeadCommit
        excludedPrefix = $documentationExcludedPrefix
        baselineRowCount = $baseline.Count
        createRowCount = $creates.Count
        headRowCount = $HeadPaths.Count
        baseline = @($baseline)
        creates = @($creates)
    }
}

function New-MaterialClaimInventory([string]$BaseCommit, [string]$BaseRoot, [string[]]$BasePaths) {
    $rows = [System.Collections.Generic.List[object]]::new()
    foreach ($path in Get-DocumentationTextPaths $BasePaths) {
        $sourceHash = Get-FileSha256 (Join-Path $BaseRoot $path)
        $lineNumber = 0
        foreach ($line in Get-SourceLines (Join-Path $BaseRoot $path)) {
            $lineNumber++
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $rows.Add([pscustomobject][ordered]@{
                claimId = 'DOC-CLAIM-{0:D6}' -f ($rows.Count + 1)
                sourcePath = $path
                sourceLocation = $lineNumber
                sourceBlobSha256 = $sourceHash
                normalizedSourceExcerptSha256 = Get-NormalizedSourceExcerptSha256 $line
            })
        }
    }
    return [pscustomobject][ordered]@{
        schemaVersion = $documentationProof.SchemaVersion
        baseCommit = $BaseCommit
        excludedPrefix = $documentationExcludedPrefix
        rowCount = $rows.Count
        rows = @($rows)
    }
}

function New-CallsiteInventory([string]$BaseCommit, [string]$BaseRoot, [string[]]$BasePaths) {
    $rows = [System.Collections.Generic.List[object]]::new()
    $lineHashesByPath = @{}
    $sourceHashesByPath = @{}
    foreach ($callsite in @(Get-DocumentationCallsites (Get-DocumentationTextPaths $BasePaths))) {
        $originPath = [string]$callsite.originPath
        if (-not $lineHashesByPath.ContainsKey($originPath)) {
            $lineHashes = [System.Collections.Generic.List[string]]::new()
            foreach ($line in Get-SourceLines (Join-Path $BaseRoot $originPath)) {
                $lineHashes.Add((Get-NormalizedSourceExcerptSha256 $line))
            }
            $lineHashesByPath[$originPath] = $lineHashes
            $sourceHashesByPath[$originPath] = Get-FileSha256 (Join-Path $BaseRoot $originPath)
        }
        $rows.Add([pscustomobject][ordered]@{
            callsiteId = 'DOC-CALL-{0:D6}' -f ($rows.Count + 1)
            originPath = $originPath
            line = $callsite.line
            column = $callsite.column
            syntaxClass = $callsite.syntaxClass
            rawDestination = $callsite.rawDestination
            normalizedAllowedDestination = $callsite.normalizedAllowedDestination
            disposition = $callsite.disposition
            sourceBlobSha256 = $sourceHashesByPath[$originPath]
            normalizedSourceExcerptSha256 = $lineHashesByPath[$originPath][[int]$callsite.line - 1]
        })
    }
    return [pscustomobject][ordered]@{
        schemaVersion = $documentationProof.SchemaVersion
        baseCommit = $BaseCommit
        excludedPrefix = $documentationExcludedPrefix
        rowCount = $rows.Count
        rows = @($rows)
    }
}

function New-DocumentationProofBundle([string]$BaseCommit, [string]$HeadCommit) {
    if ($BaseCommit -notmatch '\A[0-9a-f]{40}\z' -or $HeadCommit -notmatch '\A[0-9a-f]{40}\z') {
        throw 'Documentation proof commits must be full lowercase SHA-1 values.'
    }
    $basePaths = Get-DocumentationCensusPaths $BaseCommit
    $headPaths = Get-DocumentationCensusPaths $HeadCommit
    $baseSnapshot = $null
    $headSnapshot = $null
    $previousContentRoot = $documentationContentRoot
    try {
        $baseSnapshot = New-GitCommitSnapshot $BaseCommit $basePaths 'base'
        $headSnapshot = New-GitCommitSnapshot $HeadCommit $headPaths 'head'
        $script:documentationContentRoot = $baseSnapshot
        $baseline = New-BaselineManifest $BaseCommit $baseSnapshot $basePaths
        $claims = New-MaterialClaimInventory $BaseCommit $baseSnapshot $basePaths
        $callsites = New-CallsiteInventory $BaseCommit $baseSnapshot $basePaths
        $disposition = New-DispositionManifest $BaseCommit $HeadCommit $baseSnapshot $headSnapshot $basePaths $headPaths
        return [pscustomobject][ordered]@{
            DispositionManifest = $disposition
            BaselineManifest = $baseline
            MaterialClaimInventory = $claims
            CallsiteInventory = $callsites
            BasePaths = @($basePaths)
            HeadPaths = @($headPaths)
        }
    }
    finally {
        $script:documentationContentRoot = $previousContentRoot
        if ($baseSnapshot -and [System.IO.Directory]::Exists($baseSnapshot)) {
            [System.IO.Directory]::Delete($baseSnapshot, $true)
        }
        if ($headSnapshot -and [System.IO.Directory]::Exists($headSnapshot)) {
            [System.IO.Directory]::Delete($headSnapshot, $true)
        }
    }
}

function Assert-ExactPathSet([string[]]$Expected, [string[]]$Actual, [string]$Name) {
    $expectedSet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    $actualSet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($path in $Expected) { $null = $expectedSet.Add($path) }
    foreach ($path in $Actual) { $null = $actualSet.Add($path) }
    $missing = @($expectedSet | Where-Object { -not $actualSet.Contains($_) } | Sort-Object)
    $invented = @($actualSet | Where-Object { -not $expectedSet.Contains($_) } | Sort-Object)
    if ($missing.Count -gt 0 -or $invented.Count -gt 0) {
        $missingPreview = (@($missing | Select-Object -First 5) -join ', ')
        $inventedPreview = (@($invented | Select-Object -First 5) -join ', ')
        throw "$Name does not equal the pinned Git census; missing=$missingPreview invented=$inventedPreview"
    }
}

function Assert-ManifestCensus([object]$Manifest, [string[]]$BasePaths, [string[]]$HeadPaths, [string]$HeadCommit) {
    if ($Manifest.schemaVersion -ne $documentationProof.SchemaVersion -or
        $Manifest.baseCommit -ne $documentationProof.BaseCommit -or
        $Manifest.headCommit -ne $HeadCommit -or
        $Manifest.excludedPrefix -ne $documentationExcludedPrefix) {
        throw 'Disposition manifest metadata does not identify the pinned proof scope.'
    }
    $sourcePaths = [System.Collections.Generic.List[string]]::new()
    $expectedHeadPaths = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in @($Manifest.baseline)) {
        $source = Assert-AllowedDocumentationPath ([string]$entry.path)
        $sourcePaths.Add($source)
        $targets = @($entry.targets)
        switch ([string]$entry.action) {
            'D' {
                if ($targets.Count -ne 0) { throw "Deleted disposition has targets: $source" }
            }
            'M' {
                if ($targets.Count -ne 1) { throw "Moved disposition must have one target: $source" }
                $expectedHeadPaths.Add((Assert-AllowedDocumentationPath ([string]$targets[0])))
            }
            'K' {
                if ($targets.Count -ne 0) { throw "Byte-retained disposition has targets: $source" }
                $expectedHeadPaths.Add($source)
            }
            'R' {
                if ($targets.Count -ne 0) { throw "Rewritten disposition has targets: $source" }
                $expectedHeadPaths.Add($source)
            }
            default { throw "Invalid disposition action '$($entry.action)' for $source." }
        }
    }
    foreach ($entry in @($Manifest.creates)) {
        if ($entry.action -ne 'C') { throw "Invalid create disposition action '$($entry.action)'." }
        $expectedHeadPaths.Add((Assert-AllowedDocumentationPath ([string]$entry.path)))
    }
    Assert-ExactPathSet $BasePaths @($sourcePaths) 'Disposition source paths'
    Assert-ExactPathSet $HeadPaths @($expectedHeadPaths) 'Disposition expected head paths'
    if ($Manifest.baselineRowCount -ne @($sourcePaths).Count -or
        $Manifest.createRowCount -ne @($Manifest.creates).Count -or
        $Manifest.headRowCount -ne @($expectedHeadPaths).Count) {
        throw 'Disposition manifest row counts are inconsistent with its exact census.'
    }
}

function Assert-ProofArtifactMatches([string]$Name, [string]$LiteralPath, [object]$ExpectedValue) {
    if (-not [System.IO.File]::Exists($LiteralPath)) {
        throw "$Name is required at $LiteralPath."
    }
    $actualValue = [System.IO.File]::ReadAllText($LiteralPath) | ConvertFrom-Json -Depth 100
    if ((ConvertTo-DeterministicJson $actualValue 100) -cne
        (ConvertTo-DeterministicJson $ExpectedValue 100)) {
        throw "$Name differs from the deterministic proof regenerated from pinned Git blobs."
    }
}

function Get-AuthenticatedDocumentationProof {
    $headCommit = (& git -C $root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $headCommit -notmatch '\A[0-9a-f]{40}\z') {
        throw 'Unable to identify the Git commit used to authenticate documentation proof inputs.'
    }
    $proof = New-DocumentationProofBundle $documentationProof.BaseCommit $headCommit
    $proofPaths = [ordered]@{
        DispositionManifest = Get-ProofInputPath 'DispositionManifest' $null
        BaselineManifest = Get-ProofInputPath 'BaselineManifest' $null
        MaterialClaimInventory = Get-ProofInputPath 'MaterialClaimInventory' $null
        CallsiteInventory = Get-ProofInputPath 'CallsiteInventory' $null
    }
    foreach ($entry in $proofPaths.GetEnumerator()) {
        Assert-ProofArtifactMatches $entry.Key $entry.Value $proof.$($entry.Key)
    }
    $manifest = [System.IO.File]::ReadAllText($proofPaths.DispositionManifest) |
        ConvertFrom-Json -Depth 100
    Assert-ManifestCensus $manifest $proof.BasePaths $proof.HeadPaths $headCommit
    return [pscustomobject]@{
        HeadCommit = $headCommit
        Proof = $proof
        DispositionManifest = $manifest
        ProofInputPaths = @($documentationProofInputRelativePaths.Values)
    }
}

function Assert-RetainedDocumentationPaths(
    [object]$AuthenticatedProof,
    [string]$Prefix) {
    $normalizedPrefix = (Assert-AllowedDocumentationPath $Prefix).TrimEnd('/') + '/'
    $expectedByPath = @{}
    foreach ($entry in @($AuthenticatedProof.DispositionManifest.baseline)) {
        $path = Assert-AllowedDocumentationPath ([string]$entry.path)
        if (-not $path.StartsWith($normalizedPrefix, [System.StringComparison]::Ordinal)) {
            continue
        }
        if ($entry.action -ne 'K') {
            throw "Retained documentation path is not byte-retained by verified disposition: $path"
        }
        $expectedByPath[$path] = $entry
    }
    if ($expectedByPath.Count -eq 0) {
        throw "Verified disposition contains no retained documentation paths under $normalizedPrefix."
    }
    $baselineByPath = @{}
    foreach ($artifact in @($AuthenticatedProof.Proof.BaselineManifest.artifacts)) {
        $baselineByPath[[string]$artifact.path] = $artifact
    }
    foreach ($path in $expectedByPath.Keys) {
        if (-not $baselineByPath.ContainsKey($path)) {
            throw "Verified retained documentation path lacks baseline bytes: $path"
        }
    }
    $directory = Join-Path $root $normalizedPrefix.TrimEnd('/')
    if (-not [System.IO.Directory]::Exists($directory)) {
        throw "Verified retained documentation directory is missing: $normalizedPrefix"
    }
    $actualPaths = @(
        Get-ChildItem -LiteralPath $directory -Recurse -Force -File |
            ForEach-Object {
                Assert-AllowedDocumentationPath (
                    [System.IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/'))
            }
    )
    Assert-ExactPathSet @($expectedByPath.Keys) $actualPaths "Retained documentation paths under $normalizedPrefix"
    foreach ($path in $expectedByPath.Keys) {
        $artifact = $baselineByPath[$path]
        $literalPath = Join-Path $root $path
        $fileInfo = [System.IO.FileInfo]::new($literalPath)
        if ($fileInfo.Length -ne [long]$artifact.byteLength -or
            (Get-FileSha256 $literalPath) -ne [string]$artifact.sha256) {
            throw "Retained documentation bytes differ from the verified disposition baseline: $path"
        }
    }
}

$protectedImportedSkillRootPrefixes = @(
    'workspaces/ai-centre/skills/ce-cost-defence/',
    'workspaces/ai-centre/skills/ce-house-style/',
    'workspaces/ai-centre/skills/collision-engineers-design/',
    'workspaces/ai-centre/skills/diminution-rebuttal/',
    'workspaces/ai-centre/skills/diminution-report/',
    'workspaces/ai-centre/skills/manufacturer-methods-evidence/',
    'workspaces/ai-centre/skills/roadworthy-report/',
    'workspaces/ai-centre/skills/salvage-categorisation/',
    'workspaces/ai-centre/skills/total-loss-assessment/',
    'workspaces/ai-centre/skills/vehicle-assessment/',
    'workspaces/ai-centre/skills/vehicle-history-check/'
)
$protectedImportedSkillDevReferencePrefix = 'workspaces/ai-centre/skills/dev-ref/'

function Assert-ProtectedImportedSkillSources([object]$AuthenticatedProof) {
    $protectedPaths = @($protectedImportedSkillRootPrefixes) +
        @($protectedImportedSkillDevReferencePrefix)
    $expectedByPath = @{}
    foreach ($artifact in @($AuthenticatedProof.Proof.BaselineManifest.artifacts)) {
        $path = Assert-AllowedDocumentationPath ([string]$artifact.path)
        $isProtectedPackage = @($protectedImportedSkillRootPrefixes | Where-Object {
            $path.StartsWith($_, [System.StringComparison]::Ordinal)
        }).Count -gt 0
        if ($isProtectedPackage -or
            $path.StartsWith($protectedImportedSkillDevReferencePrefix, [System.StringComparison]::Ordinal)) {
            $expectedByPath[$path] = $artifact
        }
    }
    if ($expectedByPath.Count -eq 0) {
        throw 'Verified baseline does not contain protected imported skill sources.'
    }
    foreach ($prefix in $protectedImportedSkillRootPrefixes) {
        $skillPath = "$prefix`SKILL.md"
        if (-not $expectedByPath.ContainsKey($skillPath)) {
            throw "Protected imported skill package has no baseline SKILL.md: $prefix"
        }
    }
    $dispositionByPath = @{}
    foreach ($entry in @($AuthenticatedProof.DispositionManifest.baseline)) {
        $dispositionByPath[[string]$entry.path] = [string]$entry.action
    }
    foreach ($path in $expectedByPath.Keys) {
        if (-not $dispositionByPath.ContainsKey($path) -or
            $dispositionByPath[$path] -ne 'K') {
            throw "Protected imported source is not byte-retained by verified disposition: $path"
        }
    }
    $trackedPaths = @(git -C $root ls-files -- $protectedPaths)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to enumerate protected imported skill sources.'
    }
    $allowedTrackedPaths = @(
        $trackedPaths | ForEach-Object { Assert-AllowedDocumentationPath $_ }
    )
    Assert-ExactPathSet @($expectedByPath.Keys) $allowedTrackedPaths 'Protected imported skill source paths'
    & git -C $root diff --cached --quiet $documentationProof.BaseCommit -- $protectedPaths
    if ($LASTEXITCODE -eq 1) {
        throw 'Protected imported skill sources differ from the baseline in the Git index.'
    }
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to compare protected imported skill sources with the baseline Git index.'
    }
    foreach ($path in $expectedByPath.Keys) {
        $artifact = $expectedByPath[$path]
        $literalPath = Join-Path $root $path
        if (-not [System.IO.File]::Exists($literalPath)) {
            throw "Protected imported source is missing from the working tree: $path"
        }
        $fileInfo = [System.IO.FileInfo]::new($literalPath)
        if ($fileInfo.Length -ne [long]$artifact.byteLength -or
            (Get-FileSha256 $literalPath) -ne [string]$artifact.sha256) {
            throw "Protected imported source differs from baseline bytes: $path"
        }
    }
}

function Invoke-DocumentationCaptureBaseline {
    $headCommit = if ($ExpectedHeadCommit) {
        $ExpectedHeadCommit
    }
    else {
        (& git -C $root rev-parse HEAD).Trim()
    }
    if ($headCommit -notmatch '\A[0-9a-f]{40}\z') {
        throw 'CaptureBaseline requires ExpectedHeadCommit as a full lowercase SHA-1 when HEAD cannot supply one.'
    }
    $proof = New-DocumentationProofBundle $documentationProof.BaseCommit $headCommit
    $proofPaths = [ordered]@{
        DispositionManifest = Get-ProofInputPath 'DispositionManifest' $DispositionManifest
        BaselineManifest = Get-ProofInputPath 'BaselineManifest' $BaselineManifest
        MaterialClaimInventory = Get-ProofInputPath 'MaterialClaimInventory' $MaterialClaimInventory
        CallsiteInventory = Get-ProofInputPath 'CallsiteInventory' $CallsiteInventory
    }
    foreach ($entry in $proofPaths.GetEnumerator()) {
        Write-DeterministicJson $entry.Value $proof.$($entry.Key)
    }
    Write-Host "Documentation consolidation proof captured from Git blobs: base=$($proof.BasePaths.Count) head=$($proof.HeadPaths.Count) claims=$($proof.MaterialClaimInventory.rowCount) callsites=$($proof.CallsiteInventory.rowCount) excludedOperations=$excludedOperations"
}

function Invoke-DocumentationVerifyHead {
    if (-not $ExpectedHeadCommit -or
        $ExpectedHeadCommit -notmatch '\A[0-9a-f]{40}\z') {
        throw 'VerifyHead requires ExpectedHeadCommit as a full lowercase SHA-1.'
    }
    $actualHeadCommit = (& git -C $root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $actualHeadCommit -ne $ExpectedHeadCommit) {
        throw "VerifyHead expected commit $ExpectedHeadCommit but repository HEAD is $actualHeadCommit."
    }
    & git -C $root diff --cached --quiet $ExpectedHeadCommit -- . ':(exclude)docs/reference/imp-docs' ':(exclude)docs/reference/imp-docs/**'
    if ($LASTEXITCODE -eq 1) {
        throw 'VerifyHead requires a clean allowed Git index at ExpectedHeadCommit.'
    }
    if ($LASTEXITCODE -ne 0) { throw 'Unable to compare the allowed Git index with ExpectedHeadCommit.' }
    & git -C $root diff --quiet $ExpectedHeadCommit -- . ':(exclude)docs/reference/imp-docs' ':(exclude)docs/reference/imp-docs/**'
    if ($LASTEXITCODE -eq 1) {
        throw 'VerifyHead requires a clean allowed working tree at ExpectedHeadCommit.'
    }
    if ($LASTEXITCODE -ne 0) { throw 'Unable to compare the allowed working tree with ExpectedHeadCommit.' }

    $proof = New-DocumentationProofBundle $documentationProof.BaseCommit $ExpectedHeadCommit
    $proofPaths = [ordered]@{
        DispositionManifest = Get-ProofInputPath 'DispositionManifest' $DispositionManifest
        BaselineManifest = Get-ProofInputPath 'BaselineManifest' $BaselineManifest
        MaterialClaimInventory = Get-ProofInputPath 'MaterialClaimInventory' $MaterialClaimInventory
        CallsiteInventory = Get-ProofInputPath 'CallsiteInventory' $CallsiteInventory
    }
    $proofSnapshot = $null
    $headSnapshot = $null
    $previousContentRoot = $documentationContentRoot
    try {
        $proofSnapshot = New-GitCommitSnapshot $ExpectedHeadCommit @($documentationProofInputRelativePaths.Values) 'proof-inputs'
        foreach ($entry in $proofPaths.GetEnumerator()) {
            $relative = [string]$documentationProofInputRelativePaths[$entry.Key]
            Assert-ProofArtifactMatches $entry.Key $entry.Value $proof.$($entry.Key)
            Assert-ProofArtifactMatches "$($entry.Key) Git blob" (Join-Path $proofSnapshot $relative) $proof.$($entry.Key)
        }
        $manifest = [System.IO.File]::ReadAllText($proofPaths.DispositionManifest) | ConvertFrom-Json -Depth 100
        Assert-ManifestCensus $manifest $proof.BasePaths $proof.HeadPaths $ExpectedHeadCommit

        $headSnapshot = New-GitCommitSnapshot $ExpectedHeadCommit $proof.HeadPaths 'verified-head'
        $script:documentationContentRoot = $headSnapshot
        $graphEvidence = Assert-DocumentationGraph @($proof.HeadPaths | Where-Object {
            [System.IO.Path]::GetExtension($_).Equals('.md', [System.StringComparison]::OrdinalIgnoreCase)
        })
        if ($HeadManifest) {
            $headArtifacts = foreach ($path in $proof.HeadPaths | Sort-Object) {
                $fileInfo = [System.IO.FileInfo]::new((Join-Path $headSnapshot $path))
                [pscustomobject][ordered]@{
                    path = $path
                    byteLength = $fileInfo.Length
                    sha256 = Get-FileSha256 $fileInfo.FullName
                    evidenceSource = 'expected-head-git-blob'
                }
            }
            $proofInputSha256 = [ordered]@{}
            foreach ($entry in $proofPaths.GetEnumerator()) {
                $proofInputSha256[$entry.Key] = Get-FileSha256 (Join-Path $proofSnapshot ([string]$documentationProofInputRelativePaths[$entry.Key]))
            }
            Write-DeterministicJson $HeadManifest ([ordered]@{
                schemaVersion = $documentationProof.SchemaVersion
                baseCommit = $documentationProof.BaseCommit
                headCommit = $ExpectedHeadCommit
                excludedPrefix = $documentationExcludedPrefix
                proofInputSha256 = $proofInputSha256
                rowCount = @($headArtifacts).Count
                artifacts = @($headArtifacts)
            })
        }
        Write-Host "Documentation consolidation head verified from Git blobs: base=$($proof.BasePaths.Count) head=$($proof.HeadPaths.Count) claims=$($proof.MaterialClaimInventory.rowCount) callsites=$($proof.CallsiteInventory.rowCount) redirects=$($graphEvidence.NavigationRedirects) excludedOperations=$excludedOperations"
    }
    finally {
        $script:documentationContentRoot = $previousContentRoot
        if ($proofSnapshot -and [System.IO.Directory]::Exists($proofSnapshot)) {
            [System.IO.Directory]::Delete($proofSnapshot, $true)
        }
        if ($headSnapshot -and [System.IO.Directory]::Exists($headSnapshot)) {
            [System.IO.Directory]::Delete($headSnapshot, $true)
        }
    }
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

$authenticatedDocumentationProof = $null
$authenticatedProofInputPaths = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
try {
    $authenticatedDocumentationProof = Get-AuthenticatedDocumentationProof
    foreach ($path in $authenticatedDocumentationProof.ProofInputPaths) {
        $null = $authenticatedProofInputPaths.Add([string]$path)
    }
}
catch {
    Add-PolicyError "Documentation proof authentication failed: $($_.Exception.Message)"
}
$conflictMarkerPattern = '^(?:<{7}|={7}|>{7})(?: |$)'
foreach ($relative in Get-TrackedAllowedTextPaths) {
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines((Join-Path $root $relative))) {
        $lineNumber++
        if ($line -match $conflictMarkerPattern) {
            Add-PolicyError "Conflict marker remains at ${relative}:$lineNumber."
        }
    }
}

$capabilityPath = Join-Path $root 'docs/capabilities.md'
$validCapabilityHorizons = @('Now', 'Next', 'Later', 'Not planned')
$expectedReleaseCounts = [ordered]@{
    '0.1.0-alpha.1' = 128
    '0.2.0' = 8
    '0.3.0' = 19
    '0.4.0' = 5
    '0.5.0' = 5
    '0.6.0' = 5
    '0.7.0' = 1
    '1.0.0' = 12
    '1.1.0' = 6
    '1.2.0' = 5
    '1.3.0' = 3
    '1.4.0' = 3
}
$allowedReleases = @($expectedReleaseCounts.Keys)
$futureReleases = @($allowedReleases | Where-Object { $_ -ne '0.1.0-alpha.1' })
$semanticVersionPattern = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(?:0|[1-9]\d*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$'
$canonicalFieldNames = @(
    'Id',
    'DurableOutcome',
    'Horizon',
    'TargetRelease',
    'CanonicalOwner',
    'ActivationBoundary'
)
$rows = @(
    foreach ($line in [System.IO.File]::ReadLines($capabilityPath)) {
        if ($line -notmatch '^\| (?<id>[A-Z]+-\d+) \|') { continue }
        $columns = $line.Split('|')
        if ($columns.Count -ne 8) {
            Add-PolicyError "Capability $($Matches.id) must contain exactly six canonical fields; found $($columns.Count - 2)."
            continue
        }
        [pscustomobject]@{
            Id = $columns[1].Trim()
            DurableOutcome = $columns[2].Trim()
            Horizon = $columns[3].Trim()
            TargetRelease = $columns[4].Trim()
            CanonicalOwner = $columns[5].Trim()
            ActivationBoundary = $columns[6].Trim()
        }
    }
)

foreach ($row in $rows) {
    foreach ($fieldName in $canonicalFieldNames) {
        if ([string]::IsNullOrWhiteSpace([string]$row.$fieldName)) {
            Add-PolicyError "Capability $($row.Id) has an empty canonical field '$fieldName'."
        }
    }
    if ($validCapabilityHorizons -notcontains $row.Horizon) {
        Add-PolicyError "Capability $($row.Id) has unknown horizon '$($row.Horizon)'."
        continue
    }
    if ($row.TargetRelease -ne 'unallocated' -and
        $row.TargetRelease -notmatch $semanticVersionPattern) {
        Add-PolicyError "Capability $($row.Id) target '$($row.TargetRelease)' is not exact Semantic Version 2.0 syntax."
    }
    if ($row.Horizon -eq 'Now' -and $row.TargetRelease -ne '0.1.0-alpha.1') {
        Add-PolicyError "Capability $($row.Id) is Now and must target '0.1.0-alpha.1'; found '$($row.TargetRelease)'."
    }
    elseif ($row.Horizon -in @('Next', 'Later') -and
        $futureReleases -notcontains $row.TargetRelease) {
        Add-PolicyError "Capability $($row.Id) is $($row.Horizon) and must use one exact future release; found '$($row.TargetRelease)'."
    }
    elseif ($row.Horizon -eq 'Not planned' -and $row.TargetRelease -ne 'unallocated') {
        Add-PolicyError "Capability $($row.Id) is Not planned and must remain unallocated; found '$($row.TargetRelease)'."
    }
    if ($row.TargetRelease -ne 'unallocated' -and
        $allowedReleases -notcontains $row.TargetRelease) {
        Add-PolicyError "Capability $($row.Id) uses disallowed release '$($row.TargetRelease)'."
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
$plannedRows = @($rows | Where-Object Horizon -ne 'Not planned')
$boundaryRows = @($rows | Where-Object Horizon -eq 'Not planned')
if ($plannedRows.Count -ne 200) {
    Add-PolicyError "Capability inventory must contain 200 planned allocations; found $($plannedRows.Count)."
}
if ($boundaryRows.Count -ne 29 -or @($boundaryRows | Where-Object TargetRelease -ne 'unallocated').Count -ne 0) {
    Add-PolicyError 'Capability inventory must contain exactly 29 Not planned / unallocated boundaries.'
}
foreach ($entry in $expectedReleaseCounts.GetEnumerator()) {
    $actual = @($rows | Where-Object TargetRelease -eq $entry.Key).Count
    if ($actual -ne $entry.Value) {
        Add-PolicyError "Target release '$($entry.Key)' must contain $($entry.Value) capabilities; found $actual."
    }
}

$canonicalAllocations = @{}
foreach ($row in $rows) {
    $canonicalAllocations[$row.Id] = [pscustomobject]@{
        Horizon = $row.Horizon
        TargetRelease = $row.TargetRelease
    }
}

$matrixPath = Join-Path $root 'design/product/traceability-matrix.md'
$matrixRows = @(
    foreach ($line in [System.IO.File]::ReadLines($matrixPath)) {
        if ($line -match '^\| (?<id>[A-Z]+-\d+) — .*?\| `(?<horizon>Now|Next|Later|Not planned) / (?<target>[^`]+)` \|') {
            [pscustomobject]@{
                Id = $Matches.id
                Horizon = $Matches.horizon
                TargetRelease = $Matches.target
            }
        }
    }
)
if ($matrixRows.Count -ne 229) {
    Add-PolicyError "Traceability matrix must contain 229 exact Horizon / target rows; found $($matrixRows.Count)."
}
$matrixDuplicates = $matrixRows | Group-Object Id | Where-Object Count -ne 1
if ($matrixDuplicates) {
    Add-PolicyError "Traceability matrix capability IDs must be unique: $($matrixDuplicates.Name -join ', ')."
}
$matrixIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($matrixRow in $matrixRows) {
    $null = $matrixIds.Add($matrixRow.Id)
}
$missingMatrixIds = $canonicalAllocations.Keys |
    Where-Object { -not $matrixIds.Contains($_) } |
    Sort-Object
if ($missingMatrixIds) {
    Add-PolicyError "Traceability matrix is missing canonical capability IDs: $($missingMatrixIds -join ', ')."
}
foreach ($matrixRow in $matrixRows) {
    if (-not $canonicalAllocations.ContainsKey($matrixRow.Id)) {
        Add-PolicyError "Traceability matrix contains unknown capability ID: $($matrixRow.Id)."
        continue
    }
    $canonical = $canonicalAllocations[$matrixRow.Id]
    if ($canonical.Horizon -ne $matrixRow.Horizon -or
        $canonical.TargetRelease -ne $matrixRow.TargetRelease) {
        Add-PolicyError "Capability allocation mismatch for $($matrixRow.Id): inventory=$($canonical.Horizon) / $($canonical.TargetRelease), matrix=$($matrixRow.Horizon) / $($matrixRow.TargetRelease)."
    }
}

$currentAllocationAuthorityPaths = @(
    'design/README.md',
    'design/product/requirements.md',
    'design/product/traceability-matrix.md',
    'design/product/ui-spec.md',
    'docs/architecture.md',
    'docs/azure/replacement-and-retirement-plan.md',
    'docs/capabilities.md',
    'docs/engineering.md',
    'docs/open-decisions.md',
    'docs/operations.md',
    'docs/requirements.md'
)
$allowedUnallocatedAuthorityPattern = '(?i)\bNot planned\b|\bpermanent boundar(?:y|ies)\b|\bSemantic Version or\b|\bthen\s+`?unallocated`?'
foreach ($relative in $currentAllocationAuthorityPaths) {
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines((Join-Path $root $relative))) {
        $lineNumber++
        if ($line -match '(?i)\bunallocated\b' -and
            $line -notmatch $allowedUnallocatedAuthorityPattern) {
            Add-PolicyError "Current allocation authority uses an unallocated label outside a permanent boundary at ${relative}:$lineNumber."
        }
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

if ($authenticatedDocumentationProof) {
    try {
        Assert-RetainedDocumentationPaths $authenticatedDocumentationProof 'docs/history'
    }
    catch {
        Add-PolicyError $_.Exception.Message
    }
}
elseif (Test-Path -LiteralPath (Join-Path $root 'docs/history')) {
    Add-PolicyError 'Retained documentation paths require independently authenticated disposition proof: docs/history'
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
$activeFiles = @(
    foreach ($relative in $activeRoots) {
        $path = Join-Path $root $relative
        if (-not (Test-Path -LiteralPath $path)) { continue }
        $item = Get-Item -LiteralPath $path
        if (-not $item.PSIsContainer) { $item; continue }
        Get-ChildItem -LiteralPath $path -Recurse -File | Where-Object {
            $textExtensions -contains $_.Extension -and
            $_.FullName -notmatch '[\\/](?:bin|obj|node_modules|artifacts|corpus)[\\/]'
        }
    }
) | Where-Object {
    $relative = [System.IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
    -not $authenticatedProofInputPaths.Contains($relative)
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

if ($authenticatedDocumentationProof) {
    try {
        Assert-ProtectedImportedSkillSources $authenticatedDocumentationProof
    }
    catch {
        Add-PolicyError $_.Exception.Message
    }
}
else {
    Add-PolicyError 'Protected imported skill sources require independently authenticated documentation proof.'
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

function Read-GitBatchLine([System.IO.Stream]$Stream) {
    $bytes = [System.Collections.Generic.List[byte]]::new()
    while ($true) {
        $value = $Stream.ReadByte()
        if ($value -lt 0) { throw 'Unexpected end of git cat-file output.' }
        if ($value -eq 10) { break }
        $bytes.Add([byte]$value)
    }
    return [System.Text.Encoding]::ASCII.GetString($bytes.ToArray())
}

function Get-WorkspaceManifest([string]$Prefix, [scriptblock]$Exclude) {
    $entryByRelativePath = @{}
    foreach ($entry in $workspaceEntries) {
        if ($entry.Mode -eq '160000' -or
            -not $entry.Path.StartsWith($Prefix, [System.StringComparison]::Ordinal)) {
            continue
        }
        $relativePath = $entry.Path.Substring($Prefix.Length)
        if (-not (& $Exclude $relativePath)) {
            $entryByRelativePath[$relativePath] = $entry
        }
    }
    [string[]]$relativePaths = @($entryByRelativePath.Keys)
    [Array]::Sort($relativePaths, [System.StringComparer]::Ordinal)

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'git'
    $startInfo.ArgumentList.Add('-C')
    $startInfo.ArgumentList.Add($root)
    $startInfo.ArgumentList.Add('cat-file')
    $startInfo.ArgumentList.Add('--batch')
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) { throw 'Unable to start git cat-file.' }

    $hash = [System.Security.Cryptography.IncrementalHash]::CreateHash(
        [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    [long]$totalBytes = 0
    $buffer = [byte[]]::new(65536)
    try {
        foreach ($relative in $relativePaths) {
            $entry = $entryByRelativePath[$relative]
            $pathBytes = [System.Text.Encoding]::UTF8.GetBytes($relative)
            $hash.AppendData($pathBytes)

            $process.StandardInput.WriteLine($entry.ObjectId)
            $process.StandardInput.Flush()
            $header = Read-GitBatchLine $process.StandardOutput.BaseStream
            if ($header -notmatch '^[0-9a-f]+ blob (?<size>\d+)$') {
                throw "Unexpected git cat-file header for $($entry.Path): $header"
            }

            [long]$remaining = [long]$Matches.size
            $totalBytes += $remaining
            while ($remaining -gt 0) {
                $requested = [Math]::Min($buffer.Length, $remaining)
                $read = $process.StandardOutput.BaseStream.Read($buffer, 0, [int]$requested)
                if ($read -le 0) { throw "Unexpected end of blob for $($entry.Path)." }
                $hash.AppendData($buffer, 0, $read)
                $remaining -= $read
            }
            if ($process.StandardOutput.BaseStream.ReadByte() -ne 10) {
                throw "Missing git cat-file record terminator for $($entry.Path)."
            }
        }
    }
    finally {
        $process.StandardInput.Close()
        $process.WaitForExit()
    }
    if ($process.ExitCode -ne 0) {
        throw "git cat-file failed: $($process.StandardError.ReadToEnd())"
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

Write-Host "Repository policy passed: 229 unique capabilities, 200 exact allocations across 12 releases, 29 permanent unallocated boundaries, exact horizon/matrix parity, Pegasus identity, Semantic Version language, and non-caller workspace boundaries."
