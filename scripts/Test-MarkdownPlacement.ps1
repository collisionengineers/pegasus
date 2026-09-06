[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Base,
    [Parameter(Mandatory)][string] $Head,
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-CommitRevision {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Revision
    )

    if ([string]::IsNullOrWhiteSpace($Revision) -or $Revision -match '^0+$') {
        throw "Markdown placement cannot be verified: $Name revision is missing or all zeroes."
    }

    $null = git rev-parse --verify --quiet "$Revision`^{commit}"
    if ($LASTEXITCODE -ne 0) {
        throw "Markdown placement cannot be verified: $Name revision '$Revision' is not an available commit."
    }
}

function Test-AllowedMarkdownPath {
    param([Parameter(Mandatory)][string] $Path)

    $normalized = $Path.Replace('\', '/') -replace '^\./', ''
    return $normalized -ceq 'NOW.md' -or
        $normalized -match '^((docs/(prd|frd|adr|design))|workspaces/document-extraction|\.design-sync|\.grok|\.stitch|design/planning-and-old-designs)/.+\.md$'
}

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
Push-Location $resolvedRoot
try {
    $insideWorkTree = git rev-parse --is-inside-work-tree 2>$null
    if ($LASTEXITCODE -ne 0 -or $insideWorkTree -ne 'true') {
        throw "Markdown placement cannot be verified: '$resolvedRoot' is not a Git worktree."
    }

    Assert-CommitRevision -Name 'base' -Revision $Base
    Assert-CommitRevision -Name 'head' -Revision $Head

    $changes = @(git -c core.quotePath=false diff --name-status --find-renames --find-copies --find-copies-harder $Base $Head)
    if ($LASTEXITCODE -ne 0) {
        throw "Markdown placement cannot be verified: Git could not compare '$Base' with '$Head'."
    }

    $violations = [System.Collections.Generic.List[string]]::new()
    foreach ($change in $changes) {
        if ([string]::IsNullOrWhiteSpace($change)) {
            continue
        }

        $fields = $change -split "`t"
        $status = $fields[0]
        $kind = $status.Substring(0, 1)
        if ($kind -notin @('A', 'C', 'R')) {
            continue
        }

        $expectedFields = if ($kind -in @('C', 'R')) { 3 } else { 2 }
        if ($fields.Count -ne $expectedFields) {
            throw "Markdown placement cannot be verified: unrecognized Git change record '$change'."
        }

        $destination = $fields[-1].Replace('\', '/')
        if ([System.IO.Path]::GetExtension($destination) -ieq '.md' -and
            -not (Test-AllowedMarkdownPath -Path $destination)) {
            $violations.Add($destination)
        }
    }

    $invalidPaths = @($violations | Sort-Object -Unique)
    if ($invalidPaths.Count -gt 0) {
        $details = $invalidPaths | ForEach-Object { "  - $_" }
        throw "New Markdown files must be placed under an approved documentation or integration root:`n$($details -join "`n")"
    }

    Write-Output "Markdown placement passed for $Base..$Head."
}
finally {
    Pop-Location
}
