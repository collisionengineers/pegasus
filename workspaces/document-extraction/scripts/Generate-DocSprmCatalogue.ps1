[CmdletBinding()]
param(
    [string] $SpecificationPath = (Join-Path $PSScriptRoot '../artifacts/research/doc/2026-07-24/specifications/MS-DOC-12.5-260217.docx'),
    [string] $OutputPath = (Join-Path $PSScriptRoot '../docs/architecture/doc-sprm-catalogue.v1.json'),
    [string] $ReviewedOwnershipPath = (Join-Path $PSScriptRoot '../docs/architecture/doc-sprm-catalogue.v1.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$expectedSourceHash = '2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a'
$resolvedSource = (Resolve-Path -LiteralPath $SpecificationPath).Path
$actualSourceHash = (Get-FileHash -LiteralPath $resolvedSource -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualSourceHash -ne $expectedSourceHash) { throw "MS-DOC source hash mismatch: $actualSourceHash" }

function Get-Sha256Text([string] $Text) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Get-OperandType([string] $Name, [string] $Description, [int] $Spra) {
    $exact = @{
        sprmCPlain = 'UInt8ZeroIgnored'; sprmCIss = 'UInt8SuperscriptSubscript';
        sprmCRgFtc0 = 'Int16FontIndex'; sprmCRgFtc1 = 'Int16FontIndex'; sprmCRgFtc2 = 'Int16FontIndex';
        sprmCCharScale = 'UInt16Percent1To600'; sprmCFtcBi = 'Int16FontIndex';
        sprmCIdctHint = 'UInt8LanguageFontHint'; sprmPIlfo = 'Int16ListIndex';
        sprmPDyaBefore = 'UInt16Twips0To0x7BC0'; sprmPDyaAfter = 'UInt16Twips0To0x7BC0';
        sprmPWr = 'UInt8TextFrameWrap'; sprmPWAlignFont = 'UInt16TextAlignment';
        sprmPHugePapx = 'UInt32DataOffset'; sprmPTtwo = 'UInt8TightWrap';
        sprmSNfcPgn = 'UInt8MSONFC'; sprmSDmPaperReq = 'UInt16PaperTieBreaker';
        sprmSNfcFtnRef = 'UInt16MSONFC'; sprmSNfcEdnRef = 'UInt16MSONFC'
    }
    if ($exact.ContainsKey($Name)) { return $exact[$Name] }
    $width = switch ($Spra) { { $_ -in 0, 1 } { 8 }; { $_ -in 2, 4, 5 } { 16 }; 3 { 32 }; 7 { 24 }; default { 0 } }
    if ($Description -match '(?i)\bunsigned\b') { return "UInt$width" }
    if ($Description -match '(?i)\bsigned\b') { return "Int$width" }
    if ($Description -match '(?i)\b(?:byte|8-bit integer)\b') { return 'UInt8' }
    if ($width -ne 0 -and $Description -match '(?i)\binteger\b') { return "Integer$width" }
    if ($Description -match '^(?:A|An) ([A-Z][A-Za-z0-9_]+)(?: value| that| which|,|\s)') { return $Matches[1] }
    throw "No reviewed operand grammar for $Name."
}

function Get-OperandValidator([string] $Name, [string] $OperandType, [string] $Framing) {
    switch ($Name) {
        sprmTDefTable { return 'ValidateTDefTableCbAndCellDescriptors' }
        sprmPChgTabs { return 'ValidatePChgTabsDeletedAndAddedArrays' }
        sprmCPicLocation { return 'ValidateSignedSelectorAndCharacterContext' }
        sprmPHugePapx { return 'ValidateDataOffsetPrcDataMinimum10AndPlacement' }
        sprmPTableProps { return 'ValidateDataOffsetPrcDataMinimum10' }
        sprmCPlain { return 'RequireZeroThenResetCharacterState' }
        default { return "Validate$OperandType`And$Framing" }
    }
}

$archive = [IO.Compression.ZipFile]::OpenRead($resolvedSource)
try {
    $entry = $archive.GetEntry('word/document.xml')
    if ($null -eq $entry) { throw 'The pinned MS-DOC publication has no word/document.xml part.' }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { [xml] $document = $reader.ReadToEnd() } finally { $reader.Dispose() }
}
finally { $archive.Dispose() }

$namespaces = [Xml.XmlNamespaceManager]::new($document.NameTable)
$namespaces.AddNamespace('w', 'http://schemas.openxmlformats.org/wordprocessingml/2006/main')
$paragraphs = $document.SelectNodes('//w:p', $namespaces)
$texts = @($paragraphs | ForEach-Object { (($_.SelectNodes('.//w:t', $namespaces) | ForEach-Object { $_.InnerText }) -join '') })
$starts = @(0..($texts.Count - 2) | Where-Object {
    $texts[$_] -cmatch '^sprm[A-Z][A-Za-z0-9_]*$' -and $texts[$_ + 1] -cmatch '^\(0x[0-9A-F]{4}\)$'
})
if ($starts.Count -ne 322) { throw "Expected 322 canonical SPRMs; found $($starts.Count)." }

$groupNames = @{ 1 = 'Paragraph'; 2 = 'Character'; 3 = 'Picture'; 4 = 'Section'; 5 = 'Table' }
$groupSections = @{ 1 = '2.6.2'; 2 = '2.6.1'; 3 = '2.6.5'; 4 = '2.6.4'; 5 = '2.6.3' }
$contexts = @{
    1 = @('PAPX', 'UPX-PAPX', 'Pcd.Prm-Paragraph')
    2 = @('CHPX', 'UPX-CHPX', 'Pcd.Prm-Character')
    3 = @('PICF')
    4 = @('SEPX')
    5 = @('TAPX', 'UPX-TAPX')
}
$reviewedCatalogue = Get-Content -Raw -LiteralPath $ReviewedOwnershipPath | ConvertFrom-Json
if ($reviewedCatalogue.specification.sha256 -ne $expectedSourceHash -or $reviewedCatalogue.entries.Count -ne 322) {
    throw 'The reviewed per-row ownership catalogue is absent or does not match the pinned MS-DOC source.'
}
$reviewedOwnership = @{}
foreach ($entry in $reviewedCatalogue.entries) {
    if ($reviewedOwnership.ContainsKey($entry.name)) { throw "Duplicate reviewed ownership for $($entry.name)." }
    $reviewedOwnership[$entry.name] = $entry
}

# These are review decisions, not name-pattern inference. They correct the original generated
# draft and remain explicit until the regenerated catalogue itself carries the reviewed value.
$reviewedRelevanceCorrections = @{
    sprmCFComplexScripts = 'TextCritical'; sprmCNeedFontFixup = 'TextCritical';
    sprmCIdctHint = 'TextCritical'; sprmCPbiIBullet = 'StructureCritical';
    sprmCPbiGrf = 'ImageCritical'; sprmTBrcLeftCv = 'RenderingOnly';
    sprmPFBiDi = 'StructureCritical'; sprmTFBiDi = 'StructureCritical';
    sprmTFBiDi90 = 'StructureCritical'; sprmSFBiDi = 'StructureCritical';
    sprmCDispFldRMark = 'PassiveControl'; sprmSBkc = 'StructureCritical';
    sprmSFTitlePage = 'StructureCritical'; sprmSFEndnote = 'StructureCritical';
    sprmSRncFtn = 'StructureCritical'; sprmSRncEdn = 'StructureCritical';
    sprmSNFtn = 'StructureCritical'; sprmSNEdn = 'StructureCritical';
    sprmSNfcFtnRef = 'TextCritical'; sprmSNfcEdnRef = 'TextCritical';
    sprmTDxaCol = 'StructureCritical'
}

$upxChpxExcluded = @(
    'sprmCFRMarkDel', 'sprmCFBiDi', 'sprmCFComplexScripts', 'sprmCFFldVanish',
    'sprmCFRMarkIns', 'sprmCFSpec', 'sprmCFData', 'sprmCFOle2', 'sprmCFWebHidden',
    'sprmCFObj', 'sprmCPicLocation', 'sprmCPropRMark', 'sprmCPropRMark90', 'sprmCWall',
    'sprmCIdslRMark', 'sprmCSymbol', 'sprmCIdctHint', 'sprmCHighlight', 'sprmCFSdtVanish',
    'sprmCNeedFontFixup', 'sprmCRsidText', 'sprmCRsidProp', 'sprmCRsidRMDel',
    'sprmCIbstRMark', 'sprmCDttmRMark', 'sprmCIbstRMarkDel', 'sprmCDttmRMarkDel',
    'sprmCFMathPr',
    'sprmCFSpecVanish', 'sprmCIstd', 'sprmCIstdPermute', 'sprmCPlain', 'sprmCMajority',
    'sprmCDispFldRMark', 'sprmCIdslRMarkDel', 'sprmCLbcCRJ', 'sprmCPbiIBullet',
    'sprmCPbiGrf', 'sprmCCnf'
)
$upxPapxExcluded = @(
    'sprmPFTtp', 'sprmPFInTable', 'sprmPItap', 'sprmPFInnerTableCell', 'sprmPIpgp',
    'sprmPWall', 'sprmPRsid', 'sprmPPropRMark', 'sprmPNumRM', 'sprmPFNumRMIns',
    'sprmPIstd', 'sprmPIstdPermute', 'sprmPIncLvl', 'sprmPNest80', 'sprmPChgTabs',
    'sprmPDcs', 'sprmPHugePapx', 'sprmPFInnerTtp', 'sprmPFOpenTch', 'sprmPNest',
    'sprmPFNoAllowOverlap', 'sprmPIstdListPermute', 'sprmPTableProps', 'sprmPTIstdInfo',
    'sprmPCnf'
)
$upxTapxExcluded = @(
    'sprmTWall', 'sprmTPropRMark', 'sprmTFBiDi', 'sprmTRsid', 'sprmTPc',
    'sprmTDxaAbs', 'sprmTDyaAbs', 'sprmTDxaFromText', 'sprmTDyaFromText',
    'sprmTDxaFromTextRight', 'sprmTDyaFromTextBottom', 'sprmTDxaGapHalf',
    'sprmTDyaRowHeight', 'sprmTTableWidth', 'sprmTFAutofit', 'sprmTTlp',
    'sprmTDxaLeft', 'sprmTDefTable', 'sprmTDefTableShd80', 'sprmTDefTableShd3rd',
    'sprmTDefTableShd', 'sprmTDefTableShd2nd', 'sprmTWidthAfter', 'sprmTFKeepFollow',
    'sprmTBrcTopCv', 'sprmTBrcLeftCv', 'sprmTBrcBottomCv', 'sprmTBrcRightCv',
    'sprmTSetBrc80', 'sprmTInsert', 'sprmTDelete', 'sprmTDxaCol', 'sprmTMerge',
    'sprmTSplit', 'sprmTTextFlow', 'sprmTVertMerge', 'sprmTVertAlign', 'sprmTSetBrc',
    'sprmTCellPadding', 'sprmTCellWidth', 'sprmTFitText', 'sprmTFCellNoWrap',
    'sprmTCellFHideMark', 'sprmTSetShdTable', 'sprmTCellBrcType', 'sprmTFBiDi90',
    'sprmTFNoAllowOverlap', 'sprmTIpgp', 'sprmTDefTableShdRaw',
    'sprmTDefTableShdRaw2nd', 'sprmTDefTableShdRaw3rd',
    'sprmTCellBrcTopStyle', 'sprmTCellBrcBottomStyle', 'sprmTCellBrcLeftStyle',
    'sprmTCellBrcRightStyle', 'sprmTCellBrcInsideHStyle', 'sprmTCellBrcInsideVStyle'
)

function Get-LegalPropertyArrays([string] $Name, [int] $Group) {
    $legal = [Collections.Generic.List[string]]::new()
    foreach ($context in $contexts[$Group]) { $legal.Add($context) }
    if ($Name -in $upxChpxExcluded) { [void] $legal.Remove('UPX-CHPX') }
    if ($Name -in $upxPapxExcluded) { [void] $legal.Remove('UPX-PAPX') }
    if ($Name -in $upxTapxExcluded) { [void] $legal.Remove('UPX-TAPX') }
    return ,$legal.ToArray()
}

function Get-MutationFamily([string] $Name, [string] $Relevance) {
    switch ($Name) {
        sprmCPlain { return 'ResetCharacterToParagraphPreservingExceptions' }
        sprmCMajority { return 'SelectiveCharacterResetWhenDefaultStyle' }
        sprmCIstd { return 'SelectCharacterStyle' }
        sprmCIstdPermute { return 'PermuteCharacterStyleIfMapped' }
        sprmPIstd { return 'SelectParagraphStyle' }
        sprmPIstdPermute { return 'PermuteParagraphStyleIfMapped' }
        sprmPIncLvl { return 'AdjustParagraphStyleOrOutlineLevel' }
        sprmPChgTabsPapx { return 'ReplaceParagraphTabs' }
        sprmPChgTabs { return 'DeleteAndAddParagraphTabs' }
        sprmPHugePapx { return 'DereferencePrcDataAndTerminateArray' }
        sprmPTableProps { return 'DereferencePrcDataAndTerminateArray' }
        sprmCPicLocation { return 'ResolvePictureBinaryOrOleSelector' }
        sprmTInsert { return 'InsertTableCells' }
        sprmTDelete { return 'DeleteTableCells' }
        sprmTMerge { return 'MergeTableCells' }
        sprmTSplit { return 'SplitTableCells' }
        sprmTVertMerge { return 'SetVerticalMergeState' }
        sprmPBrcBar80 { return 'NoOpValidateOnly' }
        sprmPBrcBar { return 'NoOpValidateOnly' }
        sprmPIstdListPermute { return 'NoOpValidateOnly' }
        sprmPTIstdInfo { return 'NoOpValidateOnly' }
    }
    $family = switch ($Relevance) {
        'RenderingOnly' { 'ValidateRenderingOnly' }
        'PassiveControl' { 'RetainPassiveEvidence' }
        'ImageCritical' { 'SetImageDiscriminator' }
        'TextCritical' { 'SetTextStateLastApplicableWins' }
        'StructureCritical' { 'SetStructureStateLastApplicableWins' }
        default { throw "Unknown reviewed relevance $Relevance for $Name." }
    }
    return $family
}

function Get-ApplicationCondition([string] $Name) {
    switch ($Name) {
        sprmCIstdPermute { return 'ApplyOnlyWhenCurrentIstdIsMapped' }
        sprmPIstdPermute { return 'ApplyOnlyWhenCurrentIstdIsMapped' }
        sprmCMajority { return 'ApplyOnlyWhenCharacterIstdEquals10' }
        sprmPIlvl { return 'IgnoreWhenParagraphIsNotInList' }
        sprmPOutLvl { return 'IgnoreForBuiltInHeadingIstd1Through9' }
        sprmPHugePapx { return 'FirstInArrayOrIgnore_OnlyPrlAndIstdZeroInGrpPrlAndIstd' }
        sprmTTableHeader { return 'ApplyOnlyToLeadingContiguousHeaderRows' }
        sprmTIstd { return 'IgnoreInsideUpxTapxOtherwiseSelectTableStyle' }
        sprmTWidthBefore { return 'UpxTapxOnlyForIstd0x000BWithFtsDxaWidthZero' }
        sprmSPgnStart97 { return 'IgnoreUnlessPageNumberRestartEnabled' }
        sprmSPgnStart { return 'IgnoreUnlessPageNumberRestartEnabled' }
        sprmSNFtn { return 'IgnoreUnlessContinuousFootnoteNumbering' }
        sprmSNEdn { return 'IgnoreUnlessContinuousEndnoteNumbering' }
        default { return 'ApplyWhenPresentInLegalArray' }
    }
}
$conditionalTableShading = @('sprmTDefTableShd80', 'sprmTDefTableShd3rd', 'sprmTDefTableShd', 'sprmTDefTableShd2nd', 'sprmTSetShd', 'sprmTSetShdOdd')
$supportedVersions = @('0x00C1', '0x00D9', '0x0101', '0x010C', '0x0112')
$catalogue = [Collections.Generic.List[object]]::new()

for ($index = 0; $index -lt $starts.Count; $index++) {
    $start = $starts[$index]
    $end = if ($index + 1 -lt $starts.Count) { $starts[$index + 1] } else { $start + 4 }
    $name = $texts[$start]
    $opcodeText = $texts[$start + 1].Trim('(', ')')
    $opcode = [Convert]::ToUInt16($opcodeText.Substring(2), 16)
    $ispmd = $opcode -band 0x01ff
    $fSpec = ($opcode -shr 9) -band 1
    $sgc = ($opcode -shr 10) -band 7
    $spra = ($opcode -shr 13) -band 7
    if (-not $groupNames.ContainsKey($sgc)) { throw "$name has unsupported sgc $sgc." }
    if (-not $reviewedOwnership.ContainsKey($name)) { throw "No explicit reviewed ownership exists for $name." }
    $reviewed = $reviewedOwnership[$name]
    if ($reviewed.opcode -ne $opcodeText) { throw "Reviewed opcode mismatch for $name." }
    $description = (($texts[($start + 3)..($end - 1)] | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ' ').Trim()
    $relevance = if ($reviewedRelevanceCorrections.ContainsKey($name)) {
        $reviewedRelevanceCorrections[$name]
    } else {
        $reviewed.relevance
    }
    $framing = switch ($opcodeText) {
        '0xD608' { 'UInt16Cb_TotalOperandBytesEqualsCbPlus1' }
        '0xC615' { 'ByteCb_Or_FFDeletedAddedTabFormula' }
        default {
            switch ($spra) {
                { $_ -in 0, 1 } { 'Fixed1' }
                { $_ -in 2, 4, 5 } { 'Fixed2' }
                3 { 'Fixed4' }
                6 { 'ByteCb_ThenCbBytes' }
                7 { 'Fixed3' }
            }
        }
    }
    $policy = switch ($relevance) {
        'RenderingOnly' { 'ValidateAndIgnoreForTextImagePayload' }
        'PassiveControl' { 'RetainPassiveControlEvidence' }
        default { 'ApplyToEffectiveState' }
    }
    $dataTarget = switch ($opcodeText) {
        '0x6A03' { 'PictureOrBinaryOrOleSelector' }
        '0x6646' { 'PrcDataHugePapx' }
        '0x646B' { 'PrcDataTableProperties' }
        default { 'None' }
    }
    $applicabilityRule = if ($name -in $conditionalTableShading) {
        'IgnoreWhenNFibGreaterThan0x00D9AndTableStylesAreUnderstood'
    } else {
        'AppliesAcrossSupportedNFibSubjectToApplicationCondition'
    }
    $operandType = Get-OperandType -Name $name -Description $description -Spra $spra
    $mutationFamily = Get-MutationFamily -Name $name -Relevance $relevance

    $catalogue.Add([ordered]@{
        ordinal = $index
        name = $name
        opcode = $opcodeText
        ispmd = $ispmd
        fSpec = $fSpec
        sgc = $sgc
        group = $groupNames[$sgc]
        spra = $spra
        operandFraming = $framing
        operandType = $operandType
        operandValidator = Get-OperandValidator -Name $name -OperandType $operandType -Framing $framing
        validPropertyArrays = Get-LegalPropertyArrays -Name $name -Group $sgc
        supportedNFib = $supportedVersions
        applicabilityRule = $applicabilityRule
        applicationCondition = Get-ApplicationCondition -Name $name
        relevance = $relevance
        supportPolicy = $policy
        mutationFamily = $mutationFamily
        stateKey = "$($groupNames[$sgc]).$name"
        stateTransition = "${mutationFamily}:$($groupNames[$sgc]).$name"
        dataStreamTarget = $dataTarget
        specificationSection = $groupSections[$sgc]
        sourceParagraphStart = $start
        sourceParagraphEndExclusive = $end
        definitionTextSha256 = Get-Sha256Text $description
    })
}

$canonicalLines = @($catalogue | ForEach-Object {
    "$($_.ordinal)|$($_.name)|$($_.opcode)|$($_.ispmd)|$($_.fSpec)|$($_.sgc)|$($_.spra)|$($_.operandFraming)|$($_.operandType)|$($_.operandValidator)|$($_.applicabilityRule)|$($_.applicationCondition)|$($_.relevance)|$($_.supportPolicy)|$($_.mutationFamily)|$($_.stateKey)|$($_.validPropertyArrays -join ',')|$($_.dataStreamTarget)|$($_.definitionTextSha256)"
})
$canonicalHash = Get-Sha256Text (($canonicalLines -join "`n") + "`n")

$result = [ordered]@{
    schemaVersion = 'collisiondocnet-doc-sprm-catalogue/1'
    owner = 'EXT-DOC-005'
    evidenceLabel = 'Mapped'
    specification = [ordered]@{
        name = 'MS-DOC'
        revision = '12.5'
        published = '2026-02-17'
        sha256 = $expectedSourceHash
        sections = @('2.2.5', '2.4.6', '2.6.1', '2.6.2', '2.6.3', '2.6.4', '2.6.5')
    }
    supportedNFib = $supportedVersions
    canonicalSha256 = $canonicalHash
    counts = [ordered]@{
        total = $catalogue.Count
        byGroup = [ordered]@{ Paragraph = 91; Character = 84; Picture = 8; Section = 59; Table = 80 }
        bySpra = [ordered]@{ '0' = 25; '1' = 80; '2' = 59; '3' = 41; '4' = 26; '5' = 9; '6' = 75; '7' = 7 }
    }
    applicationOrder = [ordered]@{
        paragraph = @('SpecificationDefaults', 'DocumentAndStylesheetDefaults', 'TableStyleBase', 'TableConditionalFormatting', 'BaseParagraphStylesParentFirst', 'CurrentParagraphStyle', 'Papx', 'PiecePrmParagraphGroup', 'ListDerivedParagraphProperties')
        character = @('StylesheetFontDefaults', 'TableStyleCharacterProperties', 'TableConditionalCharacterFormatting', 'ParagraphDerivedCharacterStyleParentFirst', 'CurrentCharacterStyle', 'Chpx', 'PiecePrmCharacterGroup')
        section = @('SectionDefaults', 'Sepx')
    }
    dataIndirection = [ordered]@{
        opcodes = @('0x6A03', '0x6646', '0x646B')
        visitedOffsetsRequired = $true
        cycles = 'Corrupt'
        configuredDepthCountOrByteBound = 'ResourceLimitExceeded'
    }
    entries = $catalogue
}

$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $resolvedOutput
if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw "Output parent does not exist: $parent" }
$result | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $resolvedOutput -Encoding utf8NoBOM
Write-Output $resolvedOutput
