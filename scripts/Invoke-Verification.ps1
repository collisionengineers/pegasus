#!/usr/bin/env pwsh
<#
.SYNOPSIS
Chooses and runs the verification a change set actually needs, and refuses the
verification it does not.

.DESCRIPTION
docs/engineering.md already says prose does not require a .NET build, that
qualifying exact-head CI evidence may be reused, and that a step which only
re-runs what CI runs is deleted. Nothing computed any of that, so the default
was a full local build. This computes it.

In order:

  1. Prose only. Runs the documentation link check and refuses to build.
  2. Exact-head CI evidence. If this commit already has a green repository-check
     run, prints its per-job verdicts and stops. -Force overrides.
  3. Otherwise a focused run over the test classes the changed files own.
  4. -Full runs the canonical whole-solution triple, but only while holding the
     host slot, because every worktree on this machine shares one LocalDB
     instance and one set of build outputs.

Routing reuses scripts/Get-CiChangeFlags.ps1, the classifier CI uses, so local
and hosted selection cannot disagree.

.EXAMPLE
pwsh ./scripts/Invoke-Verification.ps1

.EXAMPLE
pwsh ./scripts/Invoke-Verification.ps1 -WhatIf

.EXAMPLE
pwsh ./scripts/Invoke-Verification.ps1 -Full
#>
[CmdletBinding()]
param(
    # The long-lived branch this change set is measured against.
    [string] $Base = 'origin/dev',

    # Run the whole-solution triple. Requires the host slot.
    [switch] $Full,

    # Run locally even though exact-head CI evidence exists.
    [switch] $Force,

    # Print the selection and the commands, run nothing.
    [switch] $WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (& git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
$slotPath = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.pegasus/verification-host-slot.json'

function Write-Heading {
    param([Parameter(Mandatory)][string] $Text)
    Write-Host ''
    Write-Host $Text
    Write-Host ('-' * $Text.Length)
}

function Invoke-Step {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][scriptblock] $Command
    )

    Write-Host "  $Name"
    if ($WhatIf) {
        Write-Host "    would run: $($Command.ToString().Trim())"
        return
    }

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

# ---------------------------------------------------------------- change set

$head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$mergeBase = (& git -C $repositoryRoot merge-base HEAD $Base 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $mergeBase) {
    Write-Warning "No merge base with '$Base'; classifying the working tree only."
    $mergeBase = 'HEAD'
}
else {
    $mergeBase = $mergeBase.Trim()
}

# Committed against the base, plus whatever is uncommitted: a change is not
# exempt from its own verification because it has not been committed yet.
$changed = @(
    & git -C $repositoryRoot diff --name-only "$mergeBase..HEAD"
    & git -C $repositoryRoot diff --name-only HEAD
    & git -C $repositoryRoot ls-files --others --exclude-standard
) | Where-Object { $_ } | Sort-Object -Unique

$flags = & (Join-Path $PSScriptRoot 'Get-CiChangeFlags.ps1') -ChangedPath $changed

Write-Heading "Change set"
Write-Host "  head        $($head.Substring(0, 9))"
Write-Host "  base        $Base at $($mergeBase.Substring(0, [math]::Min(9, $mergeBase.Length)))"
Write-Host "  paths       $($changed.Count)"
Write-Host "  lanes       build=$($flags.Build) infrastructure=$($flags.Infrastructure) localDevelopment=$($flags.LocalDevelopment) referenceData=$($flags.ReferenceData)"

# ------------------------------------------------------------- cheap invariants

Write-Heading "Cheap invariants"
if (@($changed | Where-Object { $_ -like '*.md' }).Count -gt 0 -or $Full) {
    Invoke-Step 'Documentation links' { & (Join-Path $PSScriptRoot 'Test-DocumentationLinks.ps1') }
}
else {
    Write-Host '  Documentation links: no Markdown changed'
}

if ($flags.LocalDevelopment) {
    Invoke-Step 'LocalDB lifecycle classifier tests' { & (Join-Path $PSScriptRoot 'Test-PegasusPlatform.ps1') }
}
if ($flags.ReferenceData) {
    Invoke-Step 'Provider-reference generator tests' { python -m unittest discover -s (Join-Path $repositoryRoot 'scripts/reference_data/tests') -p 'test_*.py' }
}
if ($flags.Infrastructure) {
    Invoke-Step 'Change-classification regression tests' { & (Join-Path $PSScriptRoot 'Test-CiChangeFlags.ps1') }
    Invoke-Step 'Migration runtime-grant check' { & (Join-Path $PSScriptRoot 'Test-MigrationGrants.ps1') }
}

if (-not $flags.Build -and -not $Full) {
    Write-Heading "No .NET verification required"
    Write-Host '  Nothing build-relevant changed, so there is nothing a restore, build'
    Write-Host '  or test run could establish (docs/engineering.md, Verification policy:'
    Write-Host '  "Ordinary prose and links do not require .NET restore/build/test").'
    exit 0
}

# --------------------------------------------------------- exact-head evidence

if (-not $Force) {
    Write-Heading "Exact-head CI evidence"
    $runs = @()
    $reachedGitHub = $false
    try {
        $listed = & gh run list --commit $head --workflow ci.yml --limit 5 --json databaseId,conclusion,status,event 2>$null
        if ($LASTEXITCODE -eq 0) {
            $reachedGitHub = $true
            $runs = @($listed | ConvertFrom-Json)
        }
    }
    catch {
        $reachedGitHub = $false
    }

    if (-not $reachedGitHub) {
        Write-Host '  gh unavailable or unauthenticated; cannot reuse CI evidence.'
    }

    $green = @($runs | Where-Object { $_.status -eq 'completed' -and $_.conclusion -eq 'success' })
    if ($green.Count -gt 0) {
        $runId = $green[0].databaseId
        Write-Host "  run $runId is green at this exact commit."
        $jobs = & gh run view $runId --json jobs --jq '.jobs[] | "    \(.conclusion)\t\(.name)"' 2>$null
        $jobs | ForEach-Object { Write-Host $_ }
        Write-Host ''
        Write-Host '  Reusing it rather than repeating it (docs/engineering.md, Delivery'
        Write-Host '  evidence). Pass -Force to run locally anyway.'
        exit 0
    }
    elseif ($runs.Count -eq 0) {
        Write-Host '  no run at this commit yet, so there is nothing to reuse.'
    }
    else {
        Write-Host "  $($runs.Count) run(s) at this commit, none green."
    }
}

# ----------------------------------------------------------------- .NET lanes

if ($Full) {
    Write-Heading "Host slot"
    if (Test-Path -LiteralPath $slotPath) {
        $slot = Get-Content -Raw -LiteralPath $slotPath | ConvertFrom-Json
        throw "The host slot is held by '$($slot.holder)' at $($slot.acquiredUtc) in $($slot.worktree). One whole-solution run at a time: every worktree on this machine shares one LocalDB instance. Release it, or run without -Full."
    }

    $live = @(Get-Process -Name 'testhost', 'vstest.console' -ErrorAction SilentlyContinue)
    if ($live.Count -gt 0) {
        throw "$($live.Count) test host process(es) are already running. Refusing to compete for the shared LocalDB instance and build outputs."
    }

    if (-not $WhatIf) {
        New-Item -ItemType Directory -Force -Path (Split-Path $slotPath -Parent) | Out-Null
        [pscustomobject]@{
            holder = "$env:USERNAME on $env:COMPUTERNAME"
            acquiredUtc = [DateTime]::UtcNow.ToString('o')
            worktree = $repositoryRoot
            head = $head
        } | ConvertTo-Json | Set-Content -LiteralPath $slotPath -Encoding utf8NoBOM
        Write-Host "  acquired $slotPath"
    }

    try {
        Write-Heading "Whole-solution verification"
        Invoke-Step 'Locked restore' { dotnet restore (Join-Path $repositoryRoot 'Pegasus.slnx') --locked-mode }
        Invoke-Step 'Release build' { dotnet build (Join-Path $repositoryRoot 'Pegasus.slnx') --configuration Release --no-restore }
        Invoke-Step 'Solution tests' { dotnet test (Join-Path $repositoryRoot 'Pegasus.slnx') --configuration Release --no-build --filter "Category!=Corpus" }
    }
    finally {
        if (-not $WhatIf -and (Test-Path -LiteralPath $slotPath)) {
            Remove-Item -LiteralPath $slotPath -Force
            Write-Host "  released $slotPath"
        }
    }

    exit 0
}

# Focused: the test classes the changed files own. A changed test class is its
# own owner; a changed source file is owned by same-named test classes.
$stems = @($changed |
    Where-Object { $_ -like 'src/*.cs' -or $_ -like 'tests/*.cs' } |
    ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) } |
    Where-Object { $_ -and $_ -notlike '*.g' } |
    Sort-Object -Unique)

