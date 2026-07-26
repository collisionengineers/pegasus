[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,

    [ValidateSet(
        'None',
        'BrokenLink',
        'DuplicateId',
        'MissingId',
        'QualifierDrift',
        'InvalidActiveRoute',
        'PatchMarkerHeading',
        'MissingSkillRoute'
    )]
    [string]$NegativeFixture = 'None'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Assertions = 0

function Assert-Condition {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message
    )

    $script:Assertions++
    if (-not $Condition) {
        throw $Message
    }
}

function ConvertTo-RepositoryRelativePath {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [string]$Path
    )

    return [IO.Path]::GetRelativePath($Root, $Path).Replace('\', '/')
}

function Test-PathWithinRoot {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [string]$Path
    )

    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $candidatePath = [IO.Path]::GetFullPath($Path)
    if ($candidatePath.Equals($rootPath, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    $rootPrefix = $rootPath + [IO.Path]::DirectorySeparatorChar
    return $candidatePath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)
}

function Get-FirstPartyMarkdownFiles {
    param(
        [Parameter(Mandatory)]
        [string]$Root
    )

    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

    Get-ChildItem -LiteralPath $Root -File -Filter '*.md' | ForEach-Object {
        [void]$paths.Add($_.FullName)
    }

    foreach ($relativeRoot in @('.azure', 'docs', 'retrospectives', 'src', 'repoplugin')) {
        $absoluteRoot = Join-Path $Root $relativeRoot
        if (-not (Test-Path -LiteralPath $absoluteRoot -PathType Container)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File -Filter '*.md' |
            Where-Object { $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]' } |
            ForEach-Object { [void]$paths.Add($_.FullName) }
    }

    $pluginsRoot = Join-Path $Root 'plugins'
    if (Test-Path -LiteralPath $pluginsRoot -PathType Container) {
        Get-ChildItem -LiteralPath $pluginsRoot -Directory -Filter 'repoplugin-*' |
            ForEach-Object {
                Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Filter '*.md' |
                    ForEach-Object { [void]$paths.Add($_.FullName) }
            }
    }

    return @($paths | Sort-Object)
}

function Get-MarkdownLinesWithoutCode {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $insideFence = $false
    $fenceCharacter = $null
    $result = [Collections.Generic.List[string]]::new()

    foreach ($line in Get-Content -LiteralPath $Path) {
        $fenceMatch = [regex]::Match($line, '^\s*(?<fence>`{3,}|~{3,})')
        if ($fenceMatch.Success) {
            $character = $fenceMatch.Groups['fence'].Value.Substring(0, 1)
            if (-not $insideFence) {
                $insideFence = $true
                $fenceCharacter = $character
            }
            elseif ($character -eq $fenceCharacter) {
                $insideFence = $false
                $fenceCharacter = $null
            }

            $result.Add('')
            continue
        }

        if ($insideFence) {
            $result.Add('')
            continue
        }

        $withoutInlineCode = [regex]::Replace($line, '(?<!`)`+[^`]*`+', '')
        $result.Add($withoutInlineCode)
    }

    return $result.ToArray()
}

function Test-MarkdownPatchArtifacts {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [string[]]$MarkdownFiles
    )

    foreach ($path in $MarkdownFiles) {
        $lines = @(Get-MarkdownLinesWithoutCode -Path $path)
        for ($index = 0; $index -lt $lines.Count; $index++) {
            if ($lines[$index] -match '^\+#{1,6}\s') {
                $relativePath = ConvertTo-RepositoryRelativePath -Root $Root -Path $path
                throw "[DOC-PATCH-MARKER] ${relativePath}:$($index + 1) begins a Markdown heading with a stray patch marker."
            }
        }
    }
}

function Get-MarkdownLinkRecords {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $records = [Collections.Generic.List[object]]::new()
    $lines = @(Get-MarkdownLinesWithoutCode -Path $Path)
    $inlinePattern = '!?' + '\[[^\]\r\n]*\]' + '\(\s*(?<target><[^>\r\n]+>|[^\s\)]+)(?:\s+["''][^\)\r\n]*["''])?\s*\)'
    $definitionPattern = '^\s*\[[^\]]+\]:\s*(?<target><[^>]+>|\S+)'

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        foreach ($match in [regex]::Matches($line, $inlinePattern)) {
            $records.Add([pscustomobject]@{
                    Source      = $Path
                    Line        = $index + 1
                    Destination = $match.Groups['target'].Value
                })
        }

        $definition = [regex]::Match($line, $definitionPattern)
        if ($definition.Success) {
            $records.Add([pscustomobject]@{
                    Source      = $Path
                    Line        = $index + 1
                    Destination = $definition.Groups['target'].Value
                })
        }
    }

    return $records.ToArray()
}

function ConvertTo-GitHubHeadingAnchor {
    param(
        [Parameter(Mandatory)]
        [string]$Heading
    )

    $text = [regex]::Replace($Heading, '!?' + '\[(?<label>[^\]]+)\]\([^\)]+\)', '${label}')
    $text = [regex]::Replace($text, '<[^>]+>', '')
    $text = $text.Replace('`', '').Replace('*', '').Replace('_', '').Replace('~', '')
    $text = [Net.WebUtility]::HtmlDecode($text).ToLowerInvariant()
    $text = [regex]::Replace($text, '[^\p{L}\p{Nd}\-_\s]', '')
    $text = [regex]::Replace($text, '\s', '-')
    return $text
}

