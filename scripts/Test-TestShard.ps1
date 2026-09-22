[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$shardScript = Join-Path $PSScriptRoot 'Invoke-TestShard.ps1'
$root = Join-Path ([System.IO.Path]::GetTempPath()) ("pegasus-shard-test-$([guid]::NewGuid().ToString('N'))")
$testList = Join-Path $root 'tests.txt'

function Invoke-ListOnly {
    param(
        [Parameter(Mandatory)][string] $ArtifactRoot,
        [Parameter(Mandatory)][int] $ShardCount,
        # Always explicit, so no case silently picks up the committed table and
        # starts depending on whatever the last refresh measured.
        [Parameter(Mandatory)][string] $DurationPath
    )

    for ($shard = 1; $shard -le $ShardCount; $shard++) {
        & $shardScript -Project ignored -Filter ignored -Shard $shard -ShardCount $ShardCount `
            -TestListPath $testList -ArtifactRoot $ArtifactRoot -DurationPath $DurationPath -ListOnly
        if ($LASTEXITCODE -ne 0) {
            throw "List-only assignment failed for shard $shard."
        }
    }
}

function Get-ShardClasses {
    param(
        [Parameter(Mandatory)][string] $ArtifactRoot,
        [Parameter(Mandatory)][int] $Shard
    )

    return @(Get-Content (Join-Path $ArtifactRoot "assigned-$Shard.txt") |
        ForEach-Object { ($_ -split '\.')[1] } |
        Sort-Object -Unique)
}

function Copy-ShardArtifacts {
    param(
        [Parameter(Mandatory)][string] $Source,
        [Parameter(Mandatory)][string] $Destination
    )

    New-Item -ItemType Directory -Path $Destination | Out-Null
    Get-ChildItem -LiteralPath $Source -File | Copy-Item -Destination $Destination
}

function Assert-PartitionFails {
    param(
        [Parameter(Mandatory)][string] $ArtifactRoot,
        [Parameter(Mandatory)][int] $ShardCount,
        [Parameter(Mandatory)][string] $Scenario
    )

    $failed = $false
    try {
        & $shardScript -VerifyPartition -ShardCount $ShardCount -ArtifactRoot $ArtifactRoot
    }
    catch {
        $failed = $true
    }

    if (-not $failed) {
        throw "Partition verification accepted $Scenario."
    }
}

function Assert-WorkflowShardCountsAgree {
    $workflowPath = Join-Path $PSScriptRoot '../.github/workflows/ci.yml'
    $workflow = Get-Content -Raw -LiteralPath $workflowPath
    $matrixMatches = [regex]::Matches(
        $workflow,
        '(?m)^\s+shard:\s+\[(?<shards>[^\]]+)\]\s*$')
    $executionMatches = [regex]::Matches(
        $workflow,
        '(?m)^\s+-Shard \$\{\{ matrix\.shard \}\} -ShardCount (?<count>\d+)\s*$')
    $partitionMatches = [regex]::Matches(
        $workflow,
        '(?m)^\s+run: .*Invoke-TestShard\.ps1 -VerifyPartition -ShardCount (?<count>\d+)\s*$')

    if ($matrixMatches.Count -ne 1 -or
        $executionMatches.Count -ne 1 -or
        $partitionMatches.Count -ne 1) {
        throw 'The workflow must have one SQL shard matrix, execution count and partition count.'
    }

    $matrixCount = @($matrixMatches[0].Groups['shards'].Value.Split(',') |
        Where-Object { $_.Trim() }).Count
    $executionCount = [int]$executionMatches[0].Groups['count'].Value
    $partitionCount = [int]$partitionMatches[0].Groups['count'].Value
    if ($matrixCount -ne $executionCount -or $matrixCount -ne $partitionCount) {
        throw "Workflow shard counts disagree: matrix $matrixCount, execution $executionCount, partition $partitionCount."
    }
}

try {
    New-Item -ItemType Directory -Path $root | Out-Null
    # Never created: the count-balanced cases below must not read a table.
    $noDurations = Join-Path $root 'absent-durations.json'
    Assert-WorkflowShardCountsAgree
    foreach ($shardCount in 3, 6) {
        $classSizes = if ($shardCount -eq 6) {
            # Three rows cross both direction changes. Tied sizes also prove
            # the class-name tie-break is stable when discovery is reversed.
            [ordered]@{
                Alpha = 8; Bravo = 8; Charlie = 7; Delta = 7; Echo = 6
                Foxtrot = 6; Golf = 5; Hotel = 5; India = 4; Juliett = 4
                Kilo = 3; Lima = 3; Mike = 2; November = 2; Oscar = 1
            }
        }
        else {
            [ordered]@{
                Alpha = 6; Bravo = 5; Charlie = 4
                Delta = 3; Echo = 2; Foxtrot = 1
            }
        }
        $classes = @($classSizes.Keys)
        $tests = @(foreach ($class in $classes) {
            1..$classSizes[$class] | ForEach-Object {
                "Example.$class.Test(value: $_)"
            }
        })
        Set-Content -Path $testList -Value $tests

        $first = Join-Path $root "first-$shardCount"
        $second = Join-Path $root "second-$shardCount"
        Invoke-ListOnly -ArtifactRoot $first -ShardCount $shardCount -DurationPath $noDurations
        # Discovery order must not affect class ownership.
        Set-Content -Path $testList -Value @($tests | Sort-Object -Descending)
        Invoke-ListOnly -ArtifactRoot $second -ShardCount $shardCount -DurationPath $noDurations

        $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        for ($shard = 1; $shard -le $shardCount; $shard++) {
            $assigned = @(Get-Content (Join-Path $first "assigned-$shard.txt"))
            if ($shardCount -eq 3 -and $assigned.Count -ne ($classes.Count + 1)) {
                throw "Shard $shard expected $($classes.Count + 1) tests but received $($assigned.Count)."
            }

            $repeat = @(Get-Content (Join-Path $second "assigned-$shard.txt"))
            if (Compare-Object $assigned $repeat -CaseSensitive) {
                throw "Shard $shard assignment was not deterministic."
            }

            foreach ($test in $assigned) {
                if (-not $seen.Add($test)) {
                    throw "'$test' was assigned more than once."
                }
            }
        }

        if ($seen.Count -ne $tests.Count) {
            throw "Assignments covered $($seen.Count) of $($tests.Count) tests."
        }

        foreach ($class in $classes) {
            $owners = @(1..$shardCount | Where-Object {
                Get-Content (Join-Path $first "assigned-$_.txt") | Where-Object { $_ -like "Example.$class.*" }
            })
            if ($owners.Count -ne 1) {
                throw "Example.$class was split across shards."
            }
        }

        if ($shardCount -eq 6) {
            # With no table every class costs its test count, so the
            # lightest-runner rule lands on these owners. Totals are
            # 13, 13, 12, 11, 11, 11 of 71.
            $expectedOwners = @{
                1 = @('Alpha', 'Kilo', 'Mike')
                2 = @('Bravo', 'Lima', 'November')
                3 = @('Charlie', 'India', 'Oscar')
                4 = @('Delta', 'Juliett')
                5 = @('Echo', 'Golf')
                6 = @('Foxtrot', 'Hotel')
            }
            foreach ($shard in 1..$shardCount) {
                $actualClasses = Get-ShardClasses -ArtifactRoot $first -Shard $shard
                if (Compare-Object -ReferenceObject @($expectedOwners[$shard]) `
                        -DifferenceObject $actualClasses -CaseSensitive) {
                    throw "Shard $shard owned $($actualClasses -join ', ') rather than $($expectedOwners[$shard] -join ', ')."
                }
            }

            $shardTotals = @(1..$shardCount | ForEach-Object {
                $owned = Get-ShardClasses -ArtifactRoot $first -Shard $_
                ($owned | ForEach-Object { $classSizes[$_] } | Measure-Object -Sum).Sum
            })
            $spread = ($shardTotals | Measure-Object -Maximum).Maximum - ($shardTotals | Measure-Object -Minimum).Minimum
            if ($spread -gt 2) {
                throw "Count-balanced shards spread $spread tests apart: $($shardTotals -join ', ')."
            }
        }

        & $shardScript -VerifyPartition -ShardCount $shardCount -ArtifactRoot $first
        if ($LASTEXITCODE -ne 0) {
            throw 'The balanced assignment failed exact partition verification.'
        }

        Set-Content -Path $testList -Value @('Example.Alpha.Test1', 'Example.Bravo.Test1')
        $sparse = Join-Path $root "sparse-$shardCount"
        Invoke-ListOnly -ArtifactRoot $sparse -ShardCount $shardCount -DurationPath $noDurations
        foreach ($emptyShard in 3..$shardCount) {
            if (@(Get-Content (Join-Path $sparse "assigned-$emptyShard.txt")).Count -ne 0) {
                throw 'A shard with no class should write an empty assignment.'
            }
        }
        & $shardScript -VerifyPartition -ShardCount $shardCount -ArtifactRoot $sparse
        if ($LASTEXITCODE -ne 0) {
            throw 'Empty shards must not lose or duplicate assigned tests.'
        }
    }

    # Cost, not count. Two classes that each restore a database per test cost far
    # more than two parsers with fifty cheap tests, and a fixed-row deal puts both
    # expensive classes in the same slot as soon as the row boundary falls between
    # them. That is how one shard came to run 2.35 times another.
    $skewed = @(
        @(1..1 | ForEach-Object { 'Example.Exp1.Test(value: 1)' })
        @(1..1 | ForEach-Object { 'Example.Exp2.Test(value: 1)' })
        @(1..50 | ForEach-Object { "Example.Cheap1.Test(value: $_)" })
        @(1..50 | ForEach-Object { "Example.Cheap2.Test(value: $_)" })
    )
    Set-Content -Path $testList -Value $skewed

    $skewTable = Join-Path $root 'skewed-durations.json'
    $skewCosts = @{ 'Example.Exp1' = 100.0; 'Example.Exp2' = 100.0; 'Example.Cheap1' = 1.0; 'Example.Cheap2' = 1.0 }
    Set-Content -Path $skewTable -Encoding utf8NoBOM -Value ($skewCosts | ConvertTo-Json)

    function Measure-CostSpread {
        param(
            [Parameter(Mandatory)][string] $ArtifactRoot,
            [Parameter(Mandatory)][int] $ShardCount,
            [Parameter(Mandatory)][hashtable] $Cost
        )

        $totals = @(1..$ShardCount | ForEach-Object {
            $owned = Get-ShardClasses -ArtifactRoot $ArtifactRoot -Shard $_
            ($owned | ForEach-Object { $Cost["Example.$_"] } | Measure-Object -Sum).Sum
        })
        return [pscustomobject]@{
            Totals = $totals
            Spread = ($totals | Measure-Object -Maximum).Maximum - ($totals | Measure-Object -Minimum).Minimum
        }
    }

    $byCount = Join-Path $root 'skew-by-count'
    $byCost = Join-Path $root 'skew-by-cost'
    Invoke-ListOnly -ArtifactRoot $byCount -ShardCount 3 -DurationPath $noDurations
    Invoke-ListOnly -ArtifactRoot $byCost -ShardCount 3 -DurationPath $skewTable

    $countSpread = Measure-CostSpread -ArtifactRoot $byCount -ShardCount 3 -Cost $skewCosts
    $costSpread = Measure-CostSpread -ArtifactRoot $byCost -ShardCount 3 -Cost $skewCosts
    if ($costSpread.Spread -ge $countSpread.Spread) {
        throw "Balancing on recorded duration did not beat balancing on test count: cost spread $($costSpread.Spread) ($($costSpread.Totals -join ', ')) against count spread $($countSpread.Spread) ($($countSpread.Totals -join ', '))."
    }

    $expensiveOwners = @(1..3 | Where-Object {
        $owned = Get-ShardClasses -ArtifactRoot $byCost -Shard $_
        @($owned | Where-Object { $_ -like 'Exp*' }).Count -gt 0
    })
    if ($expensiveOwners.Count -ne 2) {
        throw "The two expensive classes landed on $($expensiveOwners.Count) shard(s); they must not share one."
    }

    & $shardScript -VerifyPartition -ShardCount 3 -ArtifactRoot $byCost
    if ($LASTEXITCODE -ne 0) {
        throw 'The cost-balanced assignment failed exact partition verification.'
    }

    # Discovery order must not matter with a table either.
    $byCostRepeat = Join-Path $root 'skew-by-cost-repeat'
    Set-Content -Path $testList -Value @($skewed | Sort-Object -Descending)
    Invoke-ListOnly -ArtifactRoot $byCostRepeat -ShardCount 3 -DurationPath $skewTable
    foreach ($shard in 1..3) {
        if (Compare-Object @(Get-Content (Join-Path $byCost "assigned-$shard.txt")) `
                @(Get-Content (Join-Path $byCostRepeat "assigned-$shard.txt")) -CaseSensitive) {
            throw "Shard $shard was not deterministic when balanced on recorded duration."
        }
    }

    # A class the table does not name costs the median, not nothing. Were it free,
    # both unnamed classes would pile onto the lightest shard and the split would
    # be three classes against one.
    Set-Content -Path $testList -Value @(
        'Example.Known1.Test(value: 1)'
        'Example.Known2.Test(value: 1)'
        'Example.Unnamed1.Test(value: 1)'
        'Example.Unnamed2.Test(value: 1)'
    )
    $partialTable = Join-Path $root 'partial-durations.json'
    Set-Content -Path $partialTable -Encoding utf8NoBOM -Value (@{ 'Example.Known1' = 100.0; 'Example.Known2' = 100.0 } | ConvertTo-Json)

    $partial = Join-Path $root 'partial-table'
    Invoke-ListOnly -ArtifactRoot $partial -ShardCount 2 -DurationPath $partialTable
    foreach ($shard in 1..2) {
        $owned = Get-ShardClasses -ArtifactRoot $partial -Shard $shard
        if ($owned.Count -ne 2) {
            throw "Shard $shard owned $($owned.Count) classes ($($owned -join ', ')); an unmeasured class must cost the median, not nothing."
        }
    }

    $validSix = Join-Path $root 'first-6'

    $inconsistent = Join-Path $root 'negative-inconsistent-inventories'
    Copy-ShardArtifacts -Source $validSix -Destination $inconsistent
    Add-Content -Path (Join-Path $inconsistent 'listed-2.txt') -Value 'Example.Zulu.Test(value: 1)'
    Assert-PartitionFails -ArtifactRoot $inconsistent -ShardCount 6 `
        -Scenario 'inconsistent inventories between shards'

    $duplicate = Join-Path $root 'negative-duplicate-assignment'
    Copy-ShardArtifacts -Source $validSix -Destination $duplicate
    $duplicateTest = Get-Content (Join-Path $duplicate 'assigned-2.txt') | Select-Object -First 1
    Add-Content -Path (Join-Path $duplicate 'assigned-1.txt') -Value $duplicateTest
    Assert-PartitionFails -ArtifactRoot $duplicate -ShardCount 6 `
        -Scenario 'a duplicate assignment'

    $missing = Join-Path $root 'negative-missing-assignment'
    Copy-ShardArtifacts -Source $validSix -Destination $missing
    $missingPath = Join-Path $missing 'assigned-1.txt'
    $missingAssignments = @(Get-Content $missingPath)
    Set-Content -Path $missingPath -Value @($missingAssignments | Select-Object -Skip 1)
    Assert-PartitionFails -ArtifactRoot $missing -ShardCount 6 `
        -Scenario 'a missing assignment'

    $notPartition = Join-Path $root 'negative-not-a-partition'
    Copy-ShardArtifacts -Source $validSix -Destination $notPartition
    $notPartitionPath = Join-Path $notPartition 'assigned-1.txt'
    $notPartitionAssignments = @(Get-Content $notPartitionPath)
    $notPartitionAssignments[0] = 'Example.Zulu.Test(value: 1)'
    Set-Content -Path $notPartitionPath -Value $notPartitionAssignments
    Assert-PartitionFails -ArtifactRoot $notPartition -ShardCount 6 `
        -Scenario 'an assignment set that is not a partition of the inventory'

    Write-Output 'Test-shard assignment passed for 3 and 6 shards, by test count and by recorded duration.'
}
finally {
    if (Test-Path -LiteralPath $root) {
        $resolvedRoot = [System.IO.Path]::GetFullPath($root)
        $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
        if ((Split-Path $resolvedRoot -Parent) -ne $temporaryRoot -or
            (Split-Path $resolvedRoot -Leaf) -notmatch '^pegasus-shard-test-[0-9a-f]{32}$') {
            throw "Refusing to remove unexpected shard-test directory '$resolvedRoot'."
        }
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
