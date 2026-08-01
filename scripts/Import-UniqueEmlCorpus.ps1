[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SourceRoot,
    [string]$PegasusRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$ReportDirectory
)

$ErrorActionPreference = 'Stop'
$script:Errors = [System.Collections.Generic.List[object]]::new()
$script:SourceRows = [System.Collections.Generic.List[object]]::new()
$script:PegasusRows = [System.Collections.Generic.List[object]]::new()

function Add-ScanError {
    param(
        [string]$Scope,
        [string]$Path,
        [string]$Operation,
        [string]$Message
    )

    $script:Errors.Add([pscustomobject]([ordered]@{
        scope = $Scope
        path = $Path
        operation = $Operation
        message = $Message
    })) | Out-Null
    Write-Warning "$Scope $Operation failed for '$Path': $Message"
}

function Resolve-DirectoryRoot {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Name
    )

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
    $item = Get-Item -LiteralPath $resolved.Path -Force -ErrorAction Stop
    if (-not ($item -is [System.IO.DirectoryInfo])) {
        throw "$Name must be an existing directory: $Path"
    }
    return $item.FullName.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
}

function Test-PathWithinDirectory {
    <#
        .SYNOPSIS
        Returns $true when Path is Directory itself or lies beneath it.

        .DESCRIPTION
        This is a containment guard used to exclude the Pegasus tree from the
        source scan, so it must fail closed. Comparing separator-terminated
        string prefixes cannot do that portably: the separator differs by
        platform, and case sensitivity differs by filesystem. Relative-path
        resolution answers the question directly on both platforms.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Directory
    )

    $comparison = if ($IsWindows) {
        [System.StringComparison]::OrdinalIgnoreCase
    }
    else {
        [System.StringComparison]::Ordinal
    }

    $normalizedPath = [System.IO.Path]::GetFullPath($Path)
    $normalizedDirectory = [System.IO.Path]::GetFullPath($Directory)
    if ([string]::Equals($normalizedPath, $normalizedDirectory, $comparison)) {
        return $true
    }

    $relative = [System.IO.Path]::GetRelativePath($normalizedDirectory, $normalizedPath)
    if ([System.IO.Path]::IsPathRooted($relative)) {
        return $false
    }

    return -not ($relative -eq '..' -or
        $relative.StartsWith('..' + [System.IO.Path]::DirectorySeparatorChar, $comparison) -or
        $relative.StartsWith('../', $comparison))
}

function Get-EmlFilePaths {
    param(
        [Parameter(Mandatory)]
        [string]$Root,
        [string]$ExcludedRoot,
        [Parameter(Mandatory)]
        [ValidateSet('source', 'pegasus')]
        [string]$Scope
    )

    $pending = [System.Collections.Generic.Stack[string]]::new()
    $pending.Push($Root)

    while ($pending.Count -gt 0) {
        $directoryPath = $pending.Pop()
        if ($Scope -eq 'source' -and (Test-PathWithinDirectory -Path $directoryPath -Directory $ExcludedRoot)) {
            continue
        }

        $entries = $null
        try {
            $entries = [System.IO.Directory]::EnumerateFileSystemEntries($directoryPath)
            foreach ($entryPath in $entries) {
                try {
                    $entry = Get-Item -LiteralPath $entryPath -Force -ErrorAction Stop
                    if ($entry.PSIsContainer) {
                        if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                            Add-ScanError -Scope $Scope -Path $entry.FullName -Operation 'reparse_point_skipped' -Message 'Directory was not traversed.'
                            continue
                        }
                        if ($Scope -eq 'source' -and (Test-PathWithinDirectory -Path $entry.FullName -Directory $ExcludedRoot)) {
                            continue
                        }
                        $pending.Push($entry.FullName)
                        continue
                    }

                    if ($entry.Extension.Equals('.eml', [System.StringComparison]::OrdinalIgnoreCase)) {
                        [pscustomobject]([ordered]@{
                            path = $entry.FullName
                            length = [long]$entry.Length
                            sha256 = ''
                            readStatus = 'unread'
                            comparisonStatus = ''
                            matchingPegasusPath = ''
                            representativeSourcePath = ''
                            importPath = ''
                            copyStatus = ''
                        })
                    }
                }
                catch {
                    Add-ScanError -Scope $Scope -Path $entryPath -Operation 'entry_inspection' -Message $_.Exception.Message
                }
            }
        }
        catch {
            Add-ScanError -Scope $Scope -Path $directoryPath -Operation 'directory_enumeration' -Message $_.Exception.Message
        }
    }
}