function Get-MarkdownAnchors {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $anchors = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $slugCounts = [Collections.Generic.Dictionary[string, int]]::new([StringComparer]::Ordinal)
    $lines = @(Get-MarkdownLinesWithoutCode -Path $Path)

    foreach ($line in $lines) {
        $heading = [regex]::Match($line, '^\s*#{1,6}\s+(?<heading>.+?)\s*#*\s*$')
        if ($heading.Success) {
            $baseSlug = ConvertTo-GitHubHeadingAnchor -Heading $heading.Groups['heading'].Value
            if (-not [string]::IsNullOrEmpty($baseSlug)) {
                $slug = $baseSlug
                if ($slugCounts.ContainsKey($baseSlug)) {
                    $slugCounts[$baseSlug]++
                    $slug = '{0}-{1}' -f $baseSlug, $slugCounts[$baseSlug]
                }
                else {
                    $slugCounts.Add($baseSlug, 0)
                }

                [void]$anchors.Add($slug)
            }
        }

        foreach ($explicitAnchor in [regex]::Matches(
                $line,
                '<(?:a|span|h[1-6])\b[^>]*\b(?:id|name)\s*=\s*["''](?<id>[^"'']+)["'']',
                [Text.RegularExpressions.RegexOptions]::IgnoreCase
            )) {
            [void]$anchors.Add($explicitAnchor.Groups['id'].Value)
        }
    }

    return $anchors
}

function Resolve-LocalMarkdownDestination {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [object]$Record
    )

    $destination = $Record.Destination.Trim()
    $wasAngleWrapped = $destination.StartsWith('<', [StringComparison]::Ordinal) -and
        $destination.EndsWith('>', [StringComparison]::Ordinal)
    if ($wasAngleWrapped) {
        $destination = $destination.Substring(1, $destination.Length - 2)
    }

    if ($Record.Source.EndsWith('.template.md', [StringComparison]::OrdinalIgnoreCase) -and
        $wasAngleWrapped -and $destination -match '^[a-z][a-z0-9-]*$') {
        return $null
    }

    if ($destination -match '^[A-Za-z]:[\\/]' -or $destination -match '^\\\\') {
        $sourceRelative = ConvertTo-RepositoryRelativePath -Root $Root -Path $Record.Source
        throw "[DOC-LINK-ABSOLUTE] ${sourceRelative}:$($Record.Line) uses a nonportable absolute path: $destination"
    }

    if ($destination -match '^[A-Za-z][A-Za-z0-9+.-]*:') {
        return $null
    }

    $fragment = $null
    $fragmentIndex = $destination.IndexOf('#')
    if ($fragmentIndex -ge 0) {
        $fragment = [Uri]::UnescapeDataString($destination.Substring($fragmentIndex + 1))
        $destination = $destination.Substring(0, $fragmentIndex)
    }

    $queryIndex = $destination.IndexOf('?')
    if ($queryIndex -ge 0) {
        $destination = $destination.Substring(0, $queryIndex)
    }

    $destination = [Uri]::UnescapeDataString($destination).Replace('/', [IO.Path]::DirectorySeparatorChar)
    if ([string]::IsNullOrWhiteSpace($destination)) {
        $targetPath = $Record.Source
    }
    elseif ($destination.StartsWith([IO.Path]::DirectorySeparatorChar)) {
        $targetPath = Join-Path $Root $destination.TrimStart('\', '/')
    }
    else {
        $targetPath = Join-Path (Split-Path -Parent $Record.Source) $destination
    }

    $targetPath = [IO.Path]::GetFullPath($targetPath)
    if (-not (Test-PathWithinRoot -Root $Root -Path $targetPath)) {
        $sourceRelative = ConvertTo-RepositoryRelativePath -Root $Root -Path $Record.Source
        throw "[DOC-LINK-ESCAPE] ${sourceRelative}:$($Record.Line) resolves outside the repository: $($Record.Destination)"
    }

    return [pscustomobject]@{
        Path     = $targetPath
        Fragment = $fragment
    }
}

function Assert-ExactPathExists {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [string]$TargetPath,

        [Parameter(Mandatory)]
        [object]$Record
    )

    $relativeTarget = ConvertTo-RepositoryRelativePath -Root $Root -Path $TargetPath
    $current = [IO.Path]::GetFullPath($Root)
    foreach ($segment in $relativeTarget.Split('/', [StringSplitOptions]::RemoveEmptyEntries)) {
        if ($segment -eq '.') {
            continue
        }

        $entries = @(Get-ChildItem -LiteralPath $current -Force -ErrorAction SilentlyContinue)
        $exact = @($entries | Where-Object { $_.Name -ceq $segment })
        if ($exact.Count -eq 0) {
            $caseInsensitive = @($entries | Where-Object { $_.Name -ieq $segment })
            $sourceRelative = ConvertTo-RepositoryRelativePath -Root $Root -Path $Record.Source
            if ($caseInsensitive.Count -gt 0) {
                throw "[DOC-LINK-CASE] ${sourceRelative}:$($Record.Line) uses incorrect path casing for $relativeTarget."
            }

            throw "[DOC-LINK-MISSING] ${sourceRelative}:$($Record.Line) targets missing path $relativeTarget."
        }

        $current = $exact[0].FullName
    }
}

