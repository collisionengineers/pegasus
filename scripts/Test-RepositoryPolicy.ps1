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
$rows = foreach ($line in [System.IO.File]::ReadLines($capabilityPath)) {
    if ($line -match '^\| (?<id>[A-Z]+-\d+) \|.*?\| (?<horizon>Now|Next|Later|Not planned) \|') {
        [pscustomobject]@{ Id = $Matches.id; Horizon = $Matches.horizon }
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
    'README.md', 'AGENTS.md', 'azure.yaml', '.github', 'design', 'docs',
    'infra', 'scripts', 'src', 'tests', 'workspaces/README.md', 'workspaces/AGENTS.md'
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

$historicalOrTechnicalIdentity = '(?i)(predecessor|legacy|historical|former|old application|rg-collisionspike-dev|collisionengineers/collisionspike_v2|CollisionSpikeCurrenttree|collisionspike-corpus-evaluation|ASP-rgcollisionspikedev|cespk-pg-dev|databases:.*collisionspike|CollisionSpike\.(?:Core|Infrastructure|Web|Worker|User|Superuser|Admin|Engineer))'
$technicalVersionContext = '(?i)(schema|engine|API|token|taxonomy|storage|MSAL|package|version|provider-domains-v1|cedocumentmapper_v2|baseline-v2|v2\.0|engine-v2|webhooks v2|names such as)'
$corruptedTechnicalHorizon = '(?i)(?:MSAL Browser|Rules Engine|taxonomy|access tokens?|general-purpose|engine-ready|QRD)\s+`(?:Next|Later)`/`unallocated`|`(?:Next|Later)`/`unallocated`\s+(?:access tokens?|schema|engine|storage)'
$obsoleteAllocationLanguage = '(?:^\s*#{1,6}\s+(?:Never|Conditional\s*/\s*Unclear)\s*$)|(?:\|\s*(?:Never|Conditional\s*/\s*Unclear)\s*\|)|(?:\*\*(?:Never|Unclear):\*\*)'

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
            if ($relative -like 'docs/reference/imp-docs/*' -or
                $relative -like 'docs/history/*' -or $line -match $historicalOrTechnicalIdentity) {
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
foreach ($project in $applicationProjects) {
    if ((Get-Content -LiteralPath $project.FullName -Raw) -match 'workspaces[/\\]') {
        Add-PolicyError "Application project references a source workspace: $($project.FullName)"
    }
}

$forbiddenWorkspaceNames = @('.git', 'node_modules', '.venv', '.pytest_cache', '__pycache__', '_dev', 'dist', 'coverage', 'artifacts', 'outputs', 'runs', 'checkpoints', 'sample-doc-files')
$forbiddenWorkspaceExtensions = @('.zip', '.skill', '.nupkg', '.snupkg', '.pt', '.pth', '.onnx', '.safetensors', '.ckpt')
Get-ChildItem -LiteralPath (Join-Path $root 'workspaces') -Recurse -Force | ForEach-Object {
    if ($forbiddenWorkspaceNames -contains $_.Name -or
        (-not $_.PSIsContainer -and $forbiddenWorkspaceExtensions -contains $_.Extension)) {
        Add-PolicyError "Forbidden generated/private workspace material: $($_.FullName)"
    }
    if (-not $_.PSIsContainer -and $_.Name -in @('AGENTS.md', 'CLAUDE.md') -and
        $_.FullName -ne (Join-Path $root 'workspaces/AGENTS.md')) {
        Add-PolicyError "Upstream agent instruction remains in workspace: $($_.FullName)"
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

Write-Host "Repository policy passed: 229 unique capabilities (Now 128, Next 32, Later 40, Not planned 29), Pegasus identity, Semantic Version language, and non-caller workspace boundaries."