function Get-FileFingerprint {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [ValidateSet('source', 'pegasus', 'copy')]
        [string]$Scope
    )

    $stream = $null
    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite)
        $length = $stream.Length
        $hash = [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream))
        return [pscustomobject]([ordered]@{
            path = $Path
            length = [long]$length
            sha256 = $hash
            readStatus = 'readable'
        })
    }
    catch {
        Add-ScanError -Scope $Scope -Path $Path -Operation 'file_read' -Message $_.Exception.Message
        return [pscustomobject]([ordered]@{
            path = $Path
            length = -1L
            sha256 = ''
            readStatus = 'read_error'
        })
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function Test-FileContentEqual {
    param(
        [Parameter(Mandatory)]
        [string]$LeftPath,
        [Parameter(Mandatory)]
        [string]$RightPath
    )

    $left = $null
    $right = $null
    try {
        $left = [System.IO.File]::Open($LeftPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        $right = [System.IO.File]::Open($RightPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        if ($left.Length -ne $right.Length) {
            return $false
        }

        $leftBuffer = [byte[]]::new(1048576)
        $rightBuffer = [byte[]]::new(1048576)
        while ($true) {
            $leftRead = $left.Read($leftBuffer, 0, $leftBuffer.Length)
            $rightRead = $right.Read($rightBuffer, 0, $rightBuffer.Length)
            if ($leftRead -ne $rightRead) {
                return $false
            }
            if ($leftRead -eq 0) {
                return $true
            }
            for ($index = 0; $index -lt $leftRead; $index++) {
                if ($leftBuffer[$index] -ne $rightBuffer[$index]) {
                    return $false
                }
            }
        }
    }
    finally {
        if ($null -ne $left) { $left.Dispose() }
        if ($null -ne $right) { $right.Dispose() }
    }
}

function Get-FingerprintKey {
    param(
        [Parameter(Mandatory)]
        [long]$Length,
        [Parameter(Mandatory)]
        [string]$Sha256
    )

    return "$Length|$Sha256"
}

function Get-SafeImportFileName {
    param(
        [Parameter(Mandatory)]
        [string]$ImportRoot,
        [Parameter(Mandatory)]
        [string]$Sha256,
        [Parameter(Mandatory)]
        [string]$OriginalName
    )

    $extension = [System.IO.Path]::GetExtension($OriginalName)
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($OriginalName)
    $maxPathLength = 240
    $maxNameLength = $maxPathLength - $ImportRoot.Length - 1
    $basePrefix = "$Sha256-"
    $minimumNameLength = $basePrefix.Length + $extension.Length
    if ($maxNameLength -lt $minimumNameLength) {
        throw "Import path is too long for '$OriginalName'."
    }

    $availableStemLength = $maxNameLength - $basePrefix.Length - $extension.Length
    if ($stem.Length -gt $availableStemLength) {
        $stem = $stem.Substring(0, $availableStemLength)
    }

    for ($suffix = 1; $suffix -le 1000000; $suffix++) {
        $variantPrefix = if ($suffix -eq 1) { $basePrefix } else { "$Sha256-$suffix-" }
        $candidateName = "$variantPrefix$stem$extension"
        $candidatePath = Join-Path $ImportRoot $candidateName
        if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
            return $candidatePath
        }
        try {
            if (Test-FileContentEqual -LeftPath $candidatePath -RightPath $script:CurrentSourcePath) {
                return $candidatePath
            }
        }
        catch {
            Add-ScanError -Scope 'copy' -Path $candidatePath -Operation 'destination_compare' -Message $_.Exception.Message
        }
    }

    throw "Unable to select a unique import name for '$OriginalName'."
}

function Copy-VerifiedEml {
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Row,
        [Parameter(Mandatory)]
        [string]$ImportRoot
    )

    $script:CurrentSourcePath = $Row.path
    $fingerprint = Get-FileFingerprint -Path $Row.path -Scope 'copy'
    if ($fingerprint.readStatus -ne 'readable' -or
        $fingerprint.length -ne [long]$Row.length -or
        $fingerprint.sha256 -cne $Row.sha256) {
        Add-ScanError -Scope 'copy' -Path $Row.path -Operation 'source_changed' -Message 'Source length or SHA-256 changed after inventory.'
        $Row.copyStatus = 'source_changed'
        return
    }

    $destinationPath = $null
    try {
        $destinationPath = Get-SafeImportFileName -ImportRoot $ImportRoot -Sha256 $Row.sha256 -OriginalName ([System.IO.Path]::GetFileName($Row.path))
        if (Test-Path -LiteralPath $destinationPath -PathType Leaf) {
            $Row.importPath = $destinationPath
            $Row.copyStatus = 'not_needed'
            return
        }

        $temporaryPath = Join-Path $ImportRoot ".eml-import-$([guid]::NewGuid().ToString('N')).tmp"
        try {
            [System.IO.File]::Copy($Row.path, $temporaryPath, $false)
            if (-not (Test-FileContentEqual -LeftPath $Row.path -RightPath $temporaryPath)) {
                throw 'Temporary copy failed byte comparison.'
            }
            [System.IO.File]::Move($temporaryPath, $destinationPath)
            $Row.importPath = $destinationPath
            $Row.copyStatus = 'copied'
        }
        finally {
            if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
                Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
            }
        }
    }
    catch {
        if ($null -ne $destinationPath -and (Test-Path -LiteralPath $destinationPath -PathType Leaf)) {
            try {
                if (Test-FileContentEqual -LeftPath $Row.path -RightPath $destinationPath) {
                    $Row.importPath = $destinationPath
                    $Row.copyStatus = 'not_needed'
                    return
                }
            }
            catch {
                Add-ScanError -Scope 'copy' -Path $destinationPath -Operation 'destination_compare' -Message $_.Exception.Message
            }
        }
        Add-ScanError -Scope 'copy' -Path $Row.path -Operation 'copy' -Message $_.Exception.Message
        $Row.copyStatus = 'copy_error'
    }
}

