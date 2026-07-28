[CmdletBinding()]
param(
    [string] $AtlasPath = (Join-Path $PSScriptRoot '..\docs\architecture\doc-fib-atlas.v1.json'),
    [string] $SpecificationPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$atlas = Get-Content -Raw -LiteralPath $AtlasPath | ConvertFrom-Json

if ($atlas.schemaVersion -ne 'collisiondocnet-doc-fib-atlas/1') { throw 'Unexpected DOC FIB atlas schema version.' }
if ($atlas.specification.sha256 -ne '2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a') { throw 'Unexpected MS-DOC source hash.' }
if ($atlas.entries.Count -ne 183) { throw "Expected 183 entries; found $($atlas.entries.Count)." }

$expectedIntroductions = [ordered]@{
    FibRgFcLcb97 = 93
    FibRgFcLcb2000 = 15
    FibRgFcLcb2002 = 28
    FibRgFcLcb2003 = 28
    FibRgFcLcb2007 = 19
}
foreach ($structure in $expectedIntroductions.Keys) {
    $actual = @($atlas.entries | Where-Object structureIntroduced -eq $structure).Count
    if ($actual -ne $expectedIntroductions[$structure]) { throw "$structure expected $($expectedIntroductions[$structure]) entries; found $actual." }
}

for ($index = 0; $index -lt $atlas.entries.Count; $index++) {
    $entry = $atlas.entries[$index]
    if ($entry.ordinal -ne $index -or $entry.byteOffsetInFibRgFcLcb -ne ($index * 8)) { throw "Invalid ordinal/offset at entry $index." }
    foreach ($property in @('memberName', 'valueKind', 'owningStream', 'recordGrammar', 'recordSection', 'payloadRelevance', 'parserOwner', 'supportPolicy', 'unimplementedOutcome')) {
        if ([string]::IsNullOrWhiteSpace($entry.$property)) { throw "Entry $index has no $property." }
    }
    if ($entry.parserOwner -notmatch '^EXT-DOC-0(02|03|04|05|06|07|08|09|10|11|13)$') { throw "Entry $index has invalid owner $($entry.parserOwner)." }
}

$fileTime = $atlas.entries[87]
if ($fileTime.valueKind -ne 'FILETIME' -or $fileTime.memberName -ne 'LastSavedFileTime') { throw 'Entry 87 is not the required FILETIME exception.' }
$clx = $atlas.entries[33]
if ($clx.memberName -ne 'Clx' -or $clx.parserOwner -ne 'EXT-DOC-003') { throw 'Entry 33 is not the CLX descriptor.' }
foreach ($wordDocumentOrdinal in 77, 78, 79, 119) {
    if ($atlas.entries[$wordDocumentOrdinal].owningStream -ne 'WordDocument') { throw "Entry $wordDocumentOrdinal must be owned by WordDocument." }
}
$ignoredTableOrdinals = @(8,55,62,63,64,65,66,67,68,80,81,82,86,88,90,92,93,95,101,102,103,104,105,106,107,116,128,129,130,133,134,135,146,147,148,149,150,151,152,153,154,155,156,157,158,159,160,161,162,163)
foreach ($ignoredTableOrdinal in $ignoredTableOrdinals) {
    $entry = $atlas.entries[$ignoredTableOrdinal]
    if ($entry.supportPolicy -ne 'ValidateAndIgnore' -or $entry.owningStream -ne 'Table') {
        throw "Ignored/deprecated entry $ignoredTableOrdinal must retain its physical Table stream ownership."
    }
}
foreach ($styleOrdinal in 96, 99) {
    if ($atlas.entries[$styleOrdinal].parserOwner -ne 'EXT-DOC-006') { throw "Entry $styleOrdinal must be owned by EXT-DOC-006." }
}
foreach ($layoutOrdinal in 27, 28, 29, 30) {
    $entry = $atlas.entries[$layoutOrdinal]
    if ($entry.supportPolicy -ne 'ValidateAndIgnore' -or $entry.unimplementedOutcome -ne 'NoneAfterValidation') {
        throw "Out-of-scope printer/layout entry $layoutOrdinal must be validated without a completeness penalty."
    }
}
foreach ($propertyOrdinal in 12, 13) {
    if ($atlas.entries[$propertyOrdinal].parserOwner -ne 'EXT-DOC-005') { throw "Entry $propertyOrdinal must be owned by EXT-DOC-005." }
}

$canonicalLines = $atlas.entries | ForEach-Object {
    "$($_.ordinal)|$($_.memberName)|$($_.firstField)|$($_.secondField)|$($_.owningStream)|$($_.recordGrammar)|$($_.recordSection)|$($_.parserOwner)|$($_.supportPolicy)"
}
$canonicalBytes = [System.Text.Encoding]::UTF8.GetBytes(($canonicalLines -join "`n") + "`n")
$canonicalHash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($canonicalBytes)).ToLowerInvariant()
if ($canonicalHash -ne 'c0afee25a88147efe5d4acc599a4d9876893a152f797c50958bd5669d0baf75b') {
    throw "The independently reviewed DOC FIB descriptor sequence changed: $canonicalHash"
}

