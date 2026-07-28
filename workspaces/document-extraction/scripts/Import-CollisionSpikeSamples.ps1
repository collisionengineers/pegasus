[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$CorpusRoot,

    [Parameter(Mandatory)]
    [string]$DestinationRoot
)

# Manual local-only evidence import. The repository contract prohibits copying
# genuine corpus data unless a current user instruction explicitly authorises it.
$ErrorActionPreference = 'Stop'
$maximumPositiveInputBytes = 10MB

function Resolve-ExistingDirectory {
    param([Parameter(Mandatory)][string]$Path)

    $item = Get-Item -LiteralPath $Path -Force
    if (-not $item.PSIsContainer) {
        throw "Expected a directory."
    }

    if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Reparse-point roots are not accepted."
    }

    return [System.IO.Path]::GetFullPath($item.FullName)
}

function Get-SizeBand {
    param([long]$Length)

    if ($Length -lt 256KB) { return 'under-256-kib' }
    if ($Length -lt 1MB) { return '256-kib-to-1-mib' }
    if ($Length -lt 5MB) { return '1-to-5-mib' }
    return '5-to-10-mib'
}

function Get-FeatureMarkers {
    param(
        [Parameter(Mandatory)][string]$Extension,
        [Parameter(Mandatory)][byte[]]$Bytes
    )

    $text = [System.Text.Encoding]::Latin1.GetString($Bytes)
    if ($Extension -eq '.msg') {
        $text += [System.Text.Encoding]::Unicode.GetString($Bytes)
    }
    $markersByFormat = switch ($Extension) {
        '.pdf' {
            [ordered]@{
                pages = '/Type /Page'
                fonts = '/Font'
                images = '/Image'
                annotations = '/Annots'
                forms = '/AcroForm'
                embedded_files = '/EmbeddedFile'
                object_streams = '/ObjStm'
                xref_streams = '/XRef'
                metadata = '/Metadata'
            }
        }
        '.eml' {
            [ordered]@{
                mime = 'MIME-Version:'
                multipart = 'multipart/'
                attachment = 'Content-Disposition: attachment'
                inline = 'Content-Disposition: inline'
                base64 = 'Content-Transfer-Encoding: base64'
                quoted_printable = 'Content-Transfer-Encoding: quoted-printable'
                html = 'text/html'
                nested_message = 'message/rfc822'
            }
        }
        '.msg' {
            [ordered]@{
                recipients = '__recip_version1.0'
                attachments = '__attach_version1.0'
                named_properties = '__nameid_version1.0'
                body = '__substg1.0_1000'
                html = '__substg1.0_1013'
                rtf = '__substg1.0_1009'
                message_class = '__substg1.0_001a'
                transport_headers = '__substg1.0_007d'
            }
        }
        default { throw "Unsupported extension." }
    }

    $markers = foreach ($entry in $markersByFormat.GetEnumerator()) {
        if ($text.Contains($entry.Value, [System.StringComparison]::OrdinalIgnoreCase)) {
            $entry.Key
        }
    }

    return @($markers)
}

$resolvedCorpusRoot = Resolve-ExistingDirectory -Path $CorpusRoot
$destinationParent = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($DestinationRoot))
$resolvedDestinationParent = Resolve-ExistingDirectory -Path $destinationParent
$resolvedDestinationRoot = [System.IO.Path]::GetFullPath($DestinationRoot)

