#!/usr/bin/env pwsh
<#
.SYNOPSIS
One line per pull request, and for every failing check the exact failing test
names, read from the job log.

.DESCRIPTION
Reading a failure this way costs nothing and needs no local build, which is the
point: the alternative each round reached for was reproducing the failure on
this workstation. GitHub serves a job's log while the run is still going, so a
failed shard can be read without waiting for its five siblings.

Replaces the per-round copies of this that kept being written into ignored
artifact directories and thrown away.

.EXAMPLE
pwsh ./scripts/Get-CiStatus.ps1

.EXAMPLE
pwsh ./scripts/Get-CiStatus.ps1 -PullRequest 793, 795 -Wait
#>
[CmdletBinding()]
param(
    # Defaults to the pull request for the current branch.
    [int[]] $PullRequest = @(),

    # Poll until no watched request has a pending check.
    [switch] $Wait,

    [int] $PollSeconds = 30,

    [int] $TimeoutMinutes = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repository = (& gh repo view --json nameWithOwner --jq '.nameWithOwner').Trim()

if ($PullRequest.Count -eq 0) {
    $current = & gh pr view --json number --jq '.number' 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $current) {
        throw 'No pull request for the current branch. Pass -PullRequest explicitly.'
    }
    $PullRequest = @([int] $current)
}

function Get-Checks {
    param([Parameter(Mandatory)][int] $Number)

    # `gh pr checks` exits non-zero when any check failed, which is information
    # rather than an error here.
    $lines = & gh pr checks $Number 2>$null
    return @($lines | Where-Object { $_ } | ForEach-Object {
        $fields = $_ -replace "`r", '' -split "`t"
        if ($fields.Count -ge 2) {
            [pscustomobject]@{
                Name = $fields[0]
                State = $fields[1]
                Url = if ($fields.Count -ge 4) { $fields[3] } else { '' }
            }
        }
    })
}

if ($Wait) {
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    while ([DateTime]::UtcNow -lt $deadline) {
        $pending = 0
        foreach ($number in $PullRequest) {
            $pending += @(Get-Checks -Number $number | Where-Object { $_.State -eq 'pending' }).Count
        }

        if ($pending -eq 0) {
            break
        }

        Write-Host "$pending pending check(s); next look in $PollSeconds s."
        Start-Sleep -Seconds $PollSeconds
    }
}

foreach ($number in $PullRequest) {
    $checks = Get-Checks -Number $number
    $summary = ($checks | Group-Object State | Sort-Object Name |
        ForEach-Object { "$($_.Count) $($_.Name)" }) -join ', '
    Write-Host ('{0,-6} {1}' -f $number, ($summary ? $summary : 'no checks'))

    foreach ($failure in @($checks | Where-Object { $_.State -eq 'fail' })) {
        Write-Host "       $($failure.Name)"
        if (-not $failure.Url) {
            continue
        }

        $jobId = $failure.Url -replace '.*/job/', ''
        $log = & gh api "repos/$repository/actions/jobs/$jobId/logs" 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Host '         (log unavailable)'
            continue
        }

        $interesting = @($log |
            Select-String -Pattern '\[FAIL\]|error CS|Error Message:|Exception|Assert\.' |
            ForEach-Object { $_.Line -replace '^[0-9T:.\-]+Z\s+', '' -replace '\[xUnit[^\]]*\]\s+', '' } |
            Select-Object -Unique -First 10)

        if ($interesting.Count -eq 0) {
            Write-Host '         (no test-level failure in the log; read the job)'
        }
        foreach ($line in $interesting) {
            Write-Host "         $line"
        }
    }
}
