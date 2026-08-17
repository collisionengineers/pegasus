#!/usr/bin/env pwsh
<#
.SYNOPSIS
Runs one shard of a test project, or proves that a run's shards covered every
test exactly once.

.DESCRIPTION
VSTest cannot shard a run, so shards are assigned here from the project's own
enumerated test list. Whole test classes are assigned together because the
disposable-LocalDB collection pins a class's tests to one another.

Every step that could silently shrink a run fails instead: enumeration that
yields no test throws, a shard whose executed count differs from its assigned
count throws, and -VerifyPartition rejects a run whose shards do not reassemble
into exactly the enumerated set.

.EXAMPLE
pwsh ./scripts/Invoke-TestShard.ps1 -Project ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj -Filter 'Category!=Corpus&Category!=Browser' -Shard 1 -ShardCount 3

.EXAMPLE
pwsh ./scripts/Invoke-TestShard.ps1 -VerifyPartition -ArtifactRoot ./artifacts/test-shards -ShardCount 3
#>
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Run')]
    [string] $Project,

    [Parameter(Mandatory, ParameterSetName = 'Run')]
    [string] $Filter,

    [Parameter(Mandatory, ParameterSetName = 'Run')]
    [int] $Shard,

    [Parameter(Mandatory)]
    [int] $ShardCount,

    [Parameter(Mandatory, ParameterSetName = 'Verify')]
    [switch] $VerifyPartition,

    [string] $Configuration = 'Release',

    [string] $ArtifactRoot = 'artifacts/test-shards',

    [Parameter(ParameterSetName = 'Run')]
    [switch] $ListOnly,

    [Parameter(ParameterSetName = 'Run')]
    [string] $TestListPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-TestClass {
    param([Parameter(Mandatory)][string] $TestName)

    # Theory rows are listed as `Namespace.Class.Method(arg: value)`.
    $name = $TestName
    $argument = $name.IndexOf('(')
    if ($argument -ge 0) {
        $name = $name.Substring(0, $argument)
    }

    $method = $name.LastIndexOf('.')
    if ($method -lt 1) {
        throw "'$TestName' has no declaring class; the enumeration format changed."
    }

    return $name.Substring(0, $method)
}

if ($PSCmdlet.ParameterSetName -eq 'Verify') {
    $listed = $null
    for ($shard = 1; $shard -le $ShardCount; $shard++) {
        $listedPath = Join-Path $ArtifactRoot "listed-$shard.txt"
        if (-not (Test-Path $listedPath)) {
            throw "$listedPath is missing; shard $shard did not report what it enumerated."
        }

        $shardListed = @(Get-Content $listedPath)
        if ($shardListed.Count -eq 0) {
            throw "Shard $shard enumerated no test."
        }

        if ($null -eq $listed) {
            $listed = $shardListed
        }
        elseif (Compare-Object $listed $shardListed -CaseSensitive) {
            # Shards that disagree about the whole set cannot prove coverage of
            # it, however neatly their own assignments add up.
            throw "Shard $shard enumerated a different set of tests than shard 1."
        }
    }

    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($shard = 1; $shard -le $ShardCount; $shard++) {
        $assignedPath = Join-Path $ArtifactRoot "assigned-$shard.txt"
        if (-not (Test-Path $assignedPath)) {
            throw "$assignedPath is missing; shard $shard did not report what it ran."
        }

        foreach ($test in @(Get-Content $assignedPath)) {
            if (-not $seen.Add($test)) {
                throw "'$test' was assigned to more than one shard."
            }
        }
    }

    $missing = @($listed | Where-Object { -not $seen.Contains($_) })
    if ($missing.Count -gt 0) {
        throw "$($missing.Count) enumerated test(s) ran in no shard, starting with '$($missing[0])'."
    }

    if ($seen.Count -ne $listed.Count) {
        throw "The shards ran $($seen.Count) tests but $($listed.Count) were enumerated."
    }

    Write-Host "$ShardCount shards covered all $($listed.Count) enumerated tests exactly once."
    exit 0
}

