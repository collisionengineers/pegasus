<#
    .SYNOPSIS
    Runs the browser verification walk against a running local Pegasus run
    and prints its pass/fail/skipped table.

    .DESCRIPTION
    Reads the run manifest directly (never the lifecycle script), requires the
    run to be Running in the DevelopmentOffline profile, derives the Web origin
    and the enabled live-integration flags from the manifest, locates a
    Chromium, and runs scripts/Test-LocalVerificationBrowser.mjs with those
    values in its environment. Evidence lands under
    artifacts/local-verification/<run-id>/. The exit code is the walk's.

    .EXAMPLE
    pwsh ./scripts/Invoke-LocalVerification.ps1 -RunId <id>
    pwsh ./scripts/Invoke-LocalVerification.ps1 -RunId <id> -User engineer1 -Password (Read-Host -AsSecureString) -Role Engineer -UploadFiles ./sample.jpg
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{32}$')]
    [string]$RunId,
    [string]$User,
    [securestring]$Password,
    [ValidateSet('Administrator', 'Engineer', 'User')]
    [string]$Role = 'Administrator',
    [string[]]$UploadFiles = @(),
    [string]$Chrome,
    [string]$PrincipalCode = 'AX',
    [string]$Registration = 'AB12CDE'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')

$manifestPath = Join-Path (Join-Path (Join-Path $repositoryRoot 'artifacts/local-development') $RunId) 'run-manifest.json'
if (-not [System.IO.File]::Exists($manifestPath)) {
    throw "No owned run manifest exists for run '$RunId': $manifestPath"
}
$manifest = [System.IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -Depth 30
if ([string]$manifest.runId -ne $RunId -or [string]$manifest.runtime.profile -ne 'DevelopmentOffline') {
    throw "The manifest does not describe DevelopmentOffline run '$RunId'."
}
if ([string]$manifest.state -ne 'Running') {
    throw "Run '$RunId' is '$($manifest.state)', not Running. Start it first."
}
$webBase = [string]$manifest.endpoints.webBase
$flagNames = Format-PegasusLiveIntegrationFlags -Flags $manifest.runtime.liveIntegrations
if ($Role -ne 'Administrator' -and [string]::IsNullOrWhiteSpace($User)) {
    throw "Role '$Role' needs -User and -Password: only the Administrator is signed in automatically."
}

$chromium = $Chrome
if ([string]::IsNullOrWhiteSpace($chromium)) {
    $chromium = $env:CHROME
}
if ([string]::IsNullOrWhiteSpace($chromium)) {
    $playwrightRoot = Join-Path $HOME '.cache/ms-playwright'
    if ([System.IO.Directory]::Exists($playwrightRoot)) {
        $chromium = Get-ChildItem -LiteralPath $playwrightRoot -Directory -Filter 'chromium-*' |
            Sort-Object Name -Descending |
            ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Directory -Filter 'chrome-linux*' } |
            ForEach-Object { Join-Path $_.FullName 'chrome' } |
            Where-Object { [System.IO.File]::Exists($_) } |
            Select-Object -First 1
    }
}
if ([string]::IsNullOrWhiteSpace($chromium)) {
    foreach ($candidate in @('chromium', 'chromium-browser', 'google-chrome', 'chrome')) {
        $found = Get-Command $candidate -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $found) { $chromium = $found.Source; break }
    }
}
if ([string]::IsNullOrWhiteSpace($chromium) -or -not [System.IO.File]::Exists($chromium)) {
    throw 'No Chromium found. Set CHROME or -Chrome to a Chrome/Chromium executable.'
}
$node = Get-Command node -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $node) {
    throw 'node is required. Run pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline.'
}

$resolvedUploads = @($UploadFiles | ForEach-Object {
    $full = [System.IO.Path]::GetFullPath($_)
    if (-not [System.IO.File]::Exists($full)) { throw "Upload file does not exist: $full" }
    $full
})

$walk = Join-Path $PSScriptRoot 'Test-LocalVerificationBrowser.mjs'
$environment = @{
    PEGASUS_URL = $webBase
    PEGASUS_RUN_ID = $RunId
    PEGASUS_ROLE = $Role
    PEGASUS_FLAGS = $(if ($flagNames -eq 'none') { '' } else { $flagNames })
    PEGASUS_UPLOAD_FILES = ($resolvedUploads -join ',')
    PEGASUS_PRINCIPAL_CODE = $PrincipalCode
    PEGASUS_REGISTRATION = $Registration
    CHROME = $chromium
}
if (-not [string]::IsNullOrWhiteSpace($User)) {
    $environment['PEGASUS_USER'] = $User
    $environment['PEGASUS_PASSWORD'] = if ($null -ne $Password) { [System.Net.NetworkCredential]::new('', $Password).Password } else { '' }
}

Write-Host "Run $RunId at $webBase as $Role; live integrations: $flagNames; browser: $chromium"
$previous = @{}
foreach ($entry in $environment.GetEnumerator()) {
    $previous[$entry.Key] = [System.Environment]::GetEnvironmentVariable($entry.Key)
    [System.Environment]::SetEnvironmentVariable($entry.Key, $entry.Value)
}
try {
    & $node.Source $walk
    $exitCode = $LASTEXITCODE
}
finally {
    foreach ($entry in $previous.GetEnumerator()) {
        [System.Environment]::SetEnvironmentVariable($entry.Key, $entry.Value)
    }
}

$verificationRoot = Join-Path (Join-Path $repositoryRoot 'artifacts/local-verification') $RunId
$latest = Get-ChildItem -LiteralPath $verificationRoot -Directory -Filter "$($Role.ToLowerInvariant())-*" -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending |
    Select-Object -First 1
if ($null -ne $latest -and [System.IO.File]::Exists((Join-Path $latest.FullName 'result.json'))) {
    $result = [System.IO.File]::ReadAllText((Join-Path $latest.FullName 'result.json')) | ConvertFrom-Json -Depth 10
    $result.steps | ForEach-Object {
        [pscustomobject][ordered]@{ Step = $_.name; Status = $_.status; Detail = $_.detail }
    } | Format-Table -AutoSize -Wrap | Out-String | Write-Host
    Write-Host "Passed $($result.counts.passed), failed $($result.counts.failed), skipped $($result.counts.skipped). Evidence: $(Join-Path $latest.FullName 'result.json')"
}
exit $exitCode
