<#
    .SYNOPSIS
    Seeds a running local Pegasus run from an immutable corpus: email files
    into the run's local approved inbox and sent-evidence files into its sent
    folder. Reads the corpus; writes only beneath the run root.

    .DESCRIPTION
    Follows docs/runbook.md#safety-rules: the corpus is read immutably (every
    source is hashed before and after its copy and a changed hash fails the
    seed), nothing is renamed or modified in place, and the only outputs are
    copies under artifacts/local-development/<run>/mailbox and a content-free
    seed manifest under artifacts/local-verification/<run>/. The corpus root
    may not be a reparse point and may not sit inside this repository.

    The local approved-inbox adapter reads top-level *.eml files from
    <run>/mailbox/inbox; the local sent adapter reads *.sent.json beneath
    <run>/mailbox/sent. Documents and photographs are not seeded here: there is
    no watched intake folder, so Invoke-LocalVerification.ps1 -UploadFiles
    hands them to the Upload page instead.

    .EXAMPLE
    pwsh ./scripts/Invoke-LocalSeed.ps1 -CorpusRoot /mnt/d/corpus -RunId <id> -Email 'qdos-email-corpus/*.eml' -Limit 20 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$CorpusRoot,
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{32}$')]
    [string]$RunId,
    [string[]]$Email = @('*.eml'),
    [string[]]$SentEvidence = @(),
    [ValidateRange(1, 10000)]
    [int]$Limit = 200
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')

$corpus = Get-Item -LiteralPath $CorpusRoot -Force
if (-not $corpus.PSIsContainer) {
    throw "The corpus root is not a directory: $CorpusRoot"
}
if (($corpus.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "The corpus root is a reparse point and is refused: $CorpusRoot"
}
$corpusPath = [System.IO.Path]::GetFullPath($corpus.FullName).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$comparison = Get-PegasusPathComparison
if ($corpusPath.StartsWith($repositoryRoot, $comparison)) {
    throw "The corpus root must sit outside the repository: $corpusPath"
}

$runRoot = Join-Path (Join-Path $repositoryRoot 'artifacts/local-development') $RunId
$manifestPath = Join-Path $runRoot 'run-manifest.json'
if (-not [System.IO.File]::Exists($manifestPath)) {
    throw "No owned run manifest exists for run '$RunId': $manifestPath"
}
$manifest = [System.IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -Depth 30
if ([string]$manifest.runId -ne $RunId -or [string]$manifest.runtime.profile -ne 'DevelopmentOffline') {
    throw "The manifest at $manifestPath does not describe DevelopmentOffline run '$RunId'."
}
$inboxRoot = [System.IO.Path]::GetFullPath([string]$manifest.resources.paths.mailboxInbox)
$sentRoot = [System.IO.Path]::GetFullPath([string]$manifest.resources.paths.mailboxSent)
foreach ($destination in @($inboxRoot, $sentRoot)) {
    if (-not $destination.StartsWith($runRoot, $comparison)) {
        throw "Refusing a destination outside the run root: $destination"
    }
    [System.IO.Directory]::CreateDirectory($destination) | Out-Null
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][string]$Path)
    # Open read-only and shared: the corpus is never locked or touched.
    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        return [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
    }
    finally {
        $stream.Dispose()
    }
}

function Select-CorpusFiles {
    param([Parameter(Mandatory)][string[]]$Patterns, [Parameter(Mandatory)][string]$RequiredSuffix)

    $selected = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
    foreach ($pattern in $Patterns) {
        $directoryPart = [System.IO.Path]::GetDirectoryName($pattern)
        $filePart = [System.IO.Path]::GetFileName($pattern)
        $searchRoot = if ([string]::IsNullOrEmpty($directoryPart)) { $corpusPath } else { Join-Path $corpusPath $directoryPart }
        if (-not [System.IO.Directory]::Exists($searchRoot)) {
            continue
        }
        $options = [System.IO.EnumerationOptions]::new()
        $options.RecurseSubdirectories = $true
        $options.AttributesToSkip = [System.IO.FileAttributes]::ReparsePoint
        $options.IgnoreInaccessible = $true
        foreach ($file in [System.IO.DirectoryInfo]::new($searchRoot).EnumerateFiles($filePart, $options)) {
            if ($file.Name.EndsWith($RequiredSuffix, [System.StringComparison]::OrdinalIgnoreCase)) {
                $selected.Add($file)
            }
        }
    }
    return @($selected | Sort-Object FullName -Unique | Select-Object -First $Limit)
}