if ($Shard -lt 1 -or $Shard -gt $ShardCount) {
    throw "Shard $Shard is outside 1..$ShardCount."
}

$output = @()
if ($TestListPath) {
    $tests = @(Get-Content $TestListPath | Where-Object { $_ } | Sort-Object -CaseSensitive)
}
else {
    $output = & dotnet test $Project --configuration $Configuration --no-build --filter $Filter --list-tests
    if ($LASTEXITCODE -ne 0) {
        $output | Write-Host
        throw "Enumerating '$Project' failed."
    }

    # `--list-tests` prints each test indented under a heading.
    $tests = @($output | Where-Object { $_ -match '^\s{4}\S' } | ForEach-Object { $_.Trim() } | Sort-Object -CaseSensitive)
}
if ($tests.Count -eq 0) {
    $output | Write-Host
    throw "Filter '$Filter' enumerated no test. Refusing to report an empty shard as green."
}

$classGroups = @($tests |
    Group-Object { Get-TestClass $_ } |
    ForEach-Object {
        [pscustomobject]@{
            Class = $_.Name
            Tests = @($_.Group)
        }
    } |
    # Largest test classes are placed first. The class-name tie-break and
    # lowest-shard tie-break make every runner derive the same partition.
    Sort-Object @{ Expression = { $_.Tests.Count }; Descending = $true },
                @{ Expression = { $_.Class }; Ascending = $true })

$assignments = @(for ($index = 1; $index -le $ShardCount; $index++) {
    [pscustomobject]@{
        Shard = $index
        TestCount = 0
        Classes = [System.Collections.Generic.List[string]]::new()
    }
})

foreach ($classGroup in $classGroups) {
    $lightest = $assignments | Sort-Object TestCount, Shard | Select-Object -First 1
    $lightest.Classes.Add($classGroup.Class)
    $lightest.TestCount += $classGroup.Tests.Count
}

$mine = @($assignments[$Shard - 1].Classes)

$assigned = @($tests | Where-Object { $mine -contains (Get-TestClass $_) })

New-Item -ItemType Directory -Force -Path $ArtifactRoot | Out-Null
Set-Content -Path (Join-Path $ArtifactRoot "listed-$Shard.txt") -Value $tests
Set-Content -Path (Join-Path $ArtifactRoot "assigned-$Shard.txt") -Value $assigned

Write-Host "Shard $Shard of $ShardCount takes $($mine.Count) of $($classGroups.Count) classes and $($assigned.Count) of $($tests.Count) tests."

if ($ListOnly) {
    $assigned | Write-Host
    exit 0
}

if ($assigned.Count -eq 0) {
    # Only reachable when there are fewer classes than shards, which is a
    # covered partition rather than a lost test.
    Write-Host "Shard $Shard has no class assigned to it."
    exit 0
}

$clause = ($mine | ForEach-Object { "FullyQualifiedName~$_." }) -join '|'
$resultsFile = "shard-$Shard.trx"
& dotnet test $Project --configuration $Configuration --no-build `
    --filter "($Filter)&($clause)" `
    --logger "trx;LogFileName=$resultsFile" `
    --results-directory $ArtifactRoot
$testExitCode = $LASTEXITCODE

$trxPath = Join-Path $ArtifactRoot $resultsFile
if (-not (Test-Path $trxPath)) {
    throw "$trxPath was not written, so this shard's coverage cannot be checked."
}

[xml] $trx = Get-Content $trxPath
$executed = [int] $trx.TestRun.ResultSummary.Counters.total
if ($executed -ne $assigned.Count) {
    throw "Shard $Shard was assigned $($assigned.Count) tests but ran $executed."
}

Write-Host "Shard $Shard ran all $executed assigned tests."
exit $testExitCode
