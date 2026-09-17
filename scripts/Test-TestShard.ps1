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
        [Parameter(Mandatory)][int] $ShardCount
    )

    for ($shard = 1; $shard -le $ShardCount; $shard++) {
        & $shardScript -Project ignored -Filter ignored -Shard $shard -ShardCount $ShardCount `
            -TestListPath $testList -ArtifactRoot $ArtifactRoot -ListOnly
        if ($LASTEXITCODE -ne 0) {
            throw "List-only assignment failed for shard $shard."
        }
    }
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
        Invoke-ListOnly -ArtifactRoot $first -ShardCount $shardCount
        # Discovery order must not affect class ownership.
        Set-Content -Path $testList -Value @($tests | Sort-Object -Descending)
        Invoke-ListOnly -ArtifactRoot $second -ShardCount $shardCount

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
            $expectedOwners = @{
                1 = @('Alpha', 'Lima', 'Mike')
                2 = @('Bravo', 'Kilo', 'November')
                3 = @('Charlie', 'Juliett', 'Oscar')
                4 = @('Delta', 'India')
                5 = @('Echo', 'Hotel')
                6 = @('Foxtrot', 'Golf')
            }
            foreach ($shard in 1..$shardCount) {
                $actualClasses = @(Get-Content (Join-Path $first "assigned-$shard.txt") |
                    ForEach-Object { ($_ -split '\.')[1] } |
                    Sort-Object -Unique)
                if (Compare-Object -ReferenceObject @($expectedOwners[$shard]) `
                        -DifferenceObject $actualClasses -CaseSensitive) {
                    throw "Shard $shard did not follow the six-runner snake across both direction changes."
                }
            }
        }

        & $shardScript -VerifyPartition -ShardCount $shardCount -ArtifactRoot $first
        if ($LASTEXITCODE -ne 0) {
            throw 'The balanced assignment failed exact partition verification.'
        }

        Set-Content -Path $testList -Value @('Example.Alpha.Test1', 'Example.Bravo.Test1')
        $sparse = Join-Path $root "sparse-$shardCount"
        Invoke-ListOnly -ArtifactRoot $sparse -ShardCount $shardCount
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

    Write-Output 'Test-shard assignment passed for 3 and 6 shards.'
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
