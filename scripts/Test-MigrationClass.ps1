<#
    .SYNOPSIS
    The migration class guard: fails a pull request whose changed migration is
    not additive and is not declared for the destructive route (#1845).

    .DESCRIPTION
    A migration that only adds is additive and runs on the normal release
    route; one that drops, renames, narrows or rewrites is non-additive and
    runs only on the contained destructive route (docs/engineering.md
    "Migrations", R605). CI classifies each changed migration source from the
    operations its Up method runs (Get-PegasusMigrationClass):

    - an additive one passes;
    - a non-additive one (a Drop, Rename or AlterColumn) passes only when the
      pull request body's "## Migration" section declares it non-additive and
      names the destructive route, whatever else the section says (R761);
    - raw SQL cannot be classified by keyword, so a migration whose Up runs it
      passes only when that section declares its class: additive, or
      non-additive with the destructive route (R761).

    The body declares the class and the route only; the window stays in the
    release approval (R760, ADR-0046). HTML comments in the section are not a
    declaration, so the template's prompt declares nothing.

    .PARAMETER MigrationPaths
    Repository-relative paths of the changed migration files. A path that is
    not a migration source (a Designer file, the model snapshot) or that is
    not in the tree (the change removed it) is skipped.

    .PARAMETER PullRequestBody
    The pull request's body; empty when it has none.

    .PARAMETER RepositoryPath
    The repository root the paths are relative to; the current directory by
    default.
#>
# Named arguments only: a second path given without an array would otherwise
# bind to -PullRequestBody.
[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory)]
    [AllowEmptyCollection()]
    [AllowEmptyString()]
    [string[]] $MigrationPaths,

    [AllowEmptyString()]
    [string] $PullRequestBody = '',

    [string] $RepositoryPath = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')

try {
    # The "## Migration" section runs to the next level-two heading. GitHub
    # stores a body edited in the browser with CRLF line ends.
    $body = $PullRequestBody -replace '\r\n?', "`n"
    $heading = [regex]::Match($body, '(?ims)^##[ \t]+Migration[ \t]*$(?<section>.*?)(?=^##[ \t]|\z)')
    $section = if ($heading.Success) { [regex]::Replace($heading.Groups['section'].Value, '<!--[\s\S]*?-->', ' ') } else { '' }
    $declaresNonAdditive = $section -match '\bnon-additive\b'
    $namesDestructiveRoute = $section -match '\bdestructive\s+route\b'
    $declaresAdditive = $section -match '(?<!\bnon[-\s])\badditive\b'
    $missing = if ($heading.Success) { 'the section does not' } else { 'the body has no such section' }

    $passed = [Collections.Generic.List[string]]::new()
    $skipped = [Collections.Generic.List[string]]::new()
    $failures = [Collections.Generic.List[string]]::new()
    foreach ($relativePath in @($MigrationPaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $name = Split-Path -Leaf $relativePath
        $path = Join-Path $RepositoryPath $relativePath
        if (-not (Test-PegasusMigrationSource -Name $name)) {
            $skipped.Add("$relativePath (not a migration source)")
            continue
        }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $skipped.Add("$relativePath (not in the tree)")
            continue
        }

        $migration = [IO.Path]::GetFileNameWithoutExtension($name)
        $result = Get-PegasusMigrationClass -Path $path
        $operations = $result.Operations -join ', '
        switch ($result.Class) {
            'Additive' {
                $passed.Add("$migration additive")
            }
            'NonAdditive' {
                if ($declaresNonAdditive -and $namesDestructiveRoute) {
                    $passed.Add("$migration non-additive ($operations), declared for the destructive route")
                }
                else {
                    $failures.Add("$relativePath is non-additive: its Up runs $operations. The pull request body's ""## Migration"" section must declare it non-additive and name the destructive route, and $missing (R605, R761).")
                }
            }
            'Undetermined' {
                if ($declaresNonAdditive) {
                    if ($namesDestructiveRoute) {
                        $passed.Add("$migration declared non-additive ($operations) for the destructive route")
                    }
                    else {
                        $failures.Add("$relativePath is declared non-additive but its ""## Migration"" section does not name the destructive route (R605, R761).")
                    }
                }
                elseif ($declaresAdditive) {
                    $passed.Add("$migration declared additive ($operations)")
                }
                else {
                    $failures.Add("$relativePath runs $operations, which cannot be classified by keyword. The pull request body's ""## Migration"" section must declare its class, additive or non-additive with the destructive route, and $missing (R760, R761).")
                }
            }
        }
    }

    if ($failures.Count -gt 0) {
        throw ($failures -join ' ')
    }

    $summary = if ($passed.Count -gt 0) { $passed -join '; ' } else { 'no migration source to classify' }
    if ($skipped.Count -gt 0) { $summary += "; skipped $($skipped -join ', ')" }
    Write-Output "Migration class guard passed: $summary."
}
catch {
    [Console]::Error.WriteLine("Migration class guard failed: $($_.Exception.Message)")
    exit 1
}