$emailFiles = Select-CorpusFiles -Patterns $Email -RequiredSuffix '.eml'
$sentFiles = if ($SentEvidence.Count -gt 0) { Select-CorpusFiles -Patterns $SentEvidence -RequiredSuffix '.sent.json' } else { @() }

$records = [System.Collections.Generic.List[object]]::new()
function Copy-Immutable {
    param([Parameter(Mandatory)][System.IO.FileInfo]$Source, [Parameter(Mandatory)][string]$DestinationRoot, [Parameter(Mandatory)][string]$Kind)

    $before = Get-Sha256Hex -Path $Source.FullName
    # A content-derived name keeps distinct corpus files apart even when their
    # leaf names collide, and makes a repeat seed idempotent.
    $destinationName = $before.Substring(0, 16) + '-' + $Source.Name
    $destination = Join-Path $DestinationRoot $destinationName
    $status = 'copied'
    if ([System.IO.File]::Exists($destination)) {
        $status = if ((Get-Sha256Hex -Path $destination) -eq $before) { 'already-present' } else { throw "A different file already occupies $destination" }
    }
    elseif ($PSCmdlet.ShouldProcess($destination, "Copy $Kind from $($Source.FullName)")) {
        [System.IO.File]::Copy($Source.FullName, $destination, $false)
        if ((Get-Sha256Hex -Path $destination) -ne $before) {
            throw "The copy of $($Source.FullName) does not match its source hash."
        }
    }
    else {
        $status = 'what-if'
    }
    $after = Get-Sha256Hex -Path $Source.FullName
    if ($after -ne $before) {
        throw "The corpus source changed while being read: $($Source.FullName)"
    }
    $records.Add([ordered]@{
        kind = $Kind
        sourceRelativePath = $Source.FullName.Substring($corpusPath.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar)
        sourceSha256 = $before
        sourceBytes = $Source.Length
        destinationName = $destinationName
        status = $status
    })
}

foreach ($file in $emailFiles) { Copy-Immutable -Source $file -DestinationRoot $inboxRoot -Kind 'email' }
foreach ($file in $sentFiles) { Copy-Immutable -Source $file -DestinationRoot $sentRoot -Kind 'sent-evidence' }

$verificationRoot = Join-Path (Join-Path $repositoryRoot 'artifacts/local-verification') $RunId
$seedManifest = [ordered]@{
    kind = 'Pegasus.LocalVerification.Seed'
    runId = $RunId
    seededUtc = [DateTimeOffset]::UtcNow.ToString('O')
    corpusRoot = $corpusPath
    inboxRoot = $inboxRoot
    sentRoot = $sentRoot
    emailCount = @($records | Where-Object { $_.kind -eq 'email' }).Count
    sentEvidenceCount = @($records | Where-Object { $_.kind -eq 'sent-evidence' }).Count
    whatIf = [bool]$WhatIfPreference
    items = @($records)
}
if (-not $WhatIfPreference) {
    [System.IO.Directory]::CreateDirectory($verificationRoot) | Out-Null
    $seedManifestPath = Join-Path $verificationRoot 'seed-manifest.json'
    [System.IO.File]::WriteAllText($seedManifestPath, (($seedManifest | ConvertTo-Json -Depth 6) + "`n"), [System.Text.UTF8Encoding]::new($false))
    Write-Host "Seed manifest: $seedManifestPath"
}
Write-Host "Emails: $($seedManifest.emailCount) -> $inboxRoot"
Write-Host "Sent evidence: $($seedManifest.sentEvidenceCount) -> $sentRoot"
Write-Host 'The Worker polls the local inbox on its PendingWorkRecovery schedule (every fifth minute) and Sent evidence every minute.'
