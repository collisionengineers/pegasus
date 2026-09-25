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

  1. The cheap invariants the change set touches, including the documentation
     link check, which always runs, as it does in CI.
  2. Nothing build- or infrastructure-relevant changed: stop, and say so.
  3. Exact-head CI evidence. With a clean tree, a run at HEAD in which every
     job the change set routes to succeeded is reused instead of repeated: the
     unit job and every SQL shard for the build lane, the infrastructure job
     for the infrastructure lane. A green run whose required jobs were deferred
     or skipped is not evidence. A run whose build lane is green but whose
     required infrastructure job did not run is reused for the build lane.
     -Force overrides for the build lane. A change set whose build lane is
     evidenced, or that routes only to the infrastructure lane, ends here,
     pointed at the infrastructure job: only CI runs it.
  4. Otherwise a focused run over the test classes the changed files own,
     priced first from scripts/test-shard-durations.json; a focused run too
     expensive to be worth doing here is refused in favour of pushing.
  5. -Full runs the canonical whole-solution triple, but only while holding the
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

    # Run locally even though exact-head CI evidence exists, or the focused
    # run is priced above the local budget.
    [switch] $Force,

    # Print the selection and the commands, run nothing.
    [switch] $WhatIf,

    # Recorded test time above which a focused run is left to CI.
    [int] $FocusedBudgetMinutes = 10
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

# Uncommitted work is part of the change set: it is not exempt from its own
# verification because it has not been committed yet.
$uncommitted = @(@(
    & git -C $repositoryRoot diff --name-only HEAD
    & git -C $repositoryRoot ls-files --others --exclude-standard
) | Where-Object { $_ })

$changed = @(@(
    & git -C $repositoryRoot diff --name-only "$mergeBase..HEAD"
    $uncommitted
) | Where-Object { $_ } | Sort-Object -Unique)

$flags = & (Join-Path $PSScriptRoot 'Get-CiChangeFlags.ps1') -ChangedPath $changed

Write-Heading 'Change set'
Write-Host "  head        $($head.Substring(0, 9))$(if ($uncommitted.Count) { " plus $($uncommitted.Count) uncommitted path(s)" })"
Write-Host "  base        $Base at $($mergeBase.Substring(0, [math]::Min(9, $mergeBase.Length)))"
Write-Host "  paths       $($changed.Count)"
Write-Host "  lanes       build=$($flags.Build) infrastructure=$($flags.Infrastructure) localDevelopment=$($flags.LocalDevelopment) referenceData=$($flags.ReferenceData)"

# ------------------------------------------------------------- cheap invariants

Write-Heading 'Cheap invariants'
# Always, as CI does: renaming or deleting a file can break a link without any
# Markdown file changing, and the check takes seconds.
Invoke-Step 'Documentation links' { & (Join-Path $PSScriptRoot 'Test-DocumentationLinks.ps1') }

if ($flags.LocalDevelopment) {
    Invoke-Step 'LocalDB lifecycle classifier tests' { & (Join-Path $PSScriptRoot 'Test-PegasusPlatform.ps1') }
}
if ($flags.ReferenceData) {
    Invoke-Step 'Provider-reference generator tests' {
        Push-Location $repositoryRoot
        try { python -m unittest discover -s scripts/reference_data/tests -p 'test_*.py' }
        finally { Pop-Location }
    }
}
if ($flags.Infrastructure) {
    Invoke-Step 'Change-classification regression tests' { & (Join-Path $PSScriptRoot 'Test-CiChangeFlags.ps1') }
    Invoke-Step 'Migration runtime-grant check' { & (Join-Path $PSScriptRoot 'Test-MigrationGrants.ps1') }
}

if (-not $flags.Build -and -not $flags.Infrastructure -and -not $Full) {
    Write-Heading 'No .NET verification required'
    Write-Host '  Nothing build-relevant changed, so there is nothing a restore, build'
    Write-Host '  or test run could establish (docs/engineering.md, Verification policy:'
    Write-Host '  "Ordinary prose and links do not require .NET restore/build/test").'
    exit 0
}

# --------------------------------------------------------- exact-head evidence

# Each lane is evidenced by its own jobs: the build lane by the unit job and
# every SQL shard, the infrastructure lane by the infrastructure job, which CI
# gates on its own path flag. With no build lane there is no local run to fall
# back to, so -Force does not skip the lookup.
$buildLaneRequired = [bool]($flags.Build -or $Full)
$requiredJobs = @(
    if ($buildLaneRequired) { 'the unit and SQL lanes' }
    if ($flags.Infrastructure) { 'the infrastructure job' }
) -join ' and '

