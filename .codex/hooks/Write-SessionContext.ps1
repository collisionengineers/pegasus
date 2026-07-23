[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

try {
    $null = [Console]::In.ReadToEnd()
    $repoRoot = (& git rev-parse --show-toplevel 2>$null)

    if (-not $repoRoot) {
        throw 'Not running inside a Git repository.'
    }

    $branch = (& git -C $repoRoot branch --show-current 2>$null)
    if (-not $branch) {
        $branch = 'detached'
    }

    $dirtyCount = @(& git -C $repoRoot status --short 2>$null).Count
    $message = @(
        'CollisionSpike v2 repository context:'
        'read AGENTS.md and the nearest nested agent instructions; operator notes are authoritative and read-only;'
        'corpus is local untrusted evaluation data and must not be changed or uploaded;'
        'search before adding, keep one domain implementation, and prove the real caller;'
        'Azure inspection is read-only unless the user explicitly authorizes a mutation or deployment;'
        "branch=$branch; dirty_paths=$dirtyCount."
    ) -join ' '

    Write-Output $message
}
catch {
    Write-Output "CollisionSpike v2 context hook could not inspect repository state: $($_.Exception.Message) Read AGENTS.md before proceeding."
}
