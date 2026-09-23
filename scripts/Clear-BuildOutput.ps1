#!/usr/bin/env pwsh
<#
.SYNOPSIS
Reports, and on request removes, the bin and obj directories in this
repository's git worktrees.

.DESCRIPTION
Nothing shares build output between worktrees: there is no ArtifactsPath, so
each one compiles the whole solution into its own bin and obj. Seventeen
worktrees held about 56 GB that way, on a disk with 43 GB free, and a near-full
disk is what turns a long run into a failed one.

Reports by default. -Execute is required to delete anything, and it refuses
while a test host is running or while the verification host slot is held,
because removing output under a live run is how MSB3027 and a half-built
assembly happen.

The current worktree is left alone unless -IncludeCurrent is given: you are
probably working in it.

.EXAMPLE
pwsh ./scripts/Clear-BuildOutput.ps1

.EXAMPLE
pwsh ./scripts/Clear-BuildOutput.ps1 -Execute
#>
[CmdletBinding()]
param(
    [switch] $Execute,

    [switch] $IncludeCurrent
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$current = (& git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
$slotPath = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.pegasus/verification-host-slot.json'

if ($Execute) {
    $live = @(Get-Process -Name 'testhost', 'vstest.console', 'MSBuild' -ErrorAction SilentlyContinue)
    if ($live.Count -gt 0) {
        $names = ($live | Group-Object ProcessName | ForEach-Object { "$($_.Count) $($_.Name)" }) -join ', '
        throw "Refusing to delete build output while $names process(es) are running. Stop them, or run dotnet build-server shutdown, and try again."
    }

    if (Test-Path -LiteralPath $slotPath) {
        $slot = Get-Content -Raw -LiteralPath $slotPath | ConvertFrom-Json
        throw "The verification host slot is held by '$($slot.holder)' in $($slot.worktree). A whole-solution run is in progress."
    }
}

function Get-DirectorySize {
    param([Parameter(Mandatory)][string] $Path)

    # Measure-Object emits nothing for an empty directory, and StrictMode will
    # not read a property off that.
    $measured = Get-ChildItem -LiteralPath $Path -Recurse -File -Force -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum
    if ($null -eq $measured -or $null -eq $measured.Sum) {
        return 0L
    }

    return [long] $measured.Sum
}

function Get-ProjectBuildOutput {
    param([Parameter(Mandatory)][string] $Worktree)

    # Only the bin and obj beside a project file git tracks in that worktree:
    # that is what MSBuild writes and nothing else is. Matching any directory
    # named bin or obj reached into ignored evidence and archive folders - an
    # earlier version of this script removed build output inside
    # artifacts/ui-baseline-review and an archived source pack that way - and it
    # counted nested matches twice.
    $projects = @(& git -C $Worktree ls-files -- '*.csproj' '*.fsproj' '*.vbproj' '*.proj' 2>$null)
    foreach ($project in $projects) {
        $directory = Split-Path (Join-Path $Worktree $project) -Parent
        foreach ($name in 'bin', 'obj') {
            $output = Join-Path $directory $name
            if (Test-Path -LiteralPath $output -PathType Container) {
                Get-Item -LiteralPath $output -Force
            }
        }
    }
}

$worktrees = @(& git -C $PSScriptRoot worktree list --porcelain |
    Select-String -Pattern '^worktree (?<path>.+)$' |
    ForEach-Object { $_.Matches[0].Groups['path'].Value })

$total = 0L
$removed = 0L

foreach ($worktree in $worktrees) {
    if (-not (Test-Path -LiteralPath $worktree)) {
        continue
    }

    $isCurrent = [IO.Path]::GetFullPath($worktree).TrimEnd('\', '/') -eq
        [IO.Path]::GetFullPath($current).TrimEnd('\', '/')

    $outputs = @(Get-ProjectBuildOutput -Worktree $worktree)

    if ($outputs.Count -eq 0) {
        continue
    }

    $bytes = 0L
    foreach ($output in $outputs) {
        $bytes += Get-DirectorySize -Path $output.FullName
    }
    $total += $bytes

    $label = '{0,8:N2} GB  {1}' -f ($bytes / 1GB), (Split-Path $worktree -Leaf)
    if ($isCurrent -and -not $IncludeCurrent) {
        Write-Host "$label  (current worktree, left alone)"
        continue
    }

    if (-not $Execute) {
        Write-Host "$label  ($($outputs.Count) directories)"
        continue
    }

    foreach ($output in $outputs) {
        Remove-Item -LiteralPath $output.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
    $removed += $bytes
    Write-Host "$label  removed"
}

Write-Host ''
Write-Host ('{0,8:N2} GB in build output across {1} worktree(s).' -f ($total / 1GB), $worktrees.Count)
if ($Execute) {
    Write-Host ('{0,8:N2} GB reclaimed.' -f ($removed / 1GB))
}
else {
    Write-Host '         Reporting only. Pass -Execute to remove it.'
}
