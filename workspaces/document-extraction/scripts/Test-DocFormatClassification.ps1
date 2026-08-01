[CmdletBinding()]
param(
    [string] $MatrixPath = (Join-Path $PSScriptRoot '../docs/architecture/doc-format-classification.v1.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$matrix = Get-Content -Raw -LiteralPath $MatrixPath | ConvertFrom-Json

if ($matrix.schemaVersion -ne 'collisiondocnet-doc-format-classification/1') { throw 'Unexpected DOC classification schema.' }
if (($matrix.owners -join '|') -ne 'EXT-DOC-001|EXT-DOC-012') { throw 'DOC classification must be jointly owned by EXT-DOC-001 and EXT-DOC-012.' }
if ($matrix.specifications[0].sha256 -ne '2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a') { throw 'Unexpected MS-DOC source hash.' }
if ($matrix.specifications[1].sha256 -ne '2d650184072a148ba98ad0b68072fd5ad7780e46f3528d7f263f3127b2dadab5') { throw 'Unexpected MS-CFB source hash.' }
if ($matrix.specifications[2].sha256 -ne '9b7a67eb5d0408566a61f218792fcd21536dbc970d83695ad94365e535533f33') { throw 'Unexpected MS-OFFCRYPTO source hash.' }
if ($matrix.specifications.Count -ne 4) { throw 'The DOC classification authority set must contain exactly four sources.' }
$msgSource = $matrix.specifications[3]
if ($msgSource.name -ne 'MS-OXMSG' -or $msgSource.revision -ne '18.0' -or
    $msgSource.published -ne '2025-05-20' -or
    $msgSource.url -ne 'https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxmsg/b046868c-9fbf-41ae-9ffb-8de2bd4eec82' -or
    $null -ne $msgSource.sha256 -or
    ($msgSource.sections -join '|') -ne '2.2.1|2.2.2|2.3|2.4|2.4.1.1' -or
    $msgSource.pinStatus -notmatch 'no retained hash-pinned publication') {
    throw 'The exact MS-OXMSG authority and missing-hash provenance gate changed.'
}

$expectedVersions = @('0x00C1', '0x00D9', '0x0101', '0x010C', '0x0112')
if (($matrix.supportedEffectiveNFib -join '|') -ne ($expectedVersions -join '|')) { throw 'The exact five supported effective nFib values changed.' }
if ($matrix.hintPolicy.affectsRouting -ne $false -or $matrix.hintPolicy.affectsCompleteness -ne $false) { throw 'Hints must affect neither routing nor completeness.' }

$requiredPredicates = 1..5 | ForEach-Object { 'DOC-R02-P{0:D3}' -f $_ }
$actualPredicates = @($matrix.profilePredicates | ForEach-Object id)
if (($actualPredicates -join '|') -ne ($requiredPredicates -join '|')) { throw 'The executable recognition predicate set changed.' }
foreach ($predicate in $matrix.profilePredicates) {
    foreach ($property in @('name', 'minimumIdentificationEvidence', 'strongMatch', 'identifiedFailure')) {
        if ([string]::IsNullOrWhiteSpace($predicate.$property)) { throw "$($predicate.id) has no $property." }
    }
}

$requiredCases = 1..26 | ForEach-Object { 'DOC-R02-C{0:D3}' -f $_ }
$actualCases = @($matrix.cases | ForEach-Object id)
if (($actualCases | Select-Object -Unique).Count -ne $actualCases.Count) { throw 'DOC classification case IDs are not unique.' }
foreach ($requiredCase in $requiredCases) {
    if ($requiredCase -notin $actualCases) { throw "Missing required classification case $requiredCase." }
}
foreach ($case in $matrix.cases) {
    foreach ($property in @('condition', 'classification', 'variant', 'action', 'outcome')) {
        if ([string]::IsNullOrWhiteSpace($case.$property)) { throw "$($case.id) has no $property." }
    }
    if ($case.outcome -notin @('OwningExtractor', 'AccordingToCurrentBytes', 'UnsupportedFeature', 'UnsupportedFormat', 'Corrupt', 'Encrypted', 'Cancelled', 'TimedOut', 'ResourceLimitExceeded')) {
        throw "$($case.id) has an invalid outcome $($case.outcome)."
    }
}

$legacy = $matrix.cases | Where-Object id -eq 'DOC-R02-C006'
if ($legacy.classification -ne 'UnverifiedLegacyWordIdentifier' -or $legacy.outcome -ne 'UnsupportedFeature') { throw 'Legacy identifiers must remain generic and unsupported.' }
$unrelated = $matrix.cases | Where-Object id -eq 'DOC-R02-C009'
if ($unrelated.outcome -ne 'UnsupportedFormat') { throw 'A valid unrelated CFB must be UnsupportedFormat.' }
$ambiguous = $matrix.cases | Where-Object id -eq 'DOC-R02-C012'
if ($ambiguous.outcome -ne 'UnsupportedFeature' -or $ambiguous.action -notmatch 'InvokeNoParser') { throw 'Ambiguous input must invoke no parser.' }
$repair = $matrix.cases | Where-Object id -eq 'DOC-R02-C004'
if ($repair.outcome -ne 'OwningExtractor') { throw 'Repair flags alone must not make a valid Word input corrupt.' }
$repairClaim = $matrix.cases | Where-Object id -eq 'DOC-R02-C016'
if ($repairClaim.outcome -ne 'AccordingToCurrentBytes') { throw 'An external repair claim must not override the byte-derived outcome.' }

$canonicalLines = @(
    $matrix.profilePredicates | ForEach-Object { "$($_.id)|$($_.name)|$($_.minimumIdentificationEvidence)|$($_.strongMatch)|$($_.identifiedFailure)" }
    $matrix.cases | ForEach-Object { "$($_.id)|$($_.condition)|$($_.classification)|$($_.variant)|$($_.action)|$($_.outcome)" }
)
$canonicalBytes = [System.Text.Encoding]::UTF8.GetBytes(($canonicalLines -join "`n") + "`n")
$canonicalHash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($canonicalBytes)).ToLowerInvariant()
if ($canonicalHash -ne 'c84fa08b0ebc67aa6b023e925093a27de2e1e95ddfd2d04a79a476306f7e8871') { throw "The reviewed DOC classification contract changed: $canonicalHash" }

if (($matrix.fixtureGroups -join '|') -ne 'DOC-T01|DOC-T02|DOC-T03|DOC-T04') { throw 'The DOC-R02 fixture groups changed.' }
Write-Output "DOC format classification verified: $($matrix.cases.Count) cases, $($matrix.supportedEffectiveNFib.Count) supported layouts."