$buildLaneReused = $false
if (-not $Force -or -not $buildLaneRequired) {
    Write-Heading 'Exact-head CI evidence'
    if ($uncommitted.Count -gt 0) {
        Write-Host "  not applicable: $($uncommitted.Count) uncommitted path(s) are in no run at HEAD."
    }
    else {
        $runs = @()
        $listed = & gh run list --commit $head --workflow ci.yml --limit 5 --json databaseId,conclusion,status 2>$null
        if ($LASTEXITCODE -eq 0) {
            $runs = @($listed | ConvertFrom-Json)
        }
        else {
            Write-Host '  gh unavailable or unauthenticated; cannot reuse CI evidence.'
        }

        # A green run is not necessarily evidence. A stacked pull request's run is
        # green with the unit and SQL lanes deferred, and a skipped lane proves
        # nothing (docs/engineering.md: evidence "for that job only"). Only a run
        # in which every job this change set routes to itself succeeded counts.
        $qualifying = $null
        $buildLaneRun = $null
        foreach ($run in @($runs | Where-Object { $_.status -eq 'completed' -and $_.conclusion -eq 'success' })) {
            $jobs = @(& gh run view $run.databaseId --json jobs 2>$null | ConvertFrom-Json |
                ForEach-Object { $_.jobs } |
                ForEach-Object { [pscustomobject]@{ Name = $_.name; Conclusion = $_.conclusion } })
            $lanes = @($jobs | Where-Object { $_.Name -eq 'unit' -or $_.Name -like 'sql-integration (*' })
            $buildLaneGreen = $lanes.Count -gt 1 -and @($lanes | Where-Object { $_.Conclusion -ne 'success' }).Count -eq 0
            $infrastructureGreen = @($jobs | Where-Object { $_.Name -eq 'infrastructure' -and $_.Conclusion -eq 'success' }).Count -gt 0
            if ((-not $buildLaneRequired -or $buildLaneGreen) -and (-not $flags.Infrastructure -or $infrastructureGreen)) {
                $qualifying = [pscustomobject]@{ Id = $run.databaseId; Jobs = $jobs }
                break
            }
            # A stack's tip classifies only its own files, so its run can skip
            # the infrastructure job a lower link routes to. Its green build
            # lane is still evidence for that lane.
            if ($buildLaneRequired -and $buildLaneGreen -and -not $buildLaneRun) {
                $buildLaneRun = [pscustomobject]@{ Id = $run.databaseId; Jobs = $jobs }
            }
        }

        if ($qualifying) {
            Write-Host "  run $($qualifying.Id) ran $requiredJobs green at this exact commit."
            $qualifying.Jobs | ForEach-Object { Write-Host ('    {0,-10} {1}' -f $_.Conclusion, $_.Name) }
            Write-Host ''
            Write-Host '  Reusing it rather than repeating it (docs/engineering.md, Delivery'
            Write-Host "  evidence).$(if ($buildLaneRequired) { ' Pass -Force to run locally anyway.' })"
            exit 0
        }
        elseif ($buildLaneRun) {
            Write-Host "  run $($buildLaneRun.Id) ran the unit and SQL lanes green at this exact commit;"
            Write-Host '  the infrastructure job this change set also routes to did not run there.'
            $buildLaneRun.Jobs | ForEach-Object { Write-Host ('    {0,-10} {1}' -f $_.Conclusion, $_.Name) }
            Write-Host ''
            Write-Host '  Reusing it for the build lane rather than repeating it (docs/engineering.md,'
            Write-Host '  Delivery evidence). Pass -Force to run locally anyway.'
            $buildLaneReused = $true
        }
        elseif (@($runs | Where-Object { $_.conclusion -eq 'success' }).Count -gt 0) {
            Write-Host "  a run at this commit is green, but $requiredJobs did not all run"
            Write-Host '  (deferred on a stacked pull request, or path-skipped): not evidence.'
        }
        elseif ($runs.Count -eq 0) {
            Write-Host '  no run at this commit yet, so there is nothing to reuse.'
        }
        else {
            Write-Host "  $($runs.Count) run(s) at this commit, none green."
        }
    }

    if (-not $buildLaneRequired -or $buildLaneReused) {
        Write-Heading 'Infrastructure verification is CI-owned'
        Write-Host "  $(if ($buildLaneReused) { 'The build lane is evidenced above, but the change set also routes to the' } else { 'Nothing build-relevant changed, but the change set routes to the' })"
        Write-Host '  infrastructure lane, whose job only CI runs (.github/workflows/ci.yml).'
        Write-Host '  It is unverified until the infrastructure job is green at the exact'
        Write-Host '  commit you push; push, then read it with scripts/Get-CiStatus.ps1.'
        exit 0
    }
}

# ----------------------------------------------------------------- .NET lanes

