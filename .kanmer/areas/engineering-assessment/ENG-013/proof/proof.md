# Proof — ENG-013

Release 24, source `19969404018414e968e9aedaab4dff99fb4338ad`, image `sha256:bd9d8c4a…`, revision `pegasus-prod-web-252ow37gij--199694040184`. Smoke passed 2026-08-23, asserting the deployed source revision matches the manifest.

## The operator's complaint, closed

The Vehicle block on QDOS26011 now reads:

> **Mileage  121,823 Miles — Estimated**

Screenshotted on the live case. That is the ticket's stated acceptance criterion, and it was `Not recorded` on the same page an hour earlier — the "before" was captured on the same production instance.

The figure comes from the DVSA latest-MOT observation, and it carries its classification word, so a derived estimate is never presented as supplied ([[ENG-010]]).

## The two-mileage report, explained and resolved

QDOS26010 was the case the operator pointed at. Production now holds, for that case:

| Field | Kind | Value | Source |
| --- | --- | --- | --- |
| `vehicle_mileage` | **fact** | 132,389 | `intake_evidence` |
| `vehicle_mileage` | **suggestion** | 128,343 | `vehicle_lookup` |

Both are retained — the lookup's finding is not discarded — but `CaseField.Current` resolves `Confirmed ?? Fact ?? Suggestion`, so the page shows the extracted 132,389 and only that. The operator saw two figures because the old "Vehicle evidence" panel rendered the observation beside the case field; that panel is gone ([[CASE-018]]).

## Both halves shipped

The code half (release 23) fills a case's empty vehicle fields when a lookup **runs**. That helps only cases whose lookup is still to come, and every case in the estate had already been looked up — so QDOS26011 was unchanged and the ticket was **not** met by release 23 alone.

The data half (release 24, `20260822223626_BackfillVehicleLookupSuggestions`) corrects the recorded past. Read back from production after it applied:

| Case | make | mileage | unit |
| --- | --- | --- | --- |
| QDOS26009 | fact BMW + suggestion BMW | suggestion 72,312 | suggestion Miles |
| QDOS26010 | fact RENAULT + suggestion RENAULT | **fact 132,389** + suggestion 128,343 | fact miles + suggestion Miles |
| QDOS26011 | fact MAZDA + suggestion MAZDA | **suggestion 121,823** | suggestion Miles |

Exactly what a read-only dry run of the migration's predicate forecast before it was applied. No fact was displaced anywhere; every lookup value landed at the suggestion tier with `SourceKind = vehicle_lookup`, and the mileage carries `latest-mot-observation` as its policy key so it classifies as an estimate.

## Test evidence

- `VehicleLookupGapFillTests` (3) — the code path: a lookup fills an empty mileage; an extracted fact outranks the lookup's own; a repeat lookup neither duplicates nor overwrites.
- `VehicleLookupBackfillTests` (3) — the migration, run by EF itself against a database migrated to the previous migration and seeded with a pre-fix case, so the shipped SQL is exercised rather than a copy.
- CI green on both merges: `unit`, `browser`, three `sql-integration` shards.

## Not claimed

That a lookup value can reach an EVA hand-off. It cannot, and that is asserted: `CaseEvaMapping.MapForProduction` reads `Confirmed ?? Fact` only, and `CaseOperatorExportTests.ASuggestedMileageStillCannotReachAHandoff` holds the line.
