[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path

function Require-File {
    param([Parameter(Mandatory)][string]$RelativePath)
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "[AZWF-PATH] Required file is missing: $RelativePath"
    }
    return $path
}

function Require-HeadingOnce {
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Heading,
        [Parameter(Mandatory)][string]$Owner
    )
    if ([regex]::Matches($Content, "(?m)^## $([regex]::Escape($Heading))\s*$").Count -ne 1) {
        throw "[AZWF-HEADING] $Owner must contain exactly one '## $Heading' heading."
    }
}

$required = @(
    'AGENTS.md',
    'docs/index.md',
    'docs/product/index.md',
    'docs/product/capabilities.md',
    'docs/roadmap.md',
    'docs/architecture.md',
    'docs/operations.md',
    'docs/agent-mistakes.md',
    'docs/decisions/README.md',
    'docs/changes/README.md',
    'design/README.md',
    '.github/ISSUE_TEMPLATE/feature.yml',
    '.github/ISSUE_TEMPLATE/bug.yml',
    '.github/ISSUE_TEMPLATE/task.yml',
    '.github/ISSUE_TEMPLATE/decision.yml',
    '.github/ISSUE_TEMPLATE/config.yml',
    '.github/pull_request_template.md'
)
foreach ($relativePath in $required) { [void](Require-File $relativePath) }

$agents = Get-Content -LiteralPath (Require-File 'AGENTS.md') -Raw
$product = Get-Content -LiteralPath (Require-File 'docs/product/index.md') -Raw
foreach ($pair in @(
        @{ Content = $agents; Owner = 'AGENTS.md' },
        @{ Content = $product; Owner = 'docs/product/index.md' }
    )) {
    $matches = [regex]::Matches($pair.Content, '(?m)^- Repository mode: `(development|released)`\.?\s*$')
    if ($matches.Count -ne 1) {
        throw "[AZWF-MODE] $($pair.Owner) must declare exactly one repository mode."
    }
}
$agentMode = [regex]::Match($agents, '(?m)^- Repository mode: `(?<mode>development|released)`').Groups['mode'].Value
$productMode = [regex]::Match($product, '(?m)^- Repository mode: `(?<mode>development|released)`').Groups['mode'].Value
if ($agentMode -cne $productMode) { throw '[AZWF-MODE] AGENTS.md and product mode disagree.' }

foreach ($heading in @(
        'Purpose and problem', 'Users and outcomes', 'Success measures', 'Scope',
        'Requirements and invariants', 'Quality constraints', 'Supported contracts',
        'Functional areas', 'Limitations', 'Open decisions'
    )) {
    Require-HeadingOnce -Content $product -Heading $heading -Owner 'docs/product/index.md'
}
foreach ($literal in @('Maturity stage:', 'Version scheme:', 'Current version:', 'Release authority:', 'Visual UI:')) {
    if ($product.IndexOf($literal, [StringComparison]::Ordinal) -lt 0) {
        throw "[AZWF-PRODUCT] docs/product/index.md is missing '$literal'."
    }
}

$capabilityLines = @(Get-Content -LiteralPath (Require-File 'docs/product/capabilities.md') | Where-Object { $_ -match '^\| [A-Z]+-[0-9]{2} \|' })
if ($capabilityLines.Count -ne 213) { throw "[AZWF-CAPABILITY] Expected 213 rows; found $($capabilityLines.Count)." }
$ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in $capabilityLines) {
    $cells = @($line.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() })
    if ($cells.Count -ne 7) { throw "[AZWF-CAPABILITY] Capability row has $($cells.Count) cells: $line" }
    if (-not $ids.Add($cells[0])) { throw "[AZWF-CAPABILITY] Duplicate capability ID: $($cells[0])" }
    if ([string]::IsNullOrWhiteSpace($cells[1])) { throw "[AZWF-CAPABILITY] $($cells[0]) has no outcome." }
    if ($cells[2] -cnotin @('Now', 'Next', 'Later', 'Not planned')) { throw "[AZWF-CAPABILITY] $($cells[0]) has invalid horizon '$($cells[2])'." }
    if ($cells[3] -cne 'unallocated' -and $cells[3] -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
        throw "[AZWF-CAPABILITY] $($cells[0]) has invalid target release '$($cells[3])'."
    }
    if ($cells[4] -notmatch '^\[Area\]\((?:areas/[a-z0-9-]+\.md|boundaries\.md)\)$') { throw "[AZWF-CAPABILITY] $($cells[0]) has no canonical product-area link." }
}

