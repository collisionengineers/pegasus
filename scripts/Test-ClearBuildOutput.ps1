[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Clear-BuildOutput.ps1 deletes. Its one safety property is that it deletes
# build output and nothing that merely shares the name: an earlier version
# matched any directory called bin or obj and removed build output inside
# ignored evidence and archive folders. This builds a throwaway repository with
# a tracked project beside decoys and runs the real script against it.

$root = Join-Path ([IO.Path]::GetTempPath()) ("pegasus-clear-test-$([guid]::NewGuid().ToString('N'))")

function New-File([string] $Path, [string] $Content = 'x') {
    New-Item -ItemType Directory -Force -Path (Split-Path $Path -Parent) | Out-Null
    Set-Content -LiteralPath $Path -Value $Content -NoNewline
}

try {
    New-Item -ItemType Directory -Path $root | Out-Null
    & git -C $root init -q
    & git -C $root config user.email 'test@example.invalid'
    & git -C $root config user.name 'test'

    New-File (Join-Path $root 'src/App/App.csproj') '<Project />'
    New-File (Join-Path $root 'src/App/Program.cs') 'class P {}'
    New-File (Join-Path $root '.gitignore') "bin/`nobj/`nartifacts/`ncorpus/`n"
    New-Item -ItemType Directory -Path (Join-Path $root 'scripts') | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'Clear-BuildOutput.ps1') (Join-Path $root 'scripts')
    & git -C $root add -A
    & git -C $root commit -q -m fixture

    # Build output of the tracked project: this, and only this, may go.
    New-File (Join-Path $root 'src/App/bin/Release/App.dll')
    New-File (Join-Path $root 'src/App/obj/project.assets.json')

    # Decoys that share the name. None of them sits beside a tracked project.
    $decoys = @(
        'artifacts/ui-baseline-review/visual-host/bin/Pegasus.Web.dll'
        'artifacts/archive/source/src/Old/obj/keep.json'
        'corpus/obj/evidence.eml'
        'src/App/bin/Release/nested/bin/inner.dll'
    )
    foreach ($decoy in $decoys) {
        New-File (Join-Path $root $decoy)
    }
    # An untracked project's output is left alone too: the script only speaks
    # for projects the repository defines.
    New-File (Join-Path $root 'scratch/Tool/Tool.csproj') '<Project />'
    New-File (Join-Path $root 'scratch/Tool/bin/tool.dll')

    $report = & pwsh -NoProfile -File (Join-Path $root 'scripts/Clear-BuildOutput.ps1') -IncludeCurrent -Execute 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Clear-BuildOutput failed: $($report -join [Environment]::NewLine)"
    }

    foreach ($gone in 'src/App/bin', 'src/App/obj') {
        if (Test-Path -LiteralPath (Join-Path $root $gone)) {
            throw "$gone is the tracked project's build output and should have been removed."
        }
    }

    foreach ($kept in @($decoys | Where-Object { $_ -notlike 'src/App/bin/*' }) + 'scratch/Tool/bin/tool.dll') {
        if (-not (Test-Path -LiteralPath (Join-Path $root $kept))) {
            throw "$kept is not build output of a tracked project and was deleted."
        }
    }

    if (@(& git -C $root status --porcelain | Where-Object { $_ -match '^ ?D' }).Count -gt 0) {
        throw 'A tracked file was deleted.'
    }

    Write-Output 'Build-output pruning removes only tracked projects'' bin and obj.'
}
finally {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
