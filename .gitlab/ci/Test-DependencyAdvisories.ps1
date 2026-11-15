#!/usr/bin/env pwsh
<#
.SYNOPSIS
    The daily dependency-advisories check (.gitlab-ci.yml): runs the locked
    restore and, when it fails, raises or updates one open issue and fails.

.DESCRIPTION
    Warnings are errors (Directory.Build.props), so a NuGet audit advisory
    fails the same locked restore that CI and the runbook use. The issue is
    written with the project CI variable PEGASUS_ISSUE_TOKEN: a job token
    cannot create an issue. Without that variable the job still fails; it only
    cannot file the issue.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

dotnet restore ./Pegasus.slnx --locked-mode 2>&1 | Tee-Object -FilePath restore.log
if ($LASTEXITCODE -eq 0) { exit 0 }

$ErrorActionPreference = 'Stop'
$date = [DateTime]::UtcNow.ToString('yyyy-MM-dd')
$title = "Dependency advisories $date"
$output = @(Get-Content -LiteralPath restore.log)
$advisories = @($output | Where-Object { $_ -match 'NU19\d\d' } | ForEach-Object { $_.Trim() } | Select-Object -Unique)
if ($advisories.Count -gt 0) {
    $heading = 'Restore output lines naming NU19xx advisories:'
    $lines = $advisories
} else {
    $heading = 'Last 40 lines of the restore output:'
    $lines = $output | Select-Object -Last 40
}
$fence = '```'
$body = @(
    "Date (UTC): $date"
    "dev commit: $(git rev-parse HEAD)"
    "Run: $env:CI_PIPELINE_URL"
    'Reproduce: dotnet restore ./Pegasus.slnx --locked-mode'
    ''
    $heading
    ''
    $fence
    $lines
    $fence
) -join "`n"

if ([string]::IsNullOrWhiteSpace($env:PEGASUS_ISSUE_TOKEN)) {
    Write-Host $body
    throw 'The locked restore failed and PEGASUS_ISSUE_TOKEN is not set, so no issue was raised.'
}

$headers = @{ 'PRIVATE-TOKEN' = $env:PEGASUS_ISSUE_TOKEN }
$issues = "$env:CI_API_V4_URL/projects/$env:CI_PROJECT_ID/issues"
$open = @(Invoke-RestMethod -Headers $headers -Uri "${issues}?state=opened&search=Dependency%20advisories&in=title&per_page=100" |
    ForEach-Object { $_ } | Where-Object { $_.title -like 'Dependency advisories *' })
if ($open.Count -eq 0) {
    $null = Invoke-RestMethod -Method Post -Headers $headers -Uri $issues -Body @{ title = $title; description = $body; labels = 'type:task' }
} else {
    $null = Invoke-RestMethod -Method Post -Headers $headers -Uri "$issues/$($open[0].iid)/notes" -Body @{ body = $body }
}
exit 1
