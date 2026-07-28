[CmdletBinding()]
param(
    [string] $SpecificationPath = (Join-Path $PSScriptRoot '..\artifacts\research\doc\2026-07-24\specifications\MS-DOC-12.5-260217.docx'),
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\docs\architecture\doc-fib-atlas.v1.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$expectedSourceHash = '2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a'
$resolvedSource = (Resolve-Path -LiteralPath $SpecificationPath).Path
$actualSourceHash = (Get-FileHash -LiteralPath $resolvedSource -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualSourceHash -ne $expectedSourceHash) {
    throw "MS-DOC source hash mismatch: $actualSourceHash"
}

function Get-EntryDisposition {
    param([string] $Name, [string] $Description)

    if ($Name -match '^PlcfTxbx(?:Hdr)?Bkd$') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-004'; Relevance = 'Text'; Policy = 'RequiredSemanticExtraction'; Risk = 'None' }
    }
    if ($Name -match '^(FactoidData|ODSO|CustomXForm)$') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-010'; Relevance = 'ControlEvidence'; Policy = 'PassiveInspectOrUnsupported'; Risk = 'ExternalReference' }
    }
    if ($Name -match '^(SttbRgtplc|RgDofr)$') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-006'; Relevance = 'TextSemantics'; Policy = 'RequiredSemanticExtraction'; Risk = 'None' }
    }
    if ($Name -match '^(PrDrvr|PrEnvPort|PrEnvLand|Wss)$') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-002'; Relevance = 'ControlEvidence'; Policy = 'ValidateAndIgnore'; Risk = 'None' }
    }
    if ($Name -match '^(PlcfPhe|CookieData|RgbUse|Usp|Uskf|PlcupcRgbUse|PlcupcUsp|PlcfTch|Mid|Plcfcookie|PlcfSpl|PlcfGram|PlcfBklArto)$' -or
        $Name -match 'Unused|Old|Pgd|Bkd|Afd|Lvc|pmi|Ussr|Undo|StshfOrig' -or
        $Description -match '^(This value (is undefined|MUST be 0|MUST be zero)|Undefined)') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-002'; Relevance = 'None'; Policy = 'ValidateAndIgnore'; Risk = 'None' }
    }
    if ($Name -eq 'Clx') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-003'; Relevance = 'Text'; Policy = 'RequiredSemanticExtraction'; Risk = 'None' }
    }
    if ($Name -match 'fnd|end|and|Atn|RMark|MoveFrom|MoveTo|mthd') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-008'; Relevance = 'Text'; Policy = 'RequiredSemanticExtraction'; Risk = 'None' }
    }
    if ($Name -match 'Fld|Bkmk|Bkf|Bkl|Form|Prot|Sdt|Factoid|ODSO') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-007'; Relevance = 'Text'; Policy = 'RequiredSemanticExtraction'; Risk = 'ExternalReference' }
    }
    if ($Name -match 'Hdd|Txbx|Glsy') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-004'; Relevance = 'Text'; Policy = 'RequiredSemanticExtraction'; Risk = 'None' }
    }
    if ($Name -match 'Dgg|Spa|Ocx|Arto|Theme|ColorScheme') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-009'; Relevance = 'ImageOrText'; Policy = 'RequiredSemanticExtraction'; Risk = 'EmbeddedObject' }
    }
    if ($Name -match 'BteChpx|BtePapx') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-005'; Relevance = 'TextSemantics'; Policy = 'RequiredSemanticExtraction'; Risk = 'None' }
    }
    if ($Name -match 'Stshf|Ffn|Sed|Phe|PlfLst|PlfLfo|ListNames|Tch') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-006'; Relevance = 'TextSemantics'; Policy = 'RequiredSemanticExtraction'; Risk = 'None' }
    }
    if ($Name -match 'Cmds|MsoEnvelope|Plcosl|Plcocx|CustomXForm|Pms|RouteSlip|PrDrvr|PrEnv|Wss|StwUser|Usp|Uskf|Plcupc|RmdThreading|RgDofr') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-010'; Relevance = 'PossibleNestedTextOrImage'; Policy = 'PassiveInspectOrUnsupported'; Risk = 'ActiveOrExternalContent' }
    }
    if ($Name -match 'Dop|Assoc|AtnOwners|SavedBy|Fnm|Cookie|Gram|Spl|Asumy|IntlFld|Sttb|Plgosl|PlcfPgp|Plcfuim|Plfguid|Plrsid|AtrdExtra') {
        return [pscustomobject]@{ Owner = 'EXT-DOC-011'; Relevance = 'ControlEvidence'; Policy = 'SemanticControlEvidence'; Risk = 'None' }
    }

    return [pscustomobject]@{ Owner = 'EXT-DOC-013'; Relevance = 'Unknown'; Policy = 'UnsupportedFeatureIfPresent'; Risk = 'Unknown' }
}

