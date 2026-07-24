[CmdletBinding()]
param(
    [string]$CorpusRelativePath = 'corpus/emailevals/qdos-email-corpus',
    [string]$OutputFileName = 'qdos-email-corpus-inventory.json'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$corpusRoot = Join-Path $root $CorpusRelativePath
if ([System.IO.Path]::GetFileName($OutputFileName) -ne $OutputFileName -or
    -not $OutputFileName.EndsWith('.json', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputFileName must be a JSON file name without a directory.'
}
$outputPath = Join-Path $root "artifacts/evaluation/$OutputFileName"

if (-not (Test-Path -LiteralPath $corpusRoot -PathType Container)) {
    throw "The ignored local $CorpusRelativePath directory is absent. No inventory was written."
}

$entries = [System.Collections.Generic.List[string]]::new()
$extensionCounts = @{}
$extensionBytes = @{}
$totalBytes = [long]0

try {
    foreach ($path in [System.IO.Directory]::EnumerateFiles(
            $corpusRoot,
            '*',
            [System.IO.SearchOption]::AllDirectories)) {
        $extension = [System.IO.Path]::GetExtension($path).ToLowerInvariant()
        if ([string]::IsNullOrEmpty($extension)) {
            $extension = '[none]'
        }
        elseif ($extension -notmatch '^\.[a-z0-9]{1,16}$') {
            $extension = '[other]'
        }

        $stream = $null
        try {
            $stream = [System.IO.File]::Open(
                $path,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::Read,
                [System.IO.FileShare]::Read)
            $length = $stream.Length
            $contentHash = [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream))
        }
        finally {
            if ($null -ne $stream) {
                $stream.Dispose()
            }
        }

        $entries.Add("$extension`t$length`t$contentHash")
        $totalBytes += $length
        $extensionCounts[$extension] = 1 + [int]($extensionCounts[$extension] ?? 0)
        $extensionBytes[$extension] = $length + [long]($extensionBytes[$extension] ?? 0)
    }
}
catch {
    throw 'Corpus inventory failed while reading the immutable local evidence. No source filename or content was emitted.'
}

if ($entries.Count -eq 0) {
    throw "The ignored local $CorpusRelativePath directory is empty. No inventory was written."
}

$entryArray = $entries.ToArray()
[System.Array]::Sort($entryArray, [System.StringComparer]::Ordinal)
$manifestBytes = [System.Text.Encoding]::UTF8.GetBytes([string]::Join("`n", $entryArray))
$manifestHash = [System.Convert]::ToHexString(
    [System.Security.Cryptography.SHA256]::HashData($manifestBytes)
)

$extensions = @(
    $extensionCounts.Keys |
        Sort-Object |
        ForEach-Object {
            [ordered]@{
                extension = $_
                fileCount = [int]$extensionCounts[$_]
                bytes = [long]$extensionBytes[$_]
            }
        }
)
$inventory = [ordered]@{
    fileCount = $entryArray.Count
    bytes = $totalBytes
    extensions = $extensions
    manifestHash = $manifestHash
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
$inventory | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $outputPath -Encoding utf8

Write-Host "Corpus inventory written: $($inventory.fileCount) files, $($inventory.bytes) bytes, manifest $($inventory.manifestHash)."
Write-Host "Inventory artifact: $outputPath"
