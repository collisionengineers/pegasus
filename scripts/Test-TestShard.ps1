[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$shardScript = Join-Path $PSScriptRoot 'Invoke-TestShard.ps1'
$root = Join-Path ([System.IO.Path]::GetTempPath()) ("pegasus-shard-test-$([guid]::NewGuid().ToString('N'))")
$testList = Join-Path $root 'tests.txt'

function Invoke-ListOnly {
    param([Parameter(Mandatory)][string] $ArtifactRoot)

    for ($shard = 1; $shard -le 3; $shard++) {
        & $shardScript -Project ignored -Filter ignored -Shard $shard -ShardCount 3 `
            -TestListPath $testList -ArtifactRoot $ArtifactRoot -ListOnly
        if ($LASTEXITCODE -ne 0) {
            throw "List-only assignment failed for shard $shard."
        }
    }
}

try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $tests = @(
        1..6 | ForEach-Object { "Example.Alpha.Test$_" }
        1..5 | ForEach-Object { "Example.Bravo.Test$_" }
        1..4 | ForEach-Object { "Example.Charlie.Test$_" }
        1..3 | ForEach-Object { "Example.Delta.Test$_" }
        1..2 | ForEach-Object { "Example.Echo.Test$_" }
        'Example.Foxtrot.Test1'
    )
    Set-Content -Path $testList -Value $tests

    $first = Join-Path $root 'first'
    $second = Join-Path $root 'second'
    Invoke-ListOnly -ArtifactRoot $first
    Invoke-ListOnly -ArtifactRoot $second

    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($shard = 1; $shard -le 3; $shard++) {
        $assigned = @(Get-Content (Join-Path $first "assigned-$shard.txt"))
        if ($assigned.Count -ne 7) {
            throw "Shard $shard expected 7 tests but received $($assigned.Count)."
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

    foreach ($class in 'Alpha', 'Bravo', 'Charlie', 'Delta', 'Echo', 'Foxtrot') {
        $owners = @(1..3 | Where-Object {
            Get-Content (Join-Path $first "assigned-$_.txt") | Where-Object { $_ -like "Example.$class.*" }
        })
        if ($owners.Count -ne 1) {
            throw "Example.$class was split across shards."
        }
    }

    & $shardScript -VerifyPartition -ShardCount 3 -ArtifactRoot $first
    if ($LASTEXITCODE -ne 0) {
        throw 'The balanced assignment failed exact partition verification.'
    }

    Set-Content -Path $testList -Value @('Example.Alpha.Test1', 'Example.Bravo.Test1')
    $sparse = Join-Path $root 'sparse'
    Invoke-ListOnly -ArtifactRoot $sparse
    if (@(Get-Content (Join-Path $sparse 'assigned-3.txt')).Count -ne 0) {
        throw 'A shard with no class should write an empty assignment.'
    }

    Write-Output 'Test-shard assignment passed.'
}
finally {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
