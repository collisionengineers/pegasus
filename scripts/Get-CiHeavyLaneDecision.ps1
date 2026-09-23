[CmdletBinding()]
param(
    [Parameter(Mandatory)][bool] $Build,

    [Parameter(Mandatory)][string] $EventName,

    [string] $BaseRef = '',

    [string[]] $Label = @(),

    # Open pull requests whose base is this pull request's head branch. $null
    # when the lookup failed, which counts as a tip: an unanswered question
    # runs the lanes rather than skipping them.
    [AllowNull()][Nullable[int]] $StackedAbove
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Whether the unit and SQL lanes run, and why. The workflow gathers the inputs
# and this decides, so the rule is tested rather than trusted.
#
# A stack of pull requests holds the same commits once per link. Its tip holds
# every link, so testing the tip covers the stack. The pull request based on dev
# is the bottom, which holds only its own slice; it still runs, because it is
# what merges into dev first. Everything between them defers.
function New-Decision([bool] $Heavy, [string] $Reason) {
    [pscustomobject]@{ Heavy = $Heavy; Reason = $Reason }
}

if (-not $Build) {
    return New-Decision $false 'No build-relevant path changed.'
}

if ($EventName -ne 'pull_request') {
    return New-Decision $true "A $EventName is release-route evidence."
}

if (@($Label | ForEach-Object { $_.Trim() }) -contains 'ci:full') {
    return New-Decision $true "The ci:full label asks for them on a base of '$BaseRef'."
}

if ($BaseRef -in @('dev', 'main')) {
    return New-Decision $true "This pull request merges into '$BaseRef'."
}

if ($null -eq $StackedAbove) {
    return New-Decision $true 'Could not tell whether another pull request is stacked on this one, so it is treated as the tip.'
}

if ($StackedAbove -eq 0) {
    return New-Decision $true "This is the tip of a stack based on '$BaseRef': its tree holds every link below it."
}

New-Decision $false ("Deferred. $StackedAbove open pull request(s) are stacked on this one, " +
    'and the tip of the stack holds these commits. Add the ci:full label and re-run to run them here.')
