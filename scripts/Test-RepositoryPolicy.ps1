[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()
$allowedLanguageMatches = [System.Collections.Generic.List[string]]::new()

function Add-PolicyError([string]$Message) {
    $errors.Add($Message)
}

$capabilityPath = Join-Path $root 'docs/product/capabilities.md'
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
    'docs/product/v1-gap.md',
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
    'design', 'docs', 'infra', 'scripts', 'src', 'tests',
    'workspaces/README.md', 'workspaces/AGENTS.md'
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

$historicalOrTechnicalIdentity = '(?i)(predecessor|legacy|historical|former|old application|rg-collisionspike-dev|collisionengineers/collisionspike_v2|CollisionSpikeCurrenttree|collisionspike-corpus-evaluation|ASP-rgcollisionspikedev|cespk-pg-dev|databases:.*collisionspike)'
$technicalVersionContext = '(?i)(schema|engine|API|token|taxonomy|storage|MSAL|package|version|allocation evidence|provider-domains-v1|cedocumentmapper_v2|baseline-v2|v2\.0|engine-v2|v[12] webhooks|webhooks v2|names such as)'
$corruptedTechnicalHorizon = '(?i)(?:MSAL Browser|Rules Engine|taxonomy|access tokens?|general-purpose|engine-ready|QRD)\s+`(?:Next|Later)`/`unallocated`|`(?:Next|Later)`/`unallocated`\s+(?:access tokens?|schema|engine|storage)'
$obsoleteAllocationLanguage = '(?:^\s*#{1,6}\s+(?:Never|Conditional\s*/\s*Unclear)\s*$)|(?:\|\s*(?:Never|Conditional\s*/\s*Unclear)\s*\|)|(?:\|\s*Never:)|(?:\bEvery Never feature\b)|(?:\*\*(?:Never|Unclear):\*\*)'

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
        if ($relative -notlike 'docs/reference/imp-docs/*' -and
            $line -cmatch $obsoleteAllocationLanguage) {
            Add-PolicyError "Obsolete allocation label at ${relative}:$lineNumber"
        }
        if ($line -match 'CollisionSpike') {
            $isAllowedRetainedIdentity =
                ($relative -eq 'docs/agent-notes/current-implementation-handoff.md' -and
                    $line -cmatch 'CollisionSpike\.Infrastructure\.Persistence\.ReferenceData\.provider-domains\.v1\.json') -or
                ($relative -eq 'docs/architecture/decisions/ADR-0007-repository-local-codex-planning-plugin-boundaries.md' -and
                    $line -cmatch 'Owners: Alex and the CollisionSpike v2 development team') -or
                ($relative -eq 'docs/azure/current-inventory.md' -and
                    $line -cmatch 'no CollisionSpike ownership established')
            if ($relative -like 'docs/reference/imp-docs/*' -or
                $relative -like 'docs/history/*' -or $isAllowedRetainedIdentity -or
                $line -match $historicalOrTechnicalIdentity) {
                $allowedLanguageMatches.Add("Allowed historical/technical identity at ${relative}:$lineNumber")
            } else {
                Add-PolicyError "Obsolete active product identity at ${relative}:$lineNumber"
            }
        }
        if ($file.Extension -in @('.md', '.txt', '.yml', '.yaml') -and
            $line -cmatch '\b(?:V0|V1(?:\.x)?|V2|V3\+?|first-MVP)\b') {
            if ($relative -like 'docs/reference/imp-docs/*' -or
                $relative -like 'docs/history/*' -or $line -match $technicalVersionContext) {
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
    Get-ChildItem -LiteralPath $root -Recurse -File -Include '*.props', '*.targets' |
        Where-Object {
            $_.FullName -notmatch '[\\/](?:\.git|bin|obj|workspaces|docs[\\/]reference|docs[\\/]history)[\\/]'
        }
)
foreach ($buildFile in $applicationBuildFiles | Sort-Object FullName -Unique) {
    if ((Get-Content -LiteralPath $buildFile.FullName -Raw) -match 'workspaces[/\\]') {
        $relativeBuildFile = [System.IO.Path]::GetRelativePath($root, $buildFile.FullName).Replace('\', '/')
        Add-PolicyError "Application MSBuild configuration references a source workspace: $relativeBuildFile"
    }
}

$trackedIndexLines = @(git -C $root ls-files --stage)
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
    } finally {
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

Write-Host "Repository policy passed: 229 unique capabilities (Now 128, Next 32, Later 40, Not planned 29), Pegasus identity, Semantic Version language, and non-caller workspace boundaries."