function Get-RecordGrammar {
    param([string] $Name, [string] $Description, [string] $Policy)

    if ($Policy -eq 'ValidateAndIgnore') { return 'None' }
    if ($Name -eq 'Clx') { return 'Clx' }
    if ($Name -eq 'Stshf') { return 'STSH' }
    if ($Name -eq 'Cmds') { return 'Tcg' }
    if ($Name -eq 'Dop') { return 'Dop' }
    if ($Name -eq 'PlfLst') { return 'PlfLst' }
    if ($Name -eq 'DggInfo') { return 'OfficeArtContent' }
    if ($Name -eq 'ODSO') { return 'ODSOPropertyBase[]' }
    if ($Name -eq 'RgDofr') { return 'Dofrh[]' }
    if ($Name -eq 'PmsNew') { return 'Pms' }
    if ($Name -eq 'CustomXForm') { return 'UTF16CodeUnit[]' }
    if ($Name -eq 'GrpXstAtnOwners') { return 'XST[]' }
    if ($Description -cmatch '\b[Aa]n array of ([A-Z][A-Za-z0-9_]+)') { return "$($Matches[1])[]" }
    if ($Description -cmatch '\b(?:A|An|The) ([A-Z][A-Za-z0-9_]+)(?: structure)?\b') { return $Matches[1] }
    return $Name
}

$bands = @(
    [pscustomobject]@{ Structure = 'FibRgFcLcb97'; Section = '2.5.6'; NFib = '0x00C1'; Start = 2275; End = 2477; ExpectedPairs = 93 },
    [pscustomobject]@{ Structure = 'FibRgFcLcb2000'; Section = '2.5.7'; NFib = '0x00D9'; Start = 2549; End = 2578; ExpectedPairs = 15 },
    [pscustomobject]@{ Structure = 'FibRgFcLcb2002'; Section = '2.5.8'; NFib = '0x0101'; Start = 2676; End = 2742; ExpectedPairs = 28 },
    [pscustomobject]@{ Structure = 'FibRgFcLcb2003'; Section = '2.5.9'; NFib = '0x010C'; Start = 2840; End = 2901; ExpectedPairs = 28 },
    [pscustomobject]@{ Structure = 'FibRgFcLcb2007'; Section = '2.5.10'; NFib = '0x0112'; Start = 2981; End = 3018; ExpectedPairs = 19 }
)

$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedSource)
try {
    $documentEntry = $archive.GetEntry('word/document.xml')
    if ($null -eq $documentEntry) { throw 'The pinned MS-DOC publication has no word/document.xml part.' }
    $reader = [System.IO.StreamReader]::new($documentEntry.Open())
    try { [xml] $document = $reader.ReadToEnd() } finally { $reader.Dispose() }
}
finally {
    $archive.Dispose()
}

$namespaces = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
$namespaces.AddNamespace('w', 'http://schemas.openxmlformats.org/wordprocessingml/2006/main')
$paragraphs = $document.SelectNodes('//w:p', $namespaces)
$sectionByGrammar = @{}
for ($paragraphIndex = 0; $paragraphIndex -lt [Math]::Min(950, $paragraphs.Count); $paragraphIndex++) {
    $tocLine = (($paragraphs[$paragraphIndex].SelectNodes('.//w:t', $namespaces) | ForEach-Object { $_.InnerText }) -join '')
    if ($tocLine -cmatch '^(2\.(?:2|7|8|9)(?:\.\d+)+)([A-Za-z_][A-Za-z0-9_]*?)(\d{2,3})$' -and -not $sectionByGrammar.ContainsKey($Matches[2])) {
        $sectionByGrammar[$Matches[2]] = $Matches[1]
    }
}
$entries = [System.Collections.Generic.List[object]]::new()
$ordinal = 0

