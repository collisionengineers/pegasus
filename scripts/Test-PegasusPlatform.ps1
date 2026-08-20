[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw 'Test-PegasusPlatform.ps1 must run on Windows because it exercises the LocalDB branch.'
}

. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')

$script:RequestedInstance = 'PegasusDevelopment_PLAT014_contract'
$script:FixtureOutput = ''
$script:FixtureExitCode = 0

function Invoke-LocalDbFixture {
    $script:FixtureOutput
    $global:LASTEXITCODE = $script:FixtureExitCode
}

function Assert-DatabaseState {
    param(
        [Parameter(Mandatory)][string]$Case,
        [Parameter(Mandatory)][string]$Output,
        [Parameter(Mandatory)][int]$ExitCode,
        [Parameter(Mandatory)][string]$Expected
    )

    $script:FixtureOutput = $Output
    $script:FixtureExitCode = $ExitCode
    $actual = Get-PegasusDatabaseState `
        -InstanceName $script:RequestedInstance `
        -Command 'Invoke-LocalDbFixture'

    if ($actual -ne $Expected) {
        throw "$Case expected '$Expected'; got '$actual'."
    }
}

$missingFixture = "Printing of LocalDB instance `"$script:RequestedInstance`" information failed because of the following error:`r`n`r`nLocalDB instance `"$script:RequestedInstance`" doesn't exist! "

Assert-DatabaseState -Case 'zero-exit explicit missing instance' -Output $missingFixture -ExitCode 0 -Expected 'Missing'
Assert-DatabaseState -Case 'zero-exit missing different instance' -Output 'LocalDB instance "PegasusDevelopment_other" doesn''t exist! ' -ExitCode 0 -Expected 'Unknown'
Assert-DatabaseState -Case 'zero-exit wrapper-only failure' -Output "Printing of LocalDB instance `"$script:RequestedInstance`" information failed because of the following error:" -ExitCode 0 -Expected 'Unknown'
Assert-DatabaseState -Case 'zero-exit unrecognized response' -Output 'LocalDB returned an unrecognized diagnostic.' -ExitCode 0 -Expected 'Unknown'
Assert-DatabaseState -Case 'running state' -Output 'State: Running' -ExitCode 0 -Expected 'Running'
Assert-DatabaseState -Case 'stopped state' -Output 'State: Stopped' -ExitCode 0 -Expected 'Stopped'
Assert-DatabaseState -Case 'contradictory state and missing response' -Output "State: Running`r`nLocalDB instance `"$script:RequestedInstance`" doesn't exist! " -ExitCode 0 -Expected 'Unknown'
Assert-DatabaseState -Case 'non-zero response' -Output 'LocalDB command failed.' -ExitCode 1 -Expected 'Missing'

$global:LASTEXITCODE = 0
Write-Output 'Pegasus platform LocalDB state classification passed.'