if (-not $resolvedDestinationRoot.StartsWith($resolvedDestinationParent + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Destination must be a new child of the resolved destination parent."
}

if (Test-Path -LiteralPath $resolvedDestinationRoot) {
    throw "Destination already exists. Refusing to overwrite it."
}

$sourceFiles = @(
    Get-ChildItem -LiteralPath $resolvedCorpusRoot -Recurse -File -Force |
        Where-Object { $_.Extension.ToLowerInvariant() -in @('.pdf', '.eml', '.msg') }
)

if ($sourceFiles.Count -eq 0) {
    throw "No requested format files were found."
}

if ($sourceFiles.Where({ ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -gt 0) {
    throw "The candidate set contains a reparse point."
}

$topLevelAreas = @(
    $sourceFiles |
        ForEach-Object {
            [System.IO.Path]::GetRelativePath($resolvedCorpusRoot, $_.FullName).Split([System.IO.Path]::DirectorySeparatorChar)[0]
        } |
        Sort-Object -Unique
)

$areaLabels = @{}
for ($index = 0; $index -lt $topLevelAreas.Count; $index++) {
    $areaLabels[$topLevelAreas[$index]] = 'Area-{0:D2}' -f ($index + 1)
}

$candidates = foreach ($file in $sourceFiles) {
    if ($file.Length -gt $maximumPositiveInputBytes) {
        continue
    }

    $relativePath = [System.IO.Path]::GetRelativePath($resolvedCorpusRoot, $file.FullName)
    $topLevelArea = $relativePath.Split([System.IO.Path]::DirectorySeparatorChar)[0]
    [pscustomobject]@{
        SourcePath = $file.FullName
        SourceRelativePath = $relativePath
        Extension = $file.Extension.ToLowerInvariant()
        Length = $file.Length
        Area = $areaLabels[$topLevelArea]
        SizeBand = Get-SizeBand -Length $file.Length
    }
}

$requests = @(
    @{ Label = 'PDF-P01'; Extension = '.pdf'; Area = 'Area-01'; Band = 'under-256-kib' },
    @{ Label = 'PDF-P02'; Extension = '.pdf'; Area = 'Area-02'; Band = 'under-256-kib' },
    @{ Label = 'PDF-P03'; Extension = '.pdf'; Area = 'Area-02'; Band = '1-to-5-mib' },
    @{ Label = 'PDF-P04'; Extension = '.pdf'; Area = 'Area-02'; Band = '5-to-10-mib' },
    @{ Label = 'EML-E01'; Extension = '.eml'; Area = 'Area-01'; Band = 'under-256-kib' },
    @{ Label = 'EML-E02'; Extension = '.eml'; Area = 'Area-01'; Band = '256-kib-to-1-mib' },
    @{ Label = 'EML-E03'; Extension = '.eml'; Area = 'Area-01'; Band = '1-to-5-mib' },
    @{ Label = 'EML-E04'; Extension = '.eml'; Area = 'Area-02'; Band = $null },
    @{ Label = 'MSG-M01'; Extension = '.msg'; Area = 'Area-01'; Band = 'under-256-kib' },
    @{ Label = 'MSG-M02'; Extension = '.msg'; Area = 'Area-02'; Band = 'under-256-kib' },
    @{ Label = 'MSG-M03'; Extension = '.msg'; Area = 'Area-02'; Band = '1-to-5-mib' },
    @{ Label = 'MSG-M04'; Extension = '.msg'; Area = 'Area-02'; Band = '5-to-10-mib' }
)

$selectedHashes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$selections = foreach ($request in $requests) {
    $metadataMatches = @(
        $candidates |
            Where-Object {
                $_.Extension -eq $request.Extension -and
                $_.Area -eq $request.Area -and
                ($null -eq $request.Band -or $_.SizeBand -eq $request.Band)
            } |
            Sort-Object @{ Expression = 'Length'; Descending = $true }, SourceRelativePath |
            Select-Object -First 24
    )

    if ($metadataMatches.Count -eq 0) {
        throw "No candidate satisfies opaque selection $($request.Label)."
    }

    $scoredMatches = foreach ($match in $metadataMatches) {
        $hash = (Get-FileHash -LiteralPath $match.SourcePath -Algorithm SHA256).Hash
        if ($selectedHashes.Contains($hash)) {
            continue
        }

        $bytes = [System.IO.File]::ReadAllBytes($match.SourcePath)
        $markers = @(Get-FeatureMarkers -Extension $match.Extension -Bytes $bytes)
        [pscustomobject]@{
            SourcePath = $match.SourcePath
            SourceRelativePath = $match.SourceRelativePath
            Extension = $match.Extension
            Length = $match.Length
            Sha256 = $hash
            Area = $match.Area
            SizeBand = $match.SizeBand
            FeatureMarkers = $markers
            FeatureScore = $markers.Count
        }
    }

    $selection = @(
        $scoredMatches |
            Sort-Object @{ Expression = 'FeatureScore'; Descending = $true }, @{ Expression = 'Length'; Descending = $true }, Sha256
    )[0]

    if ($null -eq $selection) {
        throw "No unique candidate satisfies opaque selection $($request.Label)."
    }

    [void]$selectedHashes.Add($selection.Sha256)
    [pscustomobject]@{
        Label = $request.Label
        SourcePath = $selection.SourcePath
        SourceRelativePath = $selection.SourceRelativePath
        Extension = $selection.Extension
        Length = $selection.Length
        Sha256 = $selection.Sha256
        Area = $selection.Area
        SizeBand = $selection.SizeBand
        FeatureMarkers = $selection.FeatureMarkers
    }
}

[System.IO.Directory]::CreateDirectory($resolvedDestinationRoot) | Out-Null
foreach ($format in @('pdf', 'eml', 'msg')) {
    [System.IO.Directory]::CreateDirectory((Join-Path $resolvedDestinationRoot $format)) | Out-Null
}

$manifestEntries = foreach ($selection in $selections) {
    $formatDirectory = $selection.Extension.TrimStart('.')
    $destinationPath = Join-Path (Join-Path $resolvedDestinationRoot $formatDirectory) ($selection.Label + $selection.Extension)
    Copy-Item -LiteralPath $selection.SourcePath -Destination $destinationPath

    $copiedHash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
    if (-not $copiedHash.Equals($selection.Sha256, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Copied sample failed hash verification."
    }

    [ordered]@{
        label = $selection.Label
        format = $formatDirectory
        source_relative_path = $selection.SourceRelativePath
        source_length = $selection.Length
        sha256 = $selection.Sha256
        source_area = $selection.Area
        size_band = $selection.SizeBand
        feature_markers = @($selection.FeatureMarkers)
        destination_relative_path = [System.IO.Path]::GetRelativePath($resolvedDestinationRoot, $destinationPath)
    }
}

$manifest = [ordered]@{
    schema_version = 1
    created_utc = [DateTimeOffset]::UtcNow.ToString('O', [System.Globalization.CultureInfo]::InvariantCulture)
    source_root = $resolvedCorpusRoot
    maximum_positive_input_bytes = $maximumPositiveInputBytes
    selection_policy = 'unique-sha256; opaque area/size cohort; highest passive marker diversity; ordinal hash tie-break'
    entries = @($manifestEntries)
}

$manifestPath = Join-Path $resolvedDestinationRoot '_manifest.local.json'
$manifestJson = $manifest | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($manifestPath, $manifestJson + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))

Write-Output "Imported $($manifestEntries.Count) opaque samples."
Write-Output "PDF=$(@($manifestEntries | Where-Object format -eq 'pdf').Count) EML=$(@($manifestEntries | Where-Object format -eq 'eml').Count) MSG=$(@($manifestEntries | Where-Object format -eq 'msg').Count)"
