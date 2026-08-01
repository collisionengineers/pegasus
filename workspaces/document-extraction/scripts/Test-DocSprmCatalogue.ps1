[CmdletBinding()]
param(
    [string] $CataloguePath = (Join-Path $PSScriptRoot '../docs/architecture/doc-sprm-catalogue.v1.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$catalogue = Get-Content -Raw -LiteralPath $CataloguePath | ConvertFrom-Json

if ($catalogue.schemaVersion -ne 'collisiondocnet-doc-sprm-catalogue/1') { throw 'Unexpected DOC SPRM catalogue schema.' }
if ($catalogue.owner -ne 'EXT-DOC-005') { throw 'Unexpected DOC SPRM catalogue owner.' }
if ($catalogue.specification.sha256 -ne '2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a') { throw 'Unexpected MS-DOC source hash.' }
if ($catalogue.entries.Count -ne 322) { throw 'The catalogue must contain exactly 322 named SPRMs.' }

$expectedGroups = @{ Paragraph = 91; Character = 84; Picture = 8; Section = 59; Table = 80 }
foreach ($group in $expectedGroups.Keys) {
    $count = @($catalogue.entries | Where-Object group -eq $group).Count
    if ($count -ne $expectedGroups[$group]) { throw "$group SPRM count changed: $count" }
}
$expectedSpra = @{ '0' = 25; '1' = 80; '2' = 59; '3' = 41; '4' = 26; '5' = 9; '6' = 75; '7' = 7 }
foreach ($spra in $expectedSpra.Keys) {
    $count = @($catalogue.entries | Where-Object spra -eq ([int] $spra)).Count
    if ($count -ne $expectedSpra[$spra]) { throw "spra=$spra count changed: $count" }
}

$names = @($catalogue.entries | ForEach-Object name)
$opcodes = @($catalogue.entries | ForEach-Object opcode)
if (($names | Sort-Object -Unique).Count -ne 322) { throw 'SPRM names are not unique.' }
if (($opcodes | Sort-Object -Unique).Count -ne 322) { throw 'SPRM opcodes are not unique.' }
foreach ($entry in $catalogue.entries) {
    $opcode = [Convert]::ToUInt16($entry.opcode.Substring(2), 16)
    if (($opcode -band 0x01ff) -ne $entry.ispmd -or (($opcode -shr 9) -band 1) -ne $entry.fSpec -or
        (($opcode -shr 10) -band 7) -ne $entry.sgc -or (($opcode -shr 13) -band 7) -ne $entry.spra) {
        throw "$($entry.name) does not round-trip its opcode fields."
    }
    if ([string]::IsNullOrWhiteSpace($entry.operandFraming) -or [string]::IsNullOrWhiteSpace($entry.operandType) -or
        [string]::IsNullOrWhiteSpace($entry.operandValidator) -or
        [string]::IsNullOrWhiteSpace($entry.applicationCondition) -or
        [string]::IsNullOrWhiteSpace($entry.relevance) -or [string]::IsNullOrWhiteSpace($entry.supportPolicy) -or
        [string]::IsNullOrWhiteSpace($entry.mutationFamily) -or [string]::IsNullOrWhiteSpace($entry.stateKey) -or
        $entry.validPropertyArrays.Count -eq 0 -or $entry.supportedNFib.Count -ne 5) {
        throw "$($entry.name) has incomplete ownership."
    }
    if ($entry.operandType -match '^(?:Spra\dOperand|signed|unsigned|integer|byte)$' -or
        $entry.stateTransition -match '^Apply:') {
        throw "$($entry.name) retained placeholder grammar or transition ownership."
    }
}

$expectedRelevance = @{ ImageCritical = 5; PassiveControl = 16; RenderingOnly = 182; StructureCritical = 87; TextCritical = 32 }
foreach ($relevance in $expectedRelevance.Keys) {
    $count = @($catalogue.entries | Where-Object relevance -eq $relevance).Count
    if ($count -ne $expectedRelevance[$relevance]) { throw "$relevance ownership changed: $count" }
}

foreach ($name in @('sprmCFSpecVanish', 'sprmCIstd', 'sprmCIstdPermute', 'sprmCPlain', 'sprmCMajority', 'sprmCDispFldRMark', 'sprmCIdslRMarkDel', 'sprmCLbcCRJ', 'sprmCPbiIBullet', 'sprmCPbiGrf', 'sprmCCnf')) {
    if (($catalogue.entries | Where-Object name -eq $name).validPropertyArrays -contains 'UPX-CHPX') { throw "$name is illegally owned by UPX-CHPX." }
}
foreach ($name in @('sprmPIstd', 'sprmPIstdPermute', 'sprmPIncLvl', 'sprmPNest80', 'sprmPChgTabs', 'sprmPDcs', 'sprmPHugePapx', 'sprmPFInnerTtp', 'sprmPFOpenTch', 'sprmPNest', 'sprmPFNoAllowOverlap', 'sprmPIstdListPermute', 'sprmPTableProps', 'sprmPTIstdInfo', 'sprmPCnf')) {
    if (($catalogue.entries | Where-Object name -eq $name).validPropertyArrays -contains 'UPX-PAPX') { throw "$name is illegally owned by UPX-PAPX." }
}

$expectedParagraphOrder = 'SpecificationDefaults|DocumentAndStylesheetDefaults|TableStyleBase|TableConditionalFormatting|BaseParagraphStylesParentFirst|CurrentParagraphStyle|Papx|PiecePrmParagraphGroup|ListDerivedParagraphProperties'
$expectedCharacterOrder = 'StylesheetFontDefaults|TableStyleCharacterProperties|TableConditionalCharacterFormatting|ParagraphDerivedCharacterStyleParentFirst|CurrentCharacterStyle|Chpx|PiecePrmCharacterGroup'
if (($catalogue.applicationOrder.paragraph -join '|') -ne $expectedParagraphOrder -or
    ($catalogue.applicationOrder.character -join '|') -ne $expectedCharacterOrder) {
    throw 'MS-DOC 2.4.6 effective-state order changed.'
}

$exceptions = @($catalogue.entries | Where-Object opcode -in @('0xD608', '0xC615'))
if ($exceptions.Count -ne 2 -or $exceptions[0].operandFraming -eq $exceptions[1].operandFraming) { throw 'Variable-length exception ownership changed.' }
$dataOpcodes = @($catalogue.entries | Where-Object dataStreamTarget -ne 'None' | ForEach-Object opcode)
if (($dataOpcodes -join '|') -ne '0x6A03|0x6646|0x646B') { throw 'Direct Data-stream SPRM ownership changed.' }

$canonicalLines = @($catalogue.entries | ForEach-Object {
    "$($_.ordinal)|$($_.name)|$($_.opcode)|$($_.ispmd)|$($_.fSpec)|$($_.sgc)|$($_.spra)|$($_.operandFraming)|$($_.operandType)|$($_.operandValidator)|$($_.applicabilityRule)|$($_.applicationCondition)|$($_.relevance)|$($_.supportPolicy)|$($_.mutationFamily)|$($_.stateKey)|$($_.validPropertyArrays -join ',')|$($_.dataStreamTarget)|$($_.definitionTextSha256)"
})
$bytes = [Text.Encoding]::UTF8.GetBytes(($canonicalLines -join "`n") + "`n")
$hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
if ($hash -ne 'f34f1b1c4a003edd7ac89d77dd0afa979de13ddfc35c7fdcaebbf73952dd2dee' -or $catalogue.canonicalSha256 -ne $hash) {
    throw "The reviewed DOC SPRM catalogue changed: $hash"
}

Write-Output "DOC SPRM catalogue verified: $($catalogue.entries.Count) entries across $($expectedGroups.Count) groups and $($expectedSpra.Count) spra forms."
