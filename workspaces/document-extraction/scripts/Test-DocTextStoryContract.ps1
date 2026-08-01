[CmdletBinding()]
param(
    [string] $ContractPath = (Join-Path $PSScriptRoot '../docs/architecture/doc-text-story-contract.v1.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$contract = Get-Content -Raw -LiteralPath $ContractPath | ConvertFrom-Json

if ($contract.schemaVersion -ne 'collisiondocnet-doc-text-story-contract/1') { throw 'Unexpected DOC text/story schema.' }
if (($contract.owners -join '|') -ne 'EXT-DOC-003|EXT-DOC-004') { throw 'Unexpected DOC text/story ownership.' }
if ($contract.specification.sha256 -ne '2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a') { throw 'Unexpected MS-DOC source hash.' }
if ($contract.clxPolicy.mandatoryForSupportedFamily -ne $true -or $contract.clxPolicy.simpleFileFallback -ne $false) { throw 'Supported Word text must always use CLX.' }

$expectedMappings = @(
    '0x82|U+201A','0x83|U+0192','0x84|U+201E','0x85|U+2026','0x86|U+2020','0x87|U+2021',
    '0x88|U+02C6','0x89|U+2030','0x8A|U+0160','0x8B|U+2039','0x8C|U+0152',
    '0x91|U+2018','0x92|U+2019','0x93|U+201C','0x94|U+201D','0x95|U+2022','0x96|U+2013',
    '0x97|U+2014','0x98|U+02DC','0x99|U+2122','0x9A|U+0161','0x9B|U+203A','0x9C|U+0153','0x9F|U+0178'
)
$actualMappings = @($contract.compressedByteOverrides | ForEach-Object { "$($_.byte)|$($_.unicode)" })
if (($actualMappings -join ',') -ne ($expectedMappings -join ',')) { throw 'The exact FcCompressed substitution table changed.' }

$expectedParts = @('ccpText|Main','ccpFtn|Footnote','ccpHdd|Header','ccpAtn|Comment','ccpEdn|Endnote','ccpTxbx|MainTextbox','ccpHdrTxbx|HeaderTextbox')
$actualParts = @($contract.documentParts | ForEach-Object { "$($_.field)|$($_.kind)" })
if (($actualParts -join ',') -ne ($expectedParts -join ',')) { throw 'The exact seven-part order changed.' }
if ($contract.partPolicy.reserved3 -ne 'Must be zero and is never a Macro part.') { throw 'reserved3 must not become a Macro part.' }
$expectedHeaderPrefix = @('FootnoteSeparator','FootnoteContinuationSeparator','FootnoteContinuationNotice','EndnoteSeparator','EndnoteContinuationSeparator','EndnoteContinuationNotice')
$expectedHeaderPerSection = @('EvenHeader','OddHeader','EvenFooter','OddFooter','FirstHeader','FirstFooter')
if (($contract.partPolicy.headerStoryPrefix -join '|') -ne ($expectedHeaderPrefix -join '|')) { throw 'The header story prefix changed.' }
if (($contract.partPolicy.headerStoryPerSection -join '|') -ne ($expectedHeaderPerSection -join '|')) { throw 'The per-section header/footer order changed.' }
if ($contract.quickSavePolicy.c1BaseRange -ne '0 through 15' -or $contract.quickSavePolicy.d9AndLaterBase -ne '0x000F' -or $contract.quickSavePolicy.d9AndLaterNewRange -ne '0 through 15') { throw 'The quick-save version partition changed.' }
if ($contract.quickSavePolicy.authoritativeState -ne 'The current FIB, selected Table stream and current CLX only.') { throw 'The quick-save authority changed.' }

$expectedControls = @('U+0001','U+0002','U+0005','U+0007','U+0008','U+0009','U+000B','U+000C','U+000D','U+000E','U+0013','U+0014','U+0015','U+0028','U+003C','U+003E','U+2002','U+2003')
$actualControls = @($contract.controlTokens | ForEach-Object codePoint)
if (($actualControls -join '|') -ne ($expectedControls -join '|')) { throw 'The typed control-token set changed.' }

$requiredCases = 1..39 | ForEach-Object { 'DOC-R03-C{0:D3}' -f $_ }
$actualCases = @($contract.cases | ForEach-Object id)
if (($actualCases -join '|') -ne ($requiredCases -join '|')) { throw 'The DOC-R03 case sequence changed.' }
foreach ($case in $contract.cases) {
    if ([string]::IsNullOrWhiteSpace($case.condition) -or [string]::IsNullOrWhiteSpace($case.outcome)) { throw "$($case.id) is incomplete." }
}
if (($contract.fixtureGroups -join '|') -ne 'DOC-T01|DOC-T02|DOC-T03') { throw 'The DOC-R03 fixture groups changed.' }

$canonicalLines = @(
    "clx|$($contract.clxPolicy | ConvertTo-Json -Compress)"
    "piece|$($contract.piecePolicy | ConvertTo-Json -Compress)"
    $contract.compressedByteOverrides | ForEach-Object { "map|$($_.byte)|$($_.unicode)" }
    $contract.documentParts | ForEach-Object { "part|$($_.order)|$($_.field)|$($_.kind)|$($_.startsAfter)" }
    "partPolicy|$($contract.partPolicy | ConvertTo-Json -Compress)"
    "quickSave|$($contract.quickSavePolicy | ConvertTo-Json -Compress)"
    $contract.controlTokens | ForEach-Object { "control|$($_.codePoint)|$($_.kind)|$($_.requiresSpecial)|$($_.reviewProjection)|$($_.owner)" }
    "review|$($contract.reviewProjection | ConvertTo-Json -Compress)"
    $contract.cases | ForEach-Object { "case|$($_.id)|$($_.condition)|$($_.outcome)" }
)
$canonicalBytes = [System.Text.Encoding]::UTF8.GetBytes(($canonicalLines -join "`n") + "`n")
$canonicalHash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($canonicalBytes)).ToLowerInvariant()
if ($canonicalHash -ne '85529c714ded2e4776c0930ef82e5d3c099c6822d156c06bf94b8248e0529c31') { throw "The reviewed DOC text/story contract changed: $canonicalHash" }

Write-Output "DOC text/story contract verified: $($contract.cases.Count) cases, $($contract.compressedByteOverrides.Count) compressed overrides, $($contract.documentParts.Count) parts."