if ($Full) {
    Write-Heading 'Host slot'
    if (Test-Path -LiteralPath $slotPath) {
        $slot = Get-Content -Raw -LiteralPath $slotPath | ConvertFrom-Json
        $holder = if ($slot.PSObject.Properties['pid']) { Get-Process -Id $slot.pid -ErrorAction SilentlyContinue }
        if ($holder) {
            throw "The host slot is held by '$($slot.holder)' (process $($slot.pid)) since $($slot.acquiredUtc) in $($slot.worktree). One whole-solution run at a time: every worktree on this machine shares one LocalDB instance."
        }

        # The holder is gone - killed, or its terminal closed mid-run - so its
        # finally block never released the slot. A lock nobody holds must not
        # block every later run.
        Write-Warning "Releasing a stale host slot left by '$($slot.holder)' in $($slot.worktree)."
        if (-not $WhatIf) {
            Remove-Item -LiteralPath $slotPath -Force
        }
    }

    $live = @(Get-Process -Name 'testhost', 'vstest.console' -ErrorAction SilentlyContinue)
    if ($live.Count -gt 0) {
        throw "$($live.Count) test host process(es) are already running. Refusing to compete for the shared LocalDB instance and build outputs."
    }

    $acquired = $false
    if (-not $WhatIf) {
        New-Item -ItemType Directory -Force -Path (Split-Path $slotPath -Parent) | Out-Null
        $record = [pscustomobject]@{
            holder = "$env:USERNAME on $env:COMPUTERNAME"
            pid = $PID
            acquiredUtc = [DateTime]::UtcNow.ToString('o')
            worktree = $repositoryRoot
            head = $head
        } | ConvertTo-Json

        # CreateNew fails when the file exists, so two sessions that both saw an
        # empty slot a moment ago cannot both take it.
        try {
            $stream = [IO.File]::Open($slotPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
        }
        catch [IO.IOException] {
            throw 'Another session took the host slot a moment ago. Try again when it finishes.'
        }
        try {
            $bytes = [Text.Encoding]::UTF8.GetBytes($record)
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $stream.Dispose()
        }
        $acquired = $true
        Write-Host "  acquired $slotPath"
    }

    try {
        Write-Heading 'Whole-solution verification'
        Invoke-Step 'Locked restore' { dotnet restore (Join-Path $repositoryRoot 'Pegasus.slnx') --locked-mode }
        Invoke-Step 'Release build' { dotnet build (Join-Path $repositoryRoot 'Pegasus.slnx') --configuration Release --no-restore }
        Invoke-Step 'Solution tests' { dotnet test (Join-Path $repositoryRoot 'Pegasus.slnx') --configuration Release --no-build --filter "Category!=Corpus" }
    }
    finally {
        if ($acquired) {
            Remove-Item -LiteralPath $slotPath -Force -ErrorAction SilentlyContinue
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

Write-Heading 'Focused verification'
if ($owning.Count -eq 0) {
    Write-Host '  No test class maps to the changed files by name. Either name the'
    Write-Host '  classes yourself, or push and let CI run the lanes.'
    Write-Host "  Changed stems: $($stems -join ', ')"
    exit 0
}

# Price the run from the SQL lane's recorded durations before spending it. A
# focused run that costs as much as a shard is not a saving over CI.
$durationsPath = Join-Path $PSScriptRoot 'test-shard-durations.json'
$priced = 0.0
if (Test-Path -LiteralPath $durationsPath) {
    $table = Get-Content -Raw -LiteralPath $durationsPath | ConvertFrom-Json
    foreach ($entry in $table.PSObject.Properties) {
        if ($owning -contains ($entry.Name -split '\.')[-1]) {
            $priced += [double] $entry.Value
        }
    }
}

Write-Host "  $($owning.Count) owning test class(es): $($owning -join ', ')"
Write-Host ('  recorded SQL-lane test time for them: {0:N1} min' -f ($priced / 60))
if ($priced -gt $FocusedBudgetMinutes * 60 -and -not $Force) {
    Write-Host ''
    Write-Host "  Above the $FocusedBudgetMinutes-minute local budget: push and let CI run it, or pass"
    Write-Host '  -Force to run it here anyway.'
    exit 0
}

$filter = ($owning | ForEach-Object { "FullyQualifiedName~$_" }) -join '|'

Invoke-Step 'Locked restore' { dotnet restore (Join-Path $repositoryRoot 'Pegasus.slnx') --locked-mode }
Invoke-Step 'Release build' { dotnet build (Join-Path $repositoryRoot 'Pegasus.slnx') --configuration Release --no-restore }
Invoke-Step 'Focused tests' { dotnet test (Join-Path $repositoryRoot 'Pegasus.slnx') --configuration Release --no-build --filter "(Category!=Corpus)&($filter)" }

Write-Host ''
Write-Host 'Focused only. Pushing is what buys the whole suite; read CI for that verdict.'