function Test-MarkdownLinks {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [string[]]$MarkdownFiles,

        [switch]$StopOnFirstError
    )

    $links = [Collections.Generic.List[object]]::new()
    $errors = [Collections.Generic.List[string]]::new()
    $anchorCache = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)

    foreach ($file in $MarkdownFiles) {
        foreach ($record in Get-MarkdownLinkRecords -Path $file) {
            try {
                $resolved = Resolve-LocalMarkdownDestination -Root $Root -Record $record
                if ($null -eq $resolved) {
                    continue
                }

                Assert-ExactPathExists -Root $Root -TargetPath $resolved.Path -Record $record
                $targetRelative = ConvertTo-RepositoryRelativePath -Root $Root -Path $resolved.Path
                $sourceRelative = ConvertTo-RepositoryRelativePath -Root $Root -Path $record.Source
                $links.Add([pscustomobject]@{
                        Source      = $sourceRelative
                        Target      = $targetRelative
                        Fragment    = $resolved.Fragment
                        Line        = $record.Line
                        Destination = $record.Destination
                    })

                if (-not [string]::IsNullOrWhiteSpace($resolved.Fragment) -and
                    [IO.Path]::GetExtension($resolved.Path) -ieq '.md') {
                    if (-not $anchorCache.ContainsKey($resolved.Path)) {
                        $anchorCache.Add($resolved.Path, (Get-MarkdownAnchors -Path $resolved.Path))
                    }

                    if (-not $anchorCache[$resolved.Path].Contains($resolved.Fragment)) {
                        throw "[DOC-ANCHOR-MISSING] ${sourceRelative}:$($record.Line) targets missing anchor #$($resolved.Fragment) in $targetRelative."
                    }
                }
            }
            catch {
                if ($StopOnFirstError) {
                    throw
                }

                $errors.Add($_.Exception.Message)
            }
        }
    }

    if ($errors.Count -gt 0) {
        throw "[DOC-LINK-FAILURES] $($errors.Count) local link or anchor failure(s):`n$($errors -join "`n")"
    }

    return $links.ToArray()
}

function Split-MarkdownTableRow {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Line
    )

    $text = $Line.Trim()
    if (-not $text.StartsWith('|', [StringComparison]::Ordinal)) {
        return @()
    }

    if ($text.EndsWith('|', [StringComparison]::Ordinal)) {
        $text = $text.Substring(1, $text.Length - 2)
    }
    else {
        $text = $text.Substring(1)
    }

    $cells = [Collections.Generic.List[string]]::new()
    $builder = [Text.StringBuilder]::new()
    $insideCode = $false

    for ($index = 0; $index -lt $text.Length; $index++) {
        $character = $text[$index]
        if ($character -eq '\' -and $index + 1 -lt $text.Length -and $text[$index + 1] -eq '|') {
            [void]$builder.Append($character)
            [void]$builder.Append('|')
            $index++
            continue
        }

        if ($character -eq '`') {
            $insideCode = -not $insideCode
            [void]$builder.Append($character)
            continue
        }

        if ($character -eq '|' -and -not $insideCode) {
            $cells.Add($builder.ToString().Trim())
            [void]$builder.Clear()
            continue
        }

        [void]$builder.Append($character)
    }

    $cells.Add($builder.ToString().Trim())
    return $cells.ToArray()
}

function Read-FeatureRows {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [ValidateSet('Worksheet', 'CanonicalMap')]
        [string]$Kind
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "[DOC-FEATURE-MISSING] Required $Kind file is missing: $Path"
    }

    $lines = Get-Content -LiteralPath $Path
    $headerFound = $false
    $rows = [Collections.Generic.List[object]]::new()
    $byId = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $cells = @(Split-MarkdownTableRow -Line $lines[$index])
        if ($cells.Count -eq 0) {
            continue
        }

        if ($Kind -eq 'Worksheet' -and $cells.Count -ge 3 -and
            $cells[0] -ceq 'ID' -and $cells[1] -ceq 'Feature' -and $cells[2] -ceq 'Your version') {
            $headerFound = $true
            continue
        }

        if ($Kind -eq 'CanonicalMap' -and $cells.Count -ge 7 -and
            $cells[0] -ceq 'ID' -and $cells[1] -ceq 'Feature' -and $cells[2] -ceq 'Raw answer' -and
            $cells[3] -ceq 'Allocation' -and $cells[4] -ceq 'Authority/source' -and
            $cells[5] -ceq 'Owning requirement/plan' -and $cells[6] -ceq 'Activation note') {
            $headerFound = $true
            continue
        }

        if ($cells[0] -notmatch '^[A-Z]+-[0-9]{2}$') {
            continue
        }

        $minimumCells = if ($Kind -eq 'Worksheet') { 3 } else { 7 }
        if ($cells.Count -lt $minimumCells) {
            throw "[DOC-FEATURE-SHAPE] ${Path}:$($index + 1) has $($cells.Count) cells; expected at least $minimumCells."
        }

        $row = [pscustomobject]@{
            Id         = $cells[0].Trim()
            Feature    = $cells[1].Trim()
            RawAnswer  = $cells[2].Trim()
            Cells      = $cells
            SourceLine = $index + 1
        }

        if ($byId.ContainsKey($row.Id)) {
            throw "[DOC-FEATURE-DUPLICATE] $Path contains duplicate ID $($row.Id) at lines $($byId[$row.Id].SourceLine) and $($row.SourceLine)."
        }

        if ($Kind -eq 'CanonicalMap') {
            foreach ($columnIndex in 3..6) {
                if ([string]::IsNullOrWhiteSpace($cells[$columnIndex])) {
                    throw "[DOC-FEATURE-SHAPE] ${Path}:$($index + 1) has an empty required canonical-map cell."
                }
            }

            if ($cells[5] -notmatch '\[[^\]]+\](?:\([^\)]+\))?') {
                throw "[DOC-FEATURE-OWNER] ${Path}:$($index + 1) must link to an owning requirement or plan."
            }
        }

        $byId.Add($row.Id, $row)
        $rows.Add($row)
    }

    if (-not $headerFound) {
        throw "[DOC-FEATURE-SHAPE] $Path does not contain the required $Kind table header."
    }

    return [pscustomobject]@{
        Rows = $rows.ToArray()
        ById = $byId
    }
}