if (-not [string]::IsNullOrWhiteSpace($SpecificationPath)) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $resolvedSpecification = (Resolve-Path -LiteralPath $SpecificationPath).Path
    $sourceHash = (Get-FileHash -LiteralPath $resolvedSpecification -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($sourceHash -ne $atlas.specification.sha256) { throw "Pinned MS-DOC hash mismatch: $sourceHash" }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedSpecification)
    try {
        $entry = $archive.GetEntry('word/document.xml')
        if ($null -eq $entry) { throw 'Pinned MS-DOC publication has no document.xml.' }
        $reader = [System.IO.StreamReader]::new($entry.Open())
        try { [xml] $document = $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $archive.Dispose() }

    $namespaces = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
    $namespaces.AddNamespace('w', 'http://schemas.openxmlformats.org/wordprocessingml/2006/main')
    $paragraphs = $document.SelectNodes('//w:p', $namespaces)
    $sourceLines = [System.Collections.Generic.List[string]]::new()
    $sourceOrdinal = 0
    foreach ($band in @(@(2275,2477), @(2549,2578), @(2676,2742), @(2840,2901), @(2981,3018))) {
        $fields = [System.Collections.Generic.List[string]]::new()
        for ($paragraphIndex = $band[0]; $paragraphIndex -le $band[1]; $paragraphIndex++) {
            $text = (($paragraphs[$paragraphIndex].SelectNodes('.//w:t', $namespaces) | ForEach-Object { $_.InnerText }) -join '')
            if ($text -match '^([A-Za-z][A-Za-z0-9]+) \(4 bytes\):') { $fields.Add($Matches[1]) }
        }
        if (($fields.Count % 2) -ne 0) { throw "Odd source field count in paragraph band $($band[0])-$($band[1])." }
        for ($fieldIndex = 0; $fieldIndex -lt $fields.Count; $fieldIndex += 2) {
            $sourceLines.Add("$sourceOrdinal|$($fields[$fieldIndex])|$($fields[$fieldIndex + 1])")
            $sourceOrdinal++
        }
    }
    $sourceBytes = [System.Text.Encoding]::UTF8.GetBytes(($sourceLines -join "`n") + "`n")
    $sourceSequenceHash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($sourceBytes)).ToLowerInvariant()
    if ($sourceSequenceHash -ne 'a7494e994901be57ee06e602eed824d99ea50699b82ab8a89f790ad34938ae8f') {
        throw "Pinned MS-DOC FIB field sequence changed: $sourceSequenceHash"
    }
}

Write-Output "DOC FIB atlas verified: $($atlas.entries.Count) entries across $($atlas.versionLayouts.Count) layouts."
