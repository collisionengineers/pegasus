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

try {
    New-Item -ItemType Directory -Path $root | Out-Null
    foreach ($shardCount in 3, 6) {
        # Two rows exercise both directions of the snake, including theory rows.
        $classes = @('Alpha', 'Bravo', 'Charlie', 'Delta', 'Echo', 'Foxtrot',
            'Golf', 'Hotel', 'India', 'Juliett', 'Kilo', 'Lima') | Select-Object -First ($shardCount * 2)
        $tests = @(for ($index = 0; $index -lt $classes.Count; $index++) {
            1..($classes.Count - $index) | ForEach-Object {
                "Example.$($classes[$index]).Test(value: $_)"
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
        for ($shard = 1; $shard -le $ShardCount; $shard++) {
            $assigned = @(Get-Content (Join-Path $first "assigned-$shard.txt"))
            if ($assigned.Count -ne ($classes.Count + 1)) {
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