function Test-FeatureParity {
    param(
        [Parameter(Mandatory)]
        [string]$WorksheetPath,

        [Parameter(Mandatory)]
        [string]$CanonicalMapPath,

        [Parameter(Mandatory)]
        [int]$ExpectedCount
    )

    $worksheet = Read-FeatureRows -Path $WorksheetPath -Kind Worksheet
    $canonical = Read-FeatureRows -Path $CanonicalMapPath -Kind CanonicalMap

    if ($worksheet.Rows.Count -ne $ExpectedCount) {
        throw "[DOC-FEATURE-COUNT] Worksheet has $($worksheet.Rows.Count) rows; expected $ExpectedCount."
    }

    if ($canonical.Rows.Count -ne $ExpectedCount) {
        throw "[DOC-FEATURE-COUNT] Canonical map has $($canonical.Rows.Count) rows; expected $ExpectedCount."
    }

    $missing = @($worksheet.ById.Keys | Where-Object { -not $canonical.ById.ContainsKey($_) } | Sort-Object)
    $extra = @($canonical.ById.Keys | Where-Object { -not $worksheet.ById.ContainsKey($_) } | Sort-Object)
    if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
        throw "[DOC-FEATURE-MISSING] Feature ID sets differ. Missing: $($missing -join ', '); extra: $($extra -join ', ')."
    }

    foreach ($id in $worksheet.ById.Keys) {
        $expected = $worksheet.ById[$id]
        $actual = $canonical.ById[$id]
        if (-not [string]::Equals($expected.Feature, $actual.Feature, [StringComparison]::Ordinal) -or
            -not [string]::Equals($expected.RawAnswer, $actual.RawAnswer, [StringComparison]::Ordinal)) {
            throw "[DOC-FEATURE-DRIFT] $id differs. Worksheet=('$($expected.Feature)', '$($expected.RawAnswer)'); canonical=('$($actual.Feature)', '$($actual.RawAnswer)')."
        }
    }

    return $worksheet.Rows.Count
}

function Test-RequiredRoutes {
    param(
        [Parameter(Mandatory)]
        [object[]]$Links,

        [Parameter(Mandatory)]
        [object[]]$RequiredRoutes
    )

    foreach ($route in $RequiredRoutes) {
        $found = @($Links | Where-Object {
                $_.Source -ceq $route.Source -and $_.Target -ceq $route.Target
            }).Count -gt 0
        if (-not $found) {
            throw "[DOC-ROUTE-INVALID] $($route.Source) must link to active owner $($route.Target)."
        }
    }
}

function Test-RequiredSkillRoutes {
    param(
        [Parameter(Mandatory)]
        [string]$Root
    )

    $requirements = [ordered]@{
        'AGENTS.md' = @(
            '$repoplugin-planning:apply-collisionspike-domain',
            '$repoplugin-planning:route-collisionspike-azure',
            '$repoplugin-documentation:bootstrap-repository-documentation',
            '$repoplugin-documentation:maintain-repository-documentation',
            '$repoplugin-documentation:audit-repository-documentation',
            '$repoplugin-ui-ux:plan-ui-ux-change',
            '$repoplugin-ui-ux:apply-collision-engineers-ui-style'
        )
        'docs/agent-guidance/agent-routing.md' = @(
            '$repoplugin-planning:plan-repository-change',
            '$repoplugin-planning:apply-collisionspike-domain',
            '$repoplugin-planning:route-collisionspike-azure',
            '$repoplugin-implementation:implement-plan-pack',
            '$repoplugin-review:review-implementation',
            '$repoplugin-review:triage-pr-feedback',
            '$repoplugin-validation:test-and-validate-repository-change',
            '$repoplugin-debugging:debug-repository-failure',
            '$repoplugin-documentation:bootstrap-repository-documentation',
            '$repoplugin-documentation:maintain-repository-documentation',
            '$repoplugin-documentation:audit-repository-documentation',
            '$repoplugin-ui-ux:plan-ui-ux-change',
            '$repoplugin-ui-ux:apply-collision-engineers-ui-style'
        )
    }

    foreach ($relativePath in $requirements.Keys) {
        $path = Join-Path $Root $relativePath
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "[DOC-INSTRUCTION-ROUTE] Required skill-route owner is missing: $relativePath"
        }

        $content = Get-Content -LiteralPath $path -Raw
        foreach ($skill in $requirements[$relativePath]) {
            if ($content.IndexOf($skill, [StringComparison]::Ordinal) -lt 0) {
                throw "[DOC-INSTRUCTION-ROUTE] $relativePath must retain the public entry route $skill."
            }
        }
    }
}

