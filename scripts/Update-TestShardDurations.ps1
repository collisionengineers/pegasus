#!/usr/bin/env pwsh
<#
.SYNOPSIS
Records how long each integration test class takes, from a run's own .trx
evidence, so Invoke-TestShard.ps1 can balance shards on cost.

.DESCRIPTION
The SQL integration lane already retains one .trx per shard. This reads those
files and writes the per-class totals Invoke-TestShard.ps1 reads, so the table
is measured rather than estimated and no extra run is needed to produce it.

Classes are keyed by the TestMethod className the runner recorded, not by
parsing display names, so theory rows and parameterised names collapse onto
their declaring class exactly as the sharder groups them.

Refuses to write a table from evidence that does not cover the whole lane: a
partial set of shards would under-record the classes the missing shards ran and
push them onto one runner.

.EXAMPLE
gh run download <run-id> --dir artifacts/test-shards
pwsh ./scripts/Update-TestShardDurations.ps1 -ShardCount 6

.EXAMPLE
pwsh ./scripts/Update-TestShardDurations.ps1 -ArtifactRoot artifacts/test-shards -PassThru
#>
[CmdletBinding()]
param(
    [string] $ArtifactRoot = 'artifacts/test-shards',

    [string] $Path,

    # When given, every shard from 1..ShardCount must have contributed a .trx.
    [int] $ShardCount = 0,

    [switch] $PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Path) {
    $Path = Join-Path $PSScriptRoot 'test-shard-durations.json'
}

if (-not (Test-Path -LiteralPath $ArtifactRoot)) {
    throw "'$ArtifactRoot' does not exist; download a run's test-shard artifacts into it first."
}

$trxFiles = @(Get-ChildItem -LiteralPath $ArtifactRoot -Filter '*.trx' -Recurse -File | Sort-Object FullName)
if ($trxFiles.Count -eq 0) {
    throw "No .trx file under '$ArtifactRoot'. Refusing to write an empty duration table."
}

if ($ShardCount -gt 0) {
    $present = @($trxFiles | ForEach-Object {
        if ($_.Name -match 'shard-(?<shard>\d+)\.trx$') { [int] $Matches['shard'] }
    })
    $absent = @(1..$ShardCount | Where-Object { $present -notcontains $_ })
    if ($absent.Count -gt 0) {
        throw "Shard(s) $($absent -join ', ') contributed no .trx. A partial run would under-record the classes they ran."
    }
}

$totals = @{}
$counts = @{}
$results = 0
$perShard = [ordered]@{}

foreach ($file in $trxFiles) {
    [xml] $trx = Get-Content -Raw -LiteralPath $file.FullName
    $shardSeconds = 0.0

    # testId -> declaring class, from the definitions the runner wrote.
    $classOf = @{}
    foreach ($definition in $trx.TestRun.TestDefinitions.UnitTest) {
        $className = $definition.TestMethod.className
        if ($className) {
            $classOf[$definition.id] = $className
        }
    }

    foreach ($result in $trx.TestRun.Results.UnitTestResult) {
        $class = $classOf[$result.testId]
        if (-not $class) {
            continue
        }

        $seconds = 0.0
        if ($result.duration) {
            # The .trx writes the invariant form; a machine whose culture uses a
            # decimal comma must not misread it.
            $seconds = ([TimeSpan]::Parse($result.duration, [Globalization.CultureInfo]::InvariantCulture)).TotalSeconds
        }

        if (-not $totals.ContainsKey($class)) {
            $totals[$class] = 0.0
            $counts[$class] = 0
        }
        $totals[$class] += $seconds
        $counts[$class]++
        $results++
        $shardSeconds += $seconds
    }

    $perShard[$file.Name] = $shardSeconds
}

if ($totals.Count -eq 0) {
    throw "No test result carried a declaring class. Refusing to write an empty duration table."
}

$table = [ordered]@{}
foreach ($class in @($totals.Keys | Sort-Object -CaseSensitive)) {
    $table[$class] = [math]::Round($totals[$class], 2)
}

# Sorted keys and a trailing newline so a refresh reads as a diff of the classes
# whose cost actually moved.
$json = ($table | ConvertTo-Json -Depth 2) + [Environment]::NewLine
Set-Content -LiteralPath $Path -Value $json -NoNewline -Encoding utf8NoBOM

$slowest = @($table.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 5)
Write-Host "Recorded $($table.Count) classes from $($trxFiles.Count) .trx file(s), $results results, $([math]::Round(($totals.Values | Measure-Object -Sum).Sum / 60, 1)) minutes total."
Write-Host "Slowest: $(($slowest | ForEach-Object { "$($_.Key) $([math]::Round($_.Value, 1))s" }) -join '; ')."
Write-Host "Wrote $Path."

# How balanced the run that produced this evidence actually was. A ratio well
# above 1 means the committed table has gone stale and this refresh is due.
if ($perShard.Count -gt 1) {
    $values = @($perShard.Values)
    $longest = ($values | Measure-Object -Maximum).Maximum
    $shortest = ($values | Measure-Object -Minimum).Minimum
    $spread = ($perShard.GetEnumerator() | ForEach-Object { '{0} {1:N1}m' -f ($_.Key -replace '\.trx$', ''), ($_.Value / 60) }) -join ', '
    Write-Host "Test time per shard: $spread."
    Write-Host ('Longest over shortest: {0:N2}.' -f ($longest / [math]::Max($shortest, 1)))
}

if ($PassThru) {
    [pscustomobject] $table
}