$sourceRootResolved = Resolve-DirectoryRoot -Path $SourceRoot -Name 'SourceRoot'
$pegasusRootResolved = Resolve-DirectoryRoot -Path $PegasusRoot -Name 'PegasusRoot'
$importRoot = Join-Path $pegasusRootResolved 'corpus/import'

if ([string]::IsNullOrWhiteSpace($ReportDirectory)) {
    $stamp = [System.DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ', [System.Globalization.CultureInfo]::InvariantCulture)
    $ReportDirectory = Join-Path $pegasusRootResolved "artifacts/intake/eml-corpus-import/$stamp"
}
$reportDirectoryResolved = [System.IO.Path]::GetFullPath($ReportDirectory)
New-Item -ItemType Directory -Force -Path $reportDirectoryResolved | Out-Null
$sourceManifestPath = Join-Path $reportDirectoryResolved 'source-eml.csv'
$pegasusManifestPath = Join-Path $reportDirectoryResolved 'pegasus-eml.csv'
$errorReportPath = Join-Path $reportDirectoryResolved 'scan-errors.csv'
foreach ($reportPath in @($sourceManifestPath, $pegasusManifestPath, $errorReportPath)) {
    if (Test-Path -LiteralPath $reportPath) {
        throw "Report file already exists: $reportPath"
    }
}

$sourceCandidates = @(Get-EmlFilePaths -Root $sourceRootResolved -ExcludedRoot $pegasusRootResolved -Scope source)
$pegasusCandidates = @(Get-EmlFilePaths -Root $pegasusRootResolved -Scope pegasus)
$sourceCandidates = @($sourceCandidates | Sort-Object -Property path)
$pegasusCandidates = @($pegasusCandidates | Sort-Object -Property path)

foreach ($candidate in $sourceCandidates) {
    $fingerprint = Get-FileFingerprint -Path $candidate.path -Scope source
    $candidate.length = $fingerprint.length
    $candidate.sha256 = $fingerprint.sha256
    $candidate.readStatus = $fingerprint.readStatus
    if ($fingerprint.readStatus -eq 'read_error') {
        $candidate.comparisonStatus = 'read_error'
    }
    $script:SourceRows.Add($candidate) | Out-Null
}

foreach ($candidate in $pegasusCandidates) {
    $fingerprint = Get-FileFingerprint -Path $candidate.path -Scope pegasus
    $candidate.length = $fingerprint.length
    $candidate.sha256 = $fingerprint.sha256
    $candidate.readStatus = $fingerprint.readStatus
    $script:PegasusRows.Add($candidate) | Out-Null
}

$pegasusLookup = @{}
foreach ($row in $script:PegasusRows | Where-Object readStatus -eq 'readable') {
    $key = Get-FingerprintKey -Length $row.length -Sha256 $row.sha256
    if (-not $pegasusLookup.ContainsKey($key)) {
        $pegasusLookup[$key] = [System.Collections.Generic.List[object]]::new()
    }
    $pegasusLookup[$key].Add($row) | Out-Null
}

foreach ($row in $script:SourceRows | Where-Object readStatus -eq 'readable') {
    $key = Get-FingerprintKey -Length $row.length -Sha256 $row.sha256
    if (-not $pegasusLookup.ContainsKey($key)) {
        continue
    }

    $comparisonFailed = $false
    foreach ($candidate in $pegasusLookup[$key] | Sort-Object -Property path) {
        try {
            if (Test-FileContentEqual -LeftPath $row.path -RightPath $candidate.path) {
                $row.comparisonStatus = 'already_in_pegasus'
                $row.matchingPegasusPath = $candidate.path
                break
            }
        }
        catch {
            Add-ScanError -Scope 'source' -Path $row.path -Operation 'byte_comparison' -Message $_.Exception.Message
            $row.comparisonStatus = 'read_error'
            $comparisonFailed = $true
            break
        }
    }
    if ($comparisonFailed) {
        continue
    }
}

$unmatched = @($script:SourceRows | Where-Object {
    $_.readStatus -eq 'readable' -and [string]::IsNullOrEmpty($_.comparisonStatus)
})
$representatives = [System.Collections.Generic.List[object]]::new()
foreach ($row in $unmatched) {
    $matchedRepresentative = $null
    foreach ($representative in $representatives) {
        if ((Get-FingerprintKey -Length $row.length -Sha256 $row.sha256) -ne
            (Get-FingerprintKey -Length $representative.length -Sha256 $representative.sha256)) {
            continue
        }
        try {
            if (Test-FileContentEqual -LeftPath $row.path -RightPath $representative.path) {
                $matchedRepresentative = $representative
                break
            }
        }
        catch {
            Add-ScanError -Scope 'source' -Path $row.path -Operation 'byte_comparison' -Message $_.Exception.Message
            $row.comparisonStatus = 'read_error'
            break
        }
    }
    if ($row.comparisonStatus -eq 'read_error') {
        continue
    }
    if ($null -ne $matchedRepresentative) {
        $row.comparisonStatus = 'duplicate_source'
        $row.representativeSourcePath = $matchedRepresentative.path
        continue
    }
    $row.comparisonStatus = 'selected_for_import'
    $row.representativeSourcePath = $row.path
    $representatives.Add($row) | Out-Null
}

New-Item -ItemType Directory -Force -Path $importRoot | Out-Null
foreach ($representative in $representatives) {
    Copy-VerifiedEml -Row $representative -ImportRoot $importRoot
}
foreach ($row in $script:SourceRows) {
    if ($row.comparisonStatus -eq 'already_in_pegasus') {
        $row.copyStatus = 'not_needed'
    }
    elseif ($row.comparisonStatus -eq 'read_error') {
        $row.copyStatus = 'read_error'
    }
    elseif ($row.comparisonStatus -eq 'duplicate_source') {
        $representative = $script:SourceRows | Where-Object path -eq $row.representativeSourcePath | Select-Object -First 1
        if ($null -ne $representative) {
            $row.importPath = $representative.importPath
            $row.copyStatus = 'skipped_duplicate'
        }
        else {
            $row.copyStatus = 'copy_error'
        }
    }
}

$sourceExport = @($script:SourceRows | Sort-Object -Property path | Select-Object path, length, sha256, comparisonStatus, matchingPegasusPath, representativeSourcePath, importPath, copyStatus)
$pegasusExport = @($script:PegasusRows | Sort-Object -Property path | Select-Object path, length, sha256, readStatus)
if ($sourceExport.Count -gt 0) {
    $sourceExport | Export-Csv -LiteralPath $sourceManifestPath -NoTypeInformation -Encoding utf8
}
else {
    Set-Content -LiteralPath $sourceManifestPath -Value 'path,length,sha256,comparisonStatus,matchingPegasusPath,representativeSourcePath,importPath,copyStatus' -Encoding utf8
}
if ($pegasusExport.Count -gt 0) {
    $pegasusExport | Export-Csv -LiteralPath $pegasusManifestPath -NoTypeInformation -Encoding utf8
}
else {
    Set-Content -LiteralPath $pegasusManifestPath -Value 'path,length,sha256,readStatus' -Encoding utf8
}
if ($script:Errors.Count -gt 0) {
    @($script:Errors) | Select-Object scope, path, operation, message | Export-Csv -LiteralPath $errorReportPath -NoTypeInformation -Encoding utf8
}
else {
    Set-Content -LiteralPath $errorReportPath -Value 'scope,path,operation,message' -Encoding utf8
}

$summary = [pscustomobject]([ordered]@{
    sourceManifestPath = $sourceManifestPath
    pegasusManifestPath = $pegasusManifestPath
    errorReportPath = $errorReportPath
    sourceCount = $sourceExport.Count
    pegasusCount = $pegasusExport.Count
    selectedCount = $representatives.Count
    copiedCount = @($representatives | Where-Object copyStatus -eq 'copied').Count
    errorCount = $script:Errors.Count
    complete = ($script:Errors.Count -eq 0)
})
Write-Host "Source EML files: $($summary.sourceCount)"
Write-Host "Pegasus EML files: $($summary.pegasusCount)"
Write-Host "Selected unique imports: $($summary.selectedCount)"
Write-Host "Copied imports: $($summary.copiedCount)"
Write-Host "Errors: $($summary.errorCount)"
$summary
