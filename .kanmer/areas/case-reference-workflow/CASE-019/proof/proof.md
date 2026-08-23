# Proof — CASE-019

Release 25, source `75570b99d713cbd3b0010f7e335c6d89acfb14b0`, image `sha256:e99ade3c…`, revision `pegasus-prod-web-252ow37gij--75570b99d713`. Smoke passed 2026-08-23.

## The archive, downloaded from production

Signed-in operator session, Export pressed on QDOS26011. The file that came back, opened and read:

```
EVA-QDOS26011.json                              655
Images/002 1_CLVoffside-V1.jpg               462,270
Images/003 2_CLVnearside-V1.jpg              436,099
Images/004 3_CLVreardamage-V1.jpg            406,251
Images/005 4_CLVreardamage-V1-2.jpg          434,690
Images/006 5_CLVreardamage-V1-3.jpg          430,856
Images/008 6_CLVreardamageatscene-V1.jpg     605,567
Images/009 6_VIN-V1.jpg                      399,587
Images/010 Mileage-V1.jpg                    235,575
provenance.json                                8,123
manifest.sha256                                  955
```

3,422,086 bytes. Exactly what was asked for: a zip of the images and a JSON matching the EVA import format. All eight retained photographs; the instruction PDF and the `.eml` correctly excluded.

## The JSON

```json
{"Work Provider":"QDOS","VRM":"ST66BCE","Vehicle Model":"CX-5 SE-L D NAV",
 "Claimant Name":"Mr Harry Sykes","Reference":"QDOS26011",
 "Incident Date":"19/08/2026","Instruction Date":"22/08/2026",
 "Inspection Date":"23/08/2026","Inspection Address":"Image Based Assessment",
 "Accident Circumstances":"Our client, in their vehicle was proceeding in the third lane…",
 "VAT Status":"","Mileage":"121823","Mileage Unit":"miles"}
```

Thirteen keys, fixed order, every one a string.

## Integrity

Every entry's SHA-256 recomputed from the downloaded archive and compared against `manifest.sha256`: **10 verified, 0 mismatched.**

`provenance.json` records `mapping.key qdos-eva-13-field-mapping`, `mapping.version 1`, acceptance evidence `docs/frd/frd-07-eva-and-external-engineering-handoff.md`, and all eight images — which is [[PLAT-037]]'s acceptance criterion, met.

## Every field says what it actually is

| Field | Status | Source |
| --- | --- | --- |
| Work Provider | accepted | `case-data:MailRoute` |
| VRM, Vehicle Model, Claimant Name, Incident Date, Instruction Date, Accident Circumstances | accepted | `case-data:IntakeEvidence` |
| Reference | accepted | `case-identity` |
| Inspection Address | accepted | `case-data:ProviderSetting` |
| **Inspection Date** | accepted | **`SystemDefault:Export date`** — today's, per operator direction, and named as a default rather than implied to be supplied |
| **VAT Status** | **unrecorded** | the case genuinely holds no value; emitted empty rather than blocking the download |
| **Mileage, Mileage Unit** | **suggested** | `case-data:VehicleLookup` — the lookup-derived figure travels as suggested, never claimed as accepted |

That last row is the point of the design: the archive carries a value the case has only as a suggestion, and says so. The same value still cannot reach an EVA hand-off, which `CaseOperatorExportTests.ASuggestedMileageStillCannotReachAHandoff` holds.

## What it took — four faults, only one visible

| # | Fault | Fixed |
| ---: | --- | --- |
| 1 | `Details.cshtml` emitted `asp-route-id` against a `{caseId:guid}` route, so no `href` was generated and the control was inert | release 23 |
| 2 | Every intake attachment was filed `Instruction`, so no photograph was eligible and the archive would have been **empty** ([[DOCS-009]]) | release 23 |
| 3 | The EVA mapping was switched off in production ([[PLAT-037]]) | release 23 |
| 4 | `VerifyFileMetadataAsync` compared Box's `content_type`, which Box does not send, so every managed Box read refused every file ([[DOCS-010]]) | release 25 |

Only the first was visible to the operator. Each fix exposed the next.

## Regression coverage

- `ExportingACaseProducesTheEvaFormatArchive` — drives intake → accept → custody → export, opens the archive and hash-checks every entry against the manifest; asserts the PDF and `.eml` stay out, all thirteen keys present as strings, and that no `EvaHandoffRevision` or `EvaFirstHandoffProxy` is written, because an export is a read.
- `CaseOperatorExportTests` (7) — blank field named and tolerated, absent inspection date defaulted, suggested value kept suggested, unaccepted mapping refused, and two tests holding the hand-off's own bar unchanged.
- `BoxManagedRevisionTests` (5) — the Box metadata rule, including that a wrong length and a file in another case's folder are still refused.

## Known and not claimed

The export takes roughly 25 seconds for eight images because `OpenReadVersionAsync` re-resolves the Box case folder on every call — about 32 Box round trips. It works; it will not scale to a case with many more photographs. Raised separately rather than folded in here.