if (Test-Path -LiteralPath (Join-Path $root 'docs/plans')) {
    throw '[AZWF-PLAN-CONVERSION] Superseded docs/plans remains.'
}
$archiveFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'docs/history/plans') -Recurse -File)
if ($archiveFiles.Count -ne 41) {
    throw "[AZWF-PLAN-CONVERSION] Expected 41 default-archived artifacts; found $($archiveFiles.Count)."
}
$convertedPlanArtifacts = @(
    'docs/product/open-decisions.md',
    'docs/product/v1-gap.md',
    'docs/product/boundaries.md',
    'design/product/requirements.md',
    'design/product/ui-spec.md',
    'design/product/traceability-matrix.md',
    'design/references/directions/case-first.md',
    'design/references/directions/operations-first.md',
    'design/references/directions/worklist-first.md',
    'design/references/mockups/candidate-a-operations-first.png',
    'design/references/mockups/candidate-b-worklist-first.png',
    'design/references/mockups/candidate-c-case-first.png',
    'docs/runbooks/testing/README.md',
    'docs/runbooks/testing/local-testing.md'
)
foreach ($relativePath in $convertedPlanArtifacts) { [void](Require-File $relativePath) }
if (($archiveFiles.Count + $convertedPlanArtifacts.Count) -ne 55) {
    throw '[AZWF-PLAN-CONVERSION] The 55-artifact source tree does not have exact destination parity.'
}

$expectedForms = [ordered]@{
    'feature' = 'type:feature'
    'bug' = 'type:bug'
    'task' = 'type:task'
    'decision' = 'type:decision'
}
foreach ($kind in $expectedForms.Keys) {
    $path = Require-File ".github/ISSUE_TEMPLATE/$kind.yml"
    try { $form = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json }
    catch { throw "[AZWF-ISSUE-FORM] $kind.yml is not strict JSON-compatible YAML: $($_.Exception.Message)" }
    if (@($form.labels).Count -ne 1 -or $form.labels[0] -cne $expectedForms[$kind]) {
        throw "[AZWF-ISSUE-FORM] $kind.yml must declare only $($expectedForms[$kind])."
    }
    $idsInForm = @($form.body |
        Where-Object { $null -ne $_.PSObject.Properties['id'] } |
        ForEach-Object { $_.id })
    if ($idsInForm.Count -ne @($idsInForm | Sort-Object -Unique).Count) {
        throw "[AZWF-ISSUE-FORM] $kind.yml contains duplicate body IDs."
    }
}
$issueConfig = Get-Content -LiteralPath (Require-File '.github/ISSUE_TEMPLATE/config.yml') -Raw | ConvertFrom-Json
if ($issueConfig.blank_issues_enabled -ne $false) { throw '[AZWF-ISSUE-FORM] Blank issues must be disabled.' }

$prTemplate = Get-Content -LiteralPath (Require-File '.github/pull_request_template.md') -Raw
foreach ($heading in @('Outcome', 'Scope', 'Work identity', 'Documentation impact', 'Verification', 'Review', 'Azure', 'Checklist')) {
    Require-HeadingOnce -Content $prTemplate -Heading $heading -Owner '.github/pull_request_template.md'
}

$changeFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'docs/changes') -File -Filter '*.md' | Where-Object Name -ne 'README.md')
foreach ($file in $changeFiles) {
    if ($file.BaseName -notmatch '^\d{4}-\d{2}-\d{2}-[a-z0-9]+(?:-[a-z0-9]+)*$') {
        throw "[AZWF-CHANGE] Invalid change-record filename: $($file.Name)"
    }
    $content = Get-Content -LiteralPath $file.FullName -Raw
    $yaml = [regex]::Match($content, '(?s)^# Change: .+?\r?\n\r?\n```yaml\r?\n(?<body>.+?)\r?\n```')
    if (-not $yaml.Success) { throw "[AZWF-CHANGE] $($file.Name) has no leading YAML identity block." }
    $fields = @{}
    foreach ($line in $yaml.Groups['body'].Value -split '\r?\n') {
        if ($line -match '^(?<key>[a-z_]+):\s*(?<value>.+)$') { $fields[$Matches.key] = $Matches.value.Trim() }
    }
    foreach ($key in @('id','type','status','risk','created','updated','issue','pull_request','baseline','target_release','roadmap_horizon','mode','supersedes','superseded_by')) {
        if (-not $fields.ContainsKey($key)) { throw "[AZWF-CHANGE] $($file.Name) is missing $key." }
    }
    if ($fields.id -cne $file.BaseName) { throw "[AZWF-CHANGE] $($file.Name) ID does not match filename." }
    if ($fields.mode -cne $productMode) { throw "[AZWF-CHANGE] $($file.Name) mode disagrees with product mode." }
    foreach ($heading in @('Summary','Scope','Authorities, current state, and constraints','Acceptance criteria','Plan','Data, failure, and recovery','UI/UX contract','Azure impact','Decisions and conflicts','Implementation','Verification','Independent review','Documentation and work tracking','Outcome','Blocker or follow-ups')) {
        Require-HeadingOnce -Content $content -Heading $heading -Owner "docs/changes/$($file.Name)"
    }
}

$decisionFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'docs/decisions') -File -Filter '*.md' | Where-Object Name -ne 'README.md')
foreach ($file in $decisionFiles) {
    if ($file.BaseName -notmatch '^(?<number>\d{4})-(?<slug>[a-z0-9]+(?:-[a-z0-9]+)*)$') { throw "[AZWF-ADR] Invalid ADR filename: $($file.Name)" }
    $content = Get-Content -LiteralPath $file.FullName -Raw
    if ($content -notmatch "(?m)^# $($Matches.number): .+$") { throw "[AZWF-ADR] $($file.Name) title does not match its number." }
    if ([regex]::Matches($content, '(?m)^- Date: \d{4}-\d{2}-\d{2}\s*$').Count -ne 1) { throw "[AZWF-ADR] $($file.Name) requires one ISO date." }
    if ([regex]::Matches($content, '(?m)^- Status: (proposed|accepted|superseded|rejected|deprecated)\s*$').Count -ne 1) { throw "[AZWF-ADR] $($file.Name) requires one supported status." }
    foreach ($heading in @('Context','Decision','Consequences')) { Require-HeadingOnce -Content $content -Heading $heading -Owner "docs/decisions/$($file.Name)" }
}

$mistakes = Get-Content -LiteralPath (Require-File 'docs/agent-mistakes.md') -Raw
foreach ($heading in @('Purpose','What to record','What not to record','Incident template','Entries')) {
    Require-HeadingOnce -Content $mistakes -Heading $heading -Owner 'docs/agent-mistakes.md'
}
if ($mistakes.IndexOf('Append incidents below; do not edit earlier entries.', [StringComparison]::Ordinal) -lt 0) {
    throw '[AZWF-MISTAKES] Append-only marker is missing.'
}