function Get-LinkedPathFromCell {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [string]$SourcePath,

        [Parameter(Mandatory)]
        [string]$Cell
    )

    $match = [regex]::Match($Cell, '\[[^\]]+\]\((?<target>[^\)]+)\)')
    if (-not $match.Success) {
        return $null
    }

    $record = [pscustomobject]@{
        Source      = $SourcePath
        Line        = 0
        Destination = $match.Groups['target'].Value
    }
    $resolved = Resolve-LocalMarkdownDestination -Root $Root -Record $record
    if ($null -eq $resolved) {
        return $null
    }

    return ConvertTo-RepositoryRelativePath -Root $Root -Path $resolved.Path
}

function Test-HistoricalPlanMetadata {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [string]$RelativePath
    )

    $path = Join-Path $Root $RelativePath
    $content = Get-Content -LiteralPath $path -Raw
    if ($content -notmatch '(?im)^\s*(?:>\s*)?(?:\*\*)?Status(?:\*\*)?:\s*(?:\*\*)?(?:Historical|Superseded)\b') {
        throw "[DOC-PLAN-STATUS] $RelativePath must declare Historical or Superseded status."
    }

    if ($content -notmatch '(?im)^\s*(?:>\s*)?(?:\*\*)?(?:Supersession(?:/current owners?)?|Superseded by|Current owners?)(?:\*\*)?:\s*.*\[[^\]]+\]\([^\)]+\)') {
        throw "[DOC-PLAN-STATUS] $RelativePath must contain a linked supersession/current-owner route."
    }
}

function Assert-KnownPlanStatus {
    param(
        [Parameter(Mandatory)]
        [string]$Status,

        [Parameter(Mandatory)]
        [string]$Location
    )

    $normalizedStatus = $Status.Trim(' ', '`', '*').ToLowerInvariant()
    $allowedStatuses = @('ready', 'research', 'draft/unapproved', 'historical', 'superseded')
    if ($normalizedStatus -notin $allowedStatuses) {
        throw "[DOC-PLAN-STATUS] $Location has unknown status '$Status'."
    }

    return $normalizedStatus
}

function Test-PlanIndex {
    param(
        [Parameter(Mandatory)]
        [string]$Root
    )

    $indexPath = Join-Path $Root 'docs/plans/README.md'
    if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
        throw '[DOC-PLAN-STATUS] docs/plans/README.md is missing.'
    }

    $requiredHeaders = @(
        'Plan',
        'Purpose',
        'Authority',
        'Status',
        'Horizon',
        'Real/intended caller',
        'Blocker/activation gate',
        'Supersession'
    )
    $headerFound = $false
    $rows = [Collections.Generic.List[object]]::new()
    $lines = Get-Content -LiteralPath $indexPath
    for ($index = 0; $index -lt $lines.Count; $index++) {
        $cells = @(Split-MarkdownTableRow -Line $lines[$index])
        if ($cells.Count -lt 8) {
            continue
        }

        $isHeader = $true
        for ($column = 0; $column -lt $requiredHeaders.Count; $column++) {
            if ($cells[$column] -cne $requiredHeaders[$column]) {
                $isHeader = $false
                break
            }
        }

        if ($isHeader) {
            $headerFound = $true
            continue
        }

        if (-not $headerFound -or $cells[0] -match '^:?-+') {
            continue
        }

        if ($cells[0] -notmatch '\[[^\]]+\]\([^\)]+\)') {
            continue
        }

        foreach ($column in 0..7) {
            if ([string]::IsNullOrWhiteSpace($cells[$column])) {
                throw "[DOC-PLAN-STATUS] docs/plans/README.md:$($index + 1) has an empty required field."
            }
        }

        $target = Get-LinkedPathFromCell -Root $Root -SourcePath $indexPath -Cell $cells[0]
        $status = Assert-KnownPlanStatus `
            -Status $cells[3] `
            -Location "docs/plans/README.md:$($index + 1)"

        if ($status -in @('historical', 'superseded') -and
            $cells[7] -notmatch '\[[^\]]+\]\([^\)]+\)') {
            throw "[DOC-PLAN-STATUS] docs/plans/README.md:$($index + 1) requires a linked supersession/current-owner route."
        }

        $rows.Add([pscustomobject]@{
                Target = $target
                Status = $status
                Line   = $index + 1
            })
    }

    if (-not $headerFound) {
        throw '[DOC-PLAN-STATUS] docs/plans/README.md lacks the required plan-index table header.'
    }

    $expectedTargets = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    Get-ChildItem -LiteralPath (Join-Path $Root 'docs/plans') -File -Filter '*.md' |
        Where-Object { $_.Name -cne 'README.md' } |
        ForEach-Object { [void]$expectedTargets.Add((ConvertTo-RepositoryRelativePath -Root $Root -Path $_.FullName)) }
    Get-ChildItem -LiteralPath (Join-Path $Root 'docs/plans') -Directory | ForEach-Object {
        $readme = Join-Path $_.FullName 'README.md'
        if (Test-Path -LiteralPath $readme -PathType Leaf) {
            [void]$expectedTargets.Add((ConvertTo-RepositoryRelativePath -Root $Root -Path $readme))
        }
    }
    foreach ($relativePath in @(
            'INITIAL_SCAFFOLD_PLAN.md',
            'REPOSITORY_STRUCTURE_PLAN.md',
            'docs/plans/remainder-delivery/review/pr-1-unaddressed-issues.md'
        )) {
        [void]$expectedTargets.Add($relativePath)
    }

    foreach ($target in $expectedTargets) {
        $matches = @($rows | Where-Object { $_.Target -ceq $target })
        if ($matches.Count -ne 1) {
            throw "[DOC-PLAN-STATUS] Plan index must link $target exactly once; found $($matches.Count)."
        }
    }

    foreach ($historicalPath in @(
            'INITIAL_SCAFFOLD_PLAN.md',
            'REPOSITORY_STRUCTURE_PLAN.md',
            'docs/plans/remainder-delivery/review/pr-1-unaddressed-issues.md'
        )) {
        Test-HistoricalPlanMetadata -Root $Root -RelativePath $historicalPath
    }

    return $rows.Count
}