$testClasses = @(& git -C $repositoryRoot ls-files --cached --others --exclude-standard 'tests/*Tests.cs' |
    ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) } |
    Sort-Object -Unique)

$owning = @($testClasses | Where-Object {
    $class = $_
    $stems | Where-Object { $class -eq $_ -or $class -like "$_*Tests" } | Select-Object -First 1
})

Write-Heading "Focused verification"
if ($owning.Count -eq 0) {
    Write-Host '  No test class maps to the changed files by name. Either name the'
    Write-Host '  classes yourself, or run with -Full once CI has had the change.'
    Write-Host "  Changed stems: $($stems -join ', ')"
    exit 0
}

Write-Host "  $($owning.Count) owning test class(es): $($owning -join ', ')"
$filter = ($owning | ForEach-Object { "FullyQualifiedName~$_" }) -join '|'

Invoke-Step 'Locked restore' { dotnet restore (Join-Path $repositoryRoot 'Pegasus.slnx') --locked-mode }
Invoke-Step 'Release build' { dotnet build (Join-Path $repositoryRoot 'Pegasus.slnx') --configuration Release --no-restore }
Invoke-Step 'Focused tests' { dotnet test (Join-Path $repositoryRoot 'Pegasus.slnx') --configuration Release --no-build --filter "(Category!=Corpus)&($filter)" }

Write-Host ''
Write-Host 'Focused only. Pushing is what buys the whole suite; read CI for that verdict.'