foreach ($band in $bands) {
    $fields = [System.Collections.Generic.List[object]]::new()
    for ($paragraphIndex = $band.Start; $paragraphIndex -le $band.End; $paragraphIndex++) {
        $paragraph = $paragraphs[$paragraphIndex]
        $text = (($paragraph.SelectNodes('.//w:t', $namespaces) | ForEach-Object { $_.InnerText }) -join '')
        if ($text -match '^([A-Za-z][A-Za-z0-9]+) \(4 bytes\):\s*(.*)$') {
            $fields.Add([pscustomobject]@{ Name = $Matches[1]; Description = $Matches[2]; Paragraph = $paragraphIndex })
        }
    }

    if ($fields.Count -ne ($band.ExpectedPairs * 2)) {
        throw "$($band.Structure) yielded $($fields.Count) fields; expected $($band.ExpectedPairs * 2)."
    }

    for ($fieldIndex = 0; $fieldIndex -lt $fields.Count; $fieldIndex += 2) {
        $first = $fields[$fieldIndex]
        $second = $fields[$fieldIndex + 1]
        $isFileTime = $first.Name -eq 'dwLowDateTime' -and $second.Name -eq 'dwHighDateTime'
        if (-not $isFileTime -and (-not $first.Name.StartsWith('fc') -or -not $second.Name.StartsWith('lcb'))) {
            throw "Unexpected field pair $($first.Name)/$($second.Name) in $($band.Structure)."
        }

        $memberName = if ($isFileTime) { 'LastSavedFileTime' } else { $first.Name.Substring(2) }
        $disposition = if ($isFileTime) {
            [pscustomobject]@{ Owner = 'EXT-DOC-011'; Relevance = 'ControlEvidence'; Policy = 'SemanticControlEvidence'; Risk = 'None' }
        } else {
            Get-EntryDisposition -Name $memberName -Description ($first.Description + ' ' + $second.Description)
        }
        $combinedDescription = $first.Description + ' ' + $second.Description
        $grammar = if ($isFileTime) { 'FILETIME' } else { Get-RecordGrammar -Name $memberName -Description $combinedDescription -Policy $disposition.Policy }
        $grammarLookup = $grammar.TrimEnd('[]')
        $recordSection = if ($isFileTime) {
            '[MS-DTYP] 2.3.3'
        } elseif ($memberName -eq 'MsoEnvelope') {
            '[MS-OSHARED] 2.3.8.1'
        } elseif ($sectionByGrammar.ContainsKey($grammarLookup)) {
            "[MS-DOC] $($sectionByGrammar[$grammarLookup])"
        } else {
            "[MS-DOC] $($band.Section), member $($first.Name)/$($second.Name)"
        }
        # Physical storage ownership is independent of semantic support policy. In
        # particular, deprecated caches can be ignored only after their Table-stream
        # ranges have been bounded and validated.
        $owningStream = if ($isFileTime) {
            'FIB'
        } elseif ($ordinal -in 77, 78, 79, 119) {
            'WordDocument'
        } elseif ($combinedDescription -match '(?i)offset[^.]*\bTable Stream\b') {
            'Table'
        } else {
            'None'
        }

        $entries.Add([ordered]@{
            ordinal = $ordinal
            byteOffsetInFibRgFcLcb = $ordinal * 8
            structureIntroduced = $band.Structure
            minimumNFib = $band.NFib
            specificationSection = $band.Section
            memberName = $memberName
            valueKind = if ($isFileTime) { 'FILETIME' } else { 'FcLcb' }
            firstField = $first.Name
            secondField = $second.Name
            owningStream = $owningStream
            recordGrammar = $grammar
            recordSection = $recordSection
            payloadRelevance = $disposition.Relevance
            activeContentRisk = $disposition.Risk
            parserOwner = $disposition.Owner
            supportPolicy = $disposition.Policy
            unimplementedOutcome = if ($disposition.Policy -eq 'ValidateAndIgnore') { 'NoneAfterValidation' } else { 'UnsupportedFeature' }
            sourceParagraphs = @($first.Paragraph, $second.Paragraph)
        })
        $ordinal++
    }
}

if ($entries.Count -ne 183) { throw "Generated $($entries.Count) FibRgFcLcb entries; expected 183." }

$atlas = [ordered]@{
    schemaVersion = 'collisiondocnet-doc-fib-atlas/1'
    owner = 'EXT-DOC-002'
    evidenceLabel = 'Mapped'
    specification = [ordered]@{
        name = 'MS-DOC'
        revision = '12.5'
        published = '2026-02-17'
        sha256 = $expectedSourceHash
        sections = @('2.5.1', '2.5.2', '2.5.3', '2.5.4', '2.5.5', '2.5.6', '2.5.7', '2.5.8', '2.5.9', '2.5.10', '2.5.11', '2.5.12', '2.5.13', '2.5.14', '2.5.15')
    }
    versionLayouts = @(
        [ordered]@{ effectiveNFib = '0x00C1'; cbRgFcLcb = 93; totalBytes = 744; structure = 'FibRgFcLcb97' },
        [ordered]@{ effectiveNFib = '0x00D9'; cbRgFcLcb = 108; totalBytes = 864; structure = 'FibRgFcLcb2000' },
        [ordered]@{ effectiveNFib = '0x0101'; cbRgFcLcb = 136; totalBytes = 1088; structure = 'FibRgFcLcb2002' },
        [ordered]@{ effectiveNFib = '0x010C'; cbRgFcLcb = 164; totalBytes = 1312; structure = 'FibRgFcLcb2003' },
        [ordered]@{ effectiveNFib = '0x0112'; cbRgFcLcb = 183; totalBytes = 1464; structure = 'FibRgFcLcb2007' }
    )
    entries = $entries
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $resolvedOutput
if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw "Output parent does not exist: $parent" }
$atlas | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutput -Encoding utf8NoBOM
Write-Output $resolvedOutput
