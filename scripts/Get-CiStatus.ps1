#!/usr/bin/env pwsh
<#
.SYNOPSIS
One line per merge request, and for every failed job of its head pipeline the
exact failing test names, read from the job log.

.DESCRIPTION
Reading a failure this way costs nothing and needs no local build, which is the
point: the alternative each round reached for was reproducing the failure on
this workstation. GitLab serves a job's log while the pipeline is still going,
so a failed shard can be read without waiting for its five siblings.

The head pipeline's own jobs are listed with the jobs of the child pipeline its
`lanes` trigger starts, where the test lanes run.

Replaces the per-round copies of this that kept being written into ignored
artifact directories and thrown away.

.EXAMPLE
pwsh ./scripts/Get-CiStatus.ps1

.EXAMPLE
pwsh ./scripts/Get-CiStatus.ps1 -MergeRequest 3, 5 -Wait
#>
[CmdletBinding()]
param(
    # Merge request IIDs (the N of !N). Defaults to the open merge request for
    # the current branch. Strings, because `pwsh -File ... -MergeRequest 3,5`
    # passes one string, and converting that to an int reads the comma as a
    # thousands separator: 35.
    [string[]] $MergeRequest = @(),

    # Poll until no watched merge request has a job still to finish.
    [switch] $Wait,

    [int] $PollSeconds = 30,

    [int] $TimeoutMinutes = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# glab fills :id from the current checkout's origin remote.
function Invoke-GitLabGet {
    param([Parameter(Mandatory)][string] $Path)
    $json = (& glab api "projects/:id/$Path" 2>$null) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "glab api projects/:id/$Path failed." }
    # Returned from a variable, a list arrives as its items.
    $parsed = $json | ConvertFrom-Json -NoEnumerate
    return $parsed
}

$MergeRequest = @($MergeRequest | ForEach-Object { $_ -split '[,\s]+' } | ForEach-Object { $_.TrimStart('!') } | Where-Object { $_ })
if (@($MergeRequest | Where-Object { $_ -notmatch '^\d+$' }).Count -gt 0) {
    throw "Merge request numbers must be digits: $($MergeRequest -join ', ')."
}

if ($MergeRequest.Count -eq 0) {
    $branch = (& git rev-parse --abbrev-ref HEAD).Trim()
    $open = @(Invoke-GitLabGet -Path "merge_requests?source_branch=$([uri]::EscapeDataString($branch))&state=opened")
    if ($open.Count -eq 0) {
        throw "No open merge request for $branch. Pass -MergeRequest explicitly."
    }
    $MergeRequest = @([string] $open[0].iid)
}

# A job in one of these has not finished; `manual` waits for a person, not CI.
$unfinished = @('created', 'waiting_for_resource', 'preparing', 'pending', 'running', 'scheduled')

function Get-Jobs {
    # The head pipeline and every job in it and in the child pipelines its
    # trigger jobs started. A trigger job is listed as well: without a child
    # pipeline (a configuration error) it is the only place the failure shows.
    param([Parameter(Mandatory)][int] $Number)

    $request = Invoke-GitLabGet -Path "merge_requests/$Number"
    $property = $request.PSObject.Properties['head_pipeline']
    $pipeline = if ($null -ne $property) { $property.Value } else { $null }
    if ($null -eq $pipeline) {
        return [pscustomobject]@{ Pipeline = $null; Jobs = @() }
    }

    $jobs = [System.Collections.Generic.List[object]]::new()
    $pipelineIds = [System.Collections.Generic.List[long]]::new()
    $pipelineIds.Add([long]$pipeline.id)
    foreach ($bridge in @(Invoke-GitLabGet -Path "pipelines/$($pipeline.id)/bridges")) {
        $jobs.Add([pscustomobject]@{ Id = [long]$bridge.id; Name = [string]$bridge.name; Status = [string]$bridge.status; Trigger = $true })
        $downstream = $bridge.PSObject.Properties['downstream_pipeline']
        if ($null -ne $downstream -and $null -ne $downstream.Value) {
            $pipelineIds.Add([long]$downstream.Value.id)
        }
    }
    foreach ($id in $pipelineIds) {
        foreach ($job in @(Invoke-GitLabGet -Path "pipelines/$id/jobs?per_page=100")) {
            $jobs.Add([pscustomobject]@{ Id = [long]$job.id; Name = [string]$job.name; Status = [string]$job.status; Trigger = $false })
        }
    }

    return [pscustomobject]@{ Pipeline = $pipeline; Jobs = $jobs.ToArray() }
}

if ($Wait) {
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    while ([DateTime]::UtcNow -lt $deadline) {
        $pending = 0
        foreach ($number in $MergeRequest) {
            $pending += @((Get-Jobs -Number $number).Jobs | Where-Object { $_.Status -in $unfinished }).Count
        }

        if ($pending -eq 0) {
            break
        }

        Write-Host "$pending unfinished job(s); next look in $PollSeconds s."
        Start-Sleep -Seconds $PollSeconds
    }
}

foreach ($number in $MergeRequest) {
    $read = Get-Jobs -Number $number
    if ($null -eq $read.Pipeline) {
        Write-Host ('{0,-6} {1}' -f "!$number", 'no pipeline')
        continue
    }

    $summary = (@($read.Jobs) | Group-Object Status | Sort-Object Name |
        ForEach-Object { "$($_.Count) $($_.Name)" }) -join ', '
    Write-Host ('{0,-6} pipeline {1} {2}: {3}' -f "!$number", $read.Pipeline.id, $read.Pipeline.status, ($summary ? $summary : 'no jobs'))

    foreach ($failure in @($read.Jobs | Where-Object { $_.Status -eq 'failed' })) {
        Write-Host "       $($failure.Name)"
        if ($failure.Trigger) {
            Write-Host '         (trigger job: read its child pipeline above)'
            continue
        }

        $log = & glab api "projects/:id/jobs/$($failure.Id)/trace" 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Host '         (log unavailable)'
            continue
        }

        # A GitLab log line starts with its timestamp and stream marker
        # (2026-11-15T14:20:01.123456Z 00O, a trailing + on a continued line),
        # carries ANSI colour codes, and folds its sections with
        # section_start:/section_end: markers (a carriage return after one
        # reads as a line break here).
        $interesting = @($log |
            ForEach-Object {
                "$_" -replace '^\d{4}-\d\d-\d\dT[0-9:.]+Z \d\d[OE]\+?\s?', '' `
                    -replace 'section_(?:start|end):\d+:[^\s]*\r?', '' `
                    -replace '\x1b\[[0-9;]*[A-Za-z]', '' `
                    -replace '\r', ''
            } |
            Select-String -Pattern '\[FAIL\]|error CS|Error Message:|Exception|Assert\.' |
            ForEach-Object { $_.Line -replace '\[xUnit[^\]]*\]\s+', '' } |
            Select-Object -Unique -First 10)

        if ($interesting.Count -eq 0) {
            Write-Host '         (no test-level failure in the log; read the job)'
        }
        foreach ($line in $interesting) {
            Write-Host "         $line"
        }
    }
}