function Test-InstructionDiscovery {
    param(
        [Parameter(Mandatory)]
        [string]$Root
    )

    $rootAgents = Join-Path $Root 'AGENTS.md'
    $docsAgents = Join-Path $Root 'docs/AGENTS.md'
    foreach ($path in @($rootAgents, $docsAgents)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "[DOC-INSTRUCTION-MISSING] Required instruction source is missing: $(ConvertTo-RepositoryRelativePath -Root $Root -Path $path)"
        }
    }

    foreach ($override in @('AGENTS.override.md', 'docs/AGENTS.override.md')) {
        if (Test-Path -LiteralPath (Join-Path $Root $override) -PathType Leaf) {
            throw "[DOC-INSTRUCTION-OVERRIDE] Unexpected instruction override changes the required root/docs chain: $override"
        }
    }

    $nestedDocsAgents = @(Get-ChildItem -LiteralPath (Join-Path $Root 'docs') -Recurse -File -Filter 'AGENTS.md')
    if ($nestedDocsAgents.Count -ne 1 -or $nestedDocsAgents[0].FullName -cne $docsAgents) {
        $found = @($nestedDocsAgents | ForEach-Object { ConvertTo-RepositoryRelativePath -Root $Root -Path $_.FullName })
        throw "[DOC-INSTRUCTION-CHAIN] Expected only docs/AGENTS.md below docs; found: $($found -join ', ')."
    }

    $rootBytes = (Get-Item -LiteralPath $rootAgents).Length
    $docsBytes = (Get-Item -LiteralPath $docsAgents).Length
    $combinedBytes = $rootBytes + $docsBytes
    if ($combinedBytes -gt 32768) {
        throw "[DOC-INSTRUCTION-BUDGET] Root/docs project instructions total $combinedBytes bytes; the limit is 32768."
    }

    Write-Host "Instruction chain at root: AGENTS.md ($rootBytes bytes)."
    Write-Host "Instruction chain under docs: AGENTS.md -> docs/AGENTS.md ($combinedBytes combined bytes)."
    return $combinedBytes
}

function Write-FixtureFile {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [string]$RelativePath,

        [Parameter(Mandatory)]
        [string]$Content
    )

    $path = Join-Path $Root $RelativePath
    [IO.Directory]::CreateDirectory((Split-Path -Parent $path)) | Out-Null
    [IO.File]::WriteAllText($path, ($Content.TrimStart() + "`n"), [Text.UTF8Encoding]::new($false))
    return $path
}

