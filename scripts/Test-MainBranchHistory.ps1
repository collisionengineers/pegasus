[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Before,

    [Parameter(Mandatory)]
    [string] $Head,

    [Parameter(Mandatory)]
    [string] $ReleaseBranch,

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
    $releaseCommit = @(Invoke-Git rev-parse --verify "$ReleaseBranch^{commit}")[-1].Trim()

    & git -C $RepositoryPath merge-base --is-ancestor $beforeCommit $headCommit 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "The before revision $beforeCommit is not an ancestor of $headCommit; the push is not an append-only update."
    }

    & git -C $RepositoryPath merge-base --is-ancestor $headCommit $releaseCommit 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "The main head is not an ancestor of release branch $($releaseCommit): $headCommit."
    }

    $commits = @(Invoke-Git rev-list --first-parent --reverse "$beforeCommit..$headCommit")
    if ($commits.Count -eq 0) {
        throw "No new first-parent commits exist between $beforeCommit and $headCommit."
    }

    Write-Output "Main history guard passed: $($commits.Count) new first-parent commit(s); main head is contained in the release branch."
}
catch {
    [Console]::Error.WriteLine("Main history guard failed: $($_.Exception.Message)")
    exit 1
}
