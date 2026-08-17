[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Before,

    [Parameter(Mandatory)]
    [string] $Head,

    [string] $RepositoryPath = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments)][string[]] $Arguments)

    $output = @(& git -C $RepositoryPath @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    return $output
}

try {
    if ($Before -match '^0+$') {
        throw 'The before revision is the all-zero sentinel; an append-only merge cannot be proved.'
    }

    $beforeCommit = @(Invoke-Git rev-parse --verify "$Before^{commit}")[-1].Trim()
    $headCommit = @(Invoke-Git rev-parse --verify "$Head^{commit}")[-1].Trim()

    & git -C $RepositoryPath merge-base --is-ancestor $beforeCommit $headCommit 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "The before revision $beforeCommit is not an ancestor of $headCommit; the push is not an append-only update."
    }

    $commits = @(Invoke-Git rev-list --first-parent --reverse "$beforeCommit..$headCommit")
    if ($commits.Count -eq 0) {
        throw "No new first-parent commits exist between $beforeCommit and $headCommit."
    }

    foreach ($commit in $commits) {
        $line = @(Invoke-Git rev-list --parents -n 1 $commit)[-1].Trim()
        $parts = @($line -split '\s+' | Where-Object { $_ })
        if ($parts.Count -ne 3) {
            $parentCount = $parts.Count - 1
            throw "Commit $commit has $parentCount parent(s); every new mainline commit must be a two-parent merge commit."
        }
    }

    Write-Output "Main history guard passed: $($commits.Count) new first-parent commit(s), all two-parent merges."
}
catch {
    Write-Error "Main history guard failed: $($_.Exception.Message)"
    exit 1
}