function Remove-DocumentationFixtureDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    $temporaryRoot = (Resolve-Path -LiteralPath ([IO.Path]::GetTempPath())).Path.TrimEnd('\', '/')
    $parentPath = [IO.Path]::GetDirectoryName($resolvedPath).TrimEnd('\', '/')
    $leafName = [IO.Path]::GetFileName($resolvedPath)
    $hasExpectedPrefix = $leafName.StartsWith('collisionspike-documentation-', [StringComparison]::Ordinal) -or
        $leafName.StartsWith('collisionspike-documentation-status-', [StringComparison]::Ordinal)

    if (-not $parentPath.Equals($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not $hasExpectedPrefix) {
        throw "[DOC-FIXTURE-CLEANUP] Refusing recursive removal outside the verified fixture boundary: $resolvedPath"
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
}

function Invoke-NegativeFixture {
    param(
        [Parameter(Mandatory)]
        [ValidateSet(
            'BrokenLink',
            'DuplicateId',
            'MissingId',
            'QualifierDrift',
            'InvalidActiveRoute',
            'PatchMarkerHeading',
            'MissingSkillRoute'
        )]
        [string]$Name
    )

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('collisionspike-documentation-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
    try {
        switch ($Name) {
            'BrokenLink' {
                $document = Write-FixtureFile -Root $fixtureRoot -RelativePath 'README.md' -Content @'
# Fixture

[Missing](missing.md)
'@
                [void](Test-MarkdownLinks -Root $fixtureRoot -MarkdownFiles @($document) -StopOnFirstError)
            }
            'DuplicateId' {
                $worksheet = Write-FixtureFile -Root $fixtureRoot -RelativePath 'FEATURE_VERSIONING.md' -Content @'
| ID | Feature | Your version |
| --- | --- | --- |
| TEST-01 | One | V1 |
| TEST-01 | Two | V2 |
'@
                [void](Read-FeatureRows -Path $worksheet -Kind Worksheet)
            }
            'MissingId' {
                $worksheet = Write-FixtureFile -Root $fixtureRoot -RelativePath 'FEATURE_VERSIONING.md' -Content @'
| ID | Feature | Your version |
| --- | --- | --- |
| TEST-01 | One | V1 |
| TEST-02 | Two | Never |
'@
                $map = Write-FixtureFile -Root $fixtureRoot -RelativePath 'docs/plans/feature-maturity-map.md' -Content @'
| ID | Feature | Raw answer | Allocation | Authority/source | Owning requirement/plan | Activation note |
| --- | --- | --- | --- | --- | --- | --- |
| TEST-01 | One | V1 | V1 | Worksheet | [Owner](owner.md) | Release gate |
'@
                [void](Test-FeatureParity -WorksheetPath $worksheet -CanonicalMapPath $map -ExpectedCount 2)
            }
            'QualifierDrift' {
                $worksheet = Write-FixtureFile -Root $fixtureRoot -RelativePath 'FEATURE_VERSIONING.md' -Content @'
| ID | Feature | Your version |
| --- | --- | --- |
| TEST-01 | Conditional capability | V3 (if rule based insufficient) |
'@
                $map = Write-FixtureFile -Root $fixtureRoot -RelativePath 'docs/plans/feature-maturity-map.md' -Content @'
| ID | Feature | Raw answer | Allocation | Authority/source | Owning requirement/plan | Activation note |
| --- | --- | --- | --- | --- | --- | --- |
| TEST-01 | Conditional capability | V3 | V3 | Worksheet | [Owner](owner.md) | Conditional |
'@
                [void](Test-FeatureParity -WorksheetPath $worksheet -CanonicalMapPath $map -ExpectedCount 1)
            }
            'InvalidActiveRoute' {
                $source = Write-FixtureFile -Root $fixtureRoot -RelativePath 'docs/README.md' -Content @'
# Documentation

[Feature maturity](../FEATURE_VERSIONING.md)
'@
                [void](Write-FixtureFile -Root $fixtureRoot -RelativePath 'FEATURE_VERSIONING.md' -Content '# Historical worksheet')
                [void](Write-FixtureFile -Root $fixtureRoot -RelativePath 'docs/plans/feature-maturity-map.md' -Content '# Active maturity map')
                $links = Test-MarkdownLinks -Root $fixtureRoot -MarkdownFiles @($source)
                Test-RequiredRoutes -Links $links -RequiredRoutes @(
                    [pscustomobject]@{
                        Source = 'docs/README.md'
                        Target = 'docs/plans/feature-maturity-map.md'
                    }
                )
            }
            'PatchMarkerHeading' {
                $document = Write-FixtureFile -Root $fixtureRoot -RelativePath 'README.md' -Content @'
# Fixture

+## Stray patch marker
'@
                Test-MarkdownPatchArtifacts -Root $fixtureRoot -MarkdownFiles @($document)
            }
            'MissingSkillRoute' {
                [void](Write-FixtureFile -Root $fixtureRoot -RelativePath 'AGENTS.md' -Content '# Incomplete routes')
                [void](Write-FixtureFile -Root $fixtureRoot -RelativePath 'docs/agent-guidance/agent-routing.md' -Content '# Incomplete routes')
                Test-RequiredSkillRoutes -Root $fixtureRoot
            }
        }

        throw "[DOC-FIXTURE-ACCEPTED] Negative fixture $Name was accepted."
    }
    finally {
        Remove-DocumentationFixtureDirectory -Path $fixtureRoot
    }
}

function Test-NegativeFixtureSuite {
    $expectedCodes = [ordered]@{
        BrokenLink        = '[DOC-LINK-MISSING]'
        DuplicateId       = '[DOC-FEATURE-DUPLICATE]'
        MissingId         = '[DOC-FEATURE-COUNT]'
        QualifierDrift    = '[DOC-FEATURE-DRIFT]'
        InvalidActiveRoute = '[DOC-ROUTE-INVALID]'
        PatchMarkerHeading = '[DOC-PATCH-MARKER]'
        MissingSkillRoute  = '[DOC-INSTRUCTION-ROUTE]'
    }

    foreach ($fixtureName in $expectedCodes.Keys) {
        $caught = $null
        try {
            Invoke-NegativeFixture -Name $fixtureName
        }
        catch {
            $caught = $_
        }

        Assert-Condition -Condition ($null -ne $caught) -Message "Negative fixture $fixtureName did not fail."
        Assert-Condition `
            -Condition $caught.Exception.Message.StartsWith($expectedCodes[$fixtureName], [StringComparison]::Ordinal) `
            -Message "Negative fixture $fixtureName failed for the wrong reason: $($caught.Exception.Message)"
    }

    Write-Host "Documentation negative fixtures: $($expectedCodes.Count)/$($expectedCodes.Count) rejected as expected." -ForegroundColor Green
}

function Test-StatusSemantics {
    foreach ($knownStatus in @('ready', 'research', 'draft/unapproved', 'historical', 'superseded')) {
        $actual = Assert-KnownPlanStatus -Status $knownStatus -Location 'status fixture'
        Assert-Condition -Condition ($actual -ceq $knownStatus) -Message "Known plan status $knownStatus was changed."
    }

    $unknownFailure = $null
    try {
        [void](Assert-KnownPlanStatus -Status 'unknown' -Location 'status fixture')
    }
    catch {
        $unknownFailure = $_
    }
    Assert-Condition `
        -Condition ($null -ne $unknownFailure -and $unknownFailure.Exception.Message.StartsWith('[DOC-PLAN-STATUS]', [StringComparison]::Ordinal)) `
        -Message 'Unknown plan status was not rejected for the expected reason.'

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('collisionspike-documentation-status-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
    try {
        $worksheet = Write-FixtureFile -Root $fixtureRoot -RelativePath 'FEATURE_VERSIONING.md' -Content @'
| ID | Feature | Your version |
| --- | --- | --- |
| TEST-01 | Permanent boundary | Never — not planned |
| TEST-02 | Unknown activation | Unclear |
| TEST-03 | Relative gate | pre V1 |
| TEST-04 | Conditional capability | V3 (if rule based insufficient) |
'@
        $map = Write-FixtureFile -Root $fixtureRoot -RelativePath 'docs/plans/feature-maturity-map.md' -Content @'
| ID | Feature | Raw answer | Allocation | Authority/source | Owning requirement/plan | Activation note |
| --- | --- | --- | --- | --- | --- | --- |
| TEST-01 | Permanent boundary | Never — not planned | Never | Worksheet | [Owner](owner.md) | Permanent boundary |
| TEST-02 | Unknown activation | Unclear | Conditional | Worksheet | [Owner](owner.md) | Decision required |
| TEST-03 | Relative gate | pre V1 | V1 prerequisite | Worksheet | [Owner](owner.md) | Before release |
| TEST-04 | Conditional capability | V3 (if rule based insufficient) | V3 conditional | Worksheet | [Owner](owner.md) | Evidence gate |
'@
        $count = Test-FeatureParity -WorksheetPath $worksheet -CanonicalMapPath $map -ExpectedCount 4
        Assert-Condition -Condition ($count -eq 4) -Message 'Literal terminal, unclear, relative, and conditional answers did not pass exact parity.'
    }
    finally {
        Remove-DocumentationFixtureDirectory -Path $fixtureRoot
    }

    Write-Host 'Documentation status semantics: terminal/transient literals preserved; unknown plan status rejected.' -ForegroundColor Green
}

if ($NegativeFixture -ne 'None') {
    Invoke-NegativeFixture -Name $NegativeFixture
    return
}

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
Test-NegativeFixtureSuite
Test-StatusSemantics

$featureCount = Test-FeatureParity `
    -WorksheetPath (Join-Path $resolvedRoot 'FEATURE_VERSIONING.md') `
    -CanonicalMapPath (Join-Path $resolvedRoot 'docs/plans/feature-maturity-map.md') `
    -ExpectedCount 213
Write-Host "Feature maturity parity: $featureCount exact ordinal worksheet/map triples." -ForegroundColor Green

$markdownFiles = @(Get-FirstPartyMarkdownFiles -Root $resolvedRoot)
Test-MarkdownPatchArtifacts -Root $resolvedRoot -MarkdownFiles $markdownFiles
$links = @(Test-MarkdownLinks -Root $resolvedRoot -MarkdownFiles $markdownFiles)

$requiredRoutes = @(
    [pscustomobject]@{ Source = 'README.md'; Target = 'docs/README.md' },
    [pscustomobject]@{ Source = 'README.md'; Target = 'docs/architecture/README.md' },
    [pscustomobject]@{ Source = 'README.md'; Target = 'docs/plans/feature-maturity-map.md' },
    [pscustomobject]@{ Source = 'README.md'; Target = 'docs/runbooks/developer-workstation.md' },
    [pscustomobject]@{ Source = 'README.md'; Target = 'docs/agent-guidance/validation.md' },
    [pscustomobject]@{ Source = 'AGENTS.md'; Target = 'docs/agent-guidance/source-of-truth.md' },
    [pscustomobject]@{ Source = 'AGENTS.md'; Target = 'docs/agent-guidance/agent-routing.md' },
    [pscustomobject]@{ Source = 'AGENTS.md'; Target = 'docs/agent-guidance/validation.md' },
    [pscustomobject]@{ Source = 'docs/README.md'; Target = 'docs/plans/README.md' },
    [pscustomobject]@{ Source = 'docs/README.md'; Target = 'docs/plans/feature-maturity-map.md' },
    [pscustomobject]@{ Source = 'docs/README.md'; Target = 'docs/architecture/README.md' },
    [pscustomobject]@{ Source = 'docs/README.md'; Target = 'docs/agent-notes/current-implementation-handoff.md' },
    [pscustomobject]@{ Source = 'docs/README.md'; Target = 'docs/azure/README.md' },
    [pscustomobject]@{ Source = 'docs/README.md'; Target = 'docs/reference/README.md' },
    [pscustomobject]@{ Source = 'docs/README.md'; Target = 'docs/evaluation/README.md' },
    [pscustomobject]@{ Source = 'docs/README.md'; Target = 'docs/runbooks/developer-workstation.md' }
)
Test-RequiredRoutes -Links $links -RequiredRoutes $requiredRoutes
Test-RequiredSkillRoutes -Root $resolvedRoot

$planCount = Test-PlanIndex -Root $resolvedRoot
$instructionBytes = Test-InstructionDiscovery -Root $resolvedRoot

$summary = (
    'Documentation validation passed: {0} Markdown files, {1} local links, ' +
    '{2} exact feature triples, {3} plan-index rows, {4} instruction bytes, {5} assertions.'
) -f
    $markdownFiles.Count,
    $links.Count,
    $featureCount,
    $planCount,
    $instructionBytes,
    $script:Assertions
Write-Host $summary -ForegroundColor Green