$operatorRoot = Join-Path $root 'docs/operator-notes'
$operatorFiles = @(Get-ChildItem -LiteralPath $operatorRoot -Recurse -File -Filter '*.md')
if ($operatorFiles.Count -ne 17) {
    throw "[AZWF-OPERATOR-NOTES] Expected 17 consolidated operator-note files; found $($operatorFiles.Count)."
}
foreach ($file in $operatorFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    if ([regex]::Matches($content, '(?m)^# .+\s*$').Count -ne 1) {
        throw "[AZWF-OPERATOR-NOTES] $($file.FullName) must contain exactly one H1."
    }
}
foreach ($legacyPath in @('collision-engineers-process', 'development-notes', 'systems-used')) {
    if (Test-Path -LiteralPath (Join-Path $operatorRoot $legacyPath)) {
        throw "[AZWF-OPERATOR-NOTES] Superseded operator-note path remains: $legacyPath"
    }
}
$operatorIndex = Get-Content -LiteralPath (Require-File 'docs/operator-notes/README.md') -Raw
foreach ($source in @(
        'collision-engineers-process/process-overview.md',
        'collision-engineers-process/initial-case-intake/*',
        'collision-engineers-process/case-guide/*',
        'collision-engineers-process/inspection-address/inspection-address-overview.md',
        'reserved-terms.md',
        'development-notes/required-features-overview.md',
        'development-notes/rules-to-follow.md',
        'systems-used/*',
        'development-notes/Untitled.md'
    )) {
    if ($operatorIndex.IndexOf($source, [StringComparison]::Ordinal) -lt 0) {
        throw "[AZWF-OPERATOR-NOTES] Source map is missing $source."
    }
}
$requiredCapabilities = Get-Content -LiteralPath (Require-File 'docs/operator-notes/product-requirements/required-capabilities.md')
if (@($requiredCapabilities | Where-Object { $_ -match '^\d+\. ' }).Count -ne 22) {
    throw '[AZWF-OPERATOR-NOTES] Required operator capability list must retain 22 items.'
}
$operatorAssertions = [ordered]@{
    'docs/operator-notes/business-process/case-lifecycle.md' = @('## Stage 0 — Triage', '## Stage 1 — Receiving instructions or images', '## Stage 1.5 — Chasing for details, images, or documents', '## Stage 2 — Inspection', '## Stage 3 — Post-report')
    'docs/operator-notes/business-process/case-types-and-references.md' = @('### Inspection', '### Audit', '### Audit + Inspection', '### Diminution', '### Commercial')
    'docs/operator-notes/business-process/reserved-terms.md' = @('- Audit', '- Triage')
    'docs/operator-notes/systems-and-integrations/outlook.md' = @('desk@collisionengineers.co.uk', 'engineers@collisionengineers.co.uk', 'info@collisionengineers.co.uk', 'instructions@collisionengineers.co.uk')
}
foreach ($relativePath in $operatorAssertions.Keys) {
    $content = Get-Content -LiteralPath (Require-File $relativePath) -Raw
    foreach ($literal in $operatorAssertions[$relativePath]) {
        if ($content.IndexOf($literal, [StringComparison]::Ordinal) -lt 0) {
            throw "[AZWF-OPERATOR-NOTES] $relativePath is missing retained authority: $literal"
        }
    }
}

$portableFiles = @('AGENTS.md', 'docs/index.md', 'docs/product/index.md', 'docs/roadmap.md', 'docs/architecture.md', 'docs/operations.md', '.github/pull_request_template.md')
foreach ($relativePath in $portableFiles) {
    $content = Get-Content -LiteralPath (Require-File $relativePath) -Raw
    $contentWithoutUrls = [regex]::Replace($content, '(?i)https?://[^\s)]+', '')
    if ($contentWithoutUrls -match '(?im)(?:[A-Z]:\\|\\\\|/home/[^/\s]+|/Users/[^/\s]+|~[/\\])') {
        throw "[AZWF-PORTABLE-PATH] $relativePath contains a workstation-specific path."
    }
}

Write-Host "Azure Workflow documentation is valid: $($capabilityLines.Count) capabilities, $($operatorFiles.Count) consolidated operator notes, $($changeFiles.Count) change record(s), $($decisionFiles.Count) canonical ADR(s), 4 issue forms." -ForegroundColor Green
