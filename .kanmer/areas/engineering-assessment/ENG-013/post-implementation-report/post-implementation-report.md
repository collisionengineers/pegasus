# Post-implementation report — ENG-013

Commit `94b6a9dd` on `task/qdos26011-regressions`.

## What changed

`EfVehicleLookupWorkStore.RecordOutcomeAsync` now calls `AddLookupSuggestionsAsync` immediately after it adds the observation entity, inside the same `Serializable` transaction. The helper reads the case's existing suggestion field names once, then adds up to four `CaseDataFields` rows at `ValueKind = "suggestion"`, `SourceKind = "vehicle_lookup"`:

| Observation value | Field | Policy recorded |
| --- | --- | --- |
| `Make` | `vehicle_make` | `vehicle-lookup-gap-fill/v1` |
| `Model` | `vehicle_model` | `vehicle-lookup-gap-fill/v1` |
| `MileageValue` | `vehicle_mileage` | `latest-mot-observation` at its own version |
| `MileageUnit` | `vehicle_mileage_unit` | `latest-mot-observation` at its own version |

The mileage carries the calculation's own key and version rather than this rule's, because that is what `VehicleMileageEvidenceClassification` reads to label it a derived estimate wherever it is shown ([[ENG-010]]).

## Why nothing else was needed

`CaseField<T>.Current` is already `Confirmed ?? Fact ?? Suggestion`. Writing at the suggestion tier means precedence is preserved by construction: an extracted fact or a staff-confirmed value silently outranks the lookup, and neither is read or written by this code. There is no new precedence rule to remember and no new concept.

The helper deliberately does **not** reuse `EfVehicleWorkflowStore.SetConfirmedField`, which stamps `ConfirmedByActor`, `ConfirmedAtUtc` and the acceptance policy key — the three things a suggestion must not carry, and which `CK_CaseDataFields_Confirmation` would reject.

## Evidence

`VehicleLookupGapFillTests`, three tests, run against LocalDB — all passed in 1 m 7 s:

- `ALookupFillsAMileageTheDocumentsNeverCarried` — the suggestion row exists with the lookup's mileage and unit and `SourceKind = vehicle_lookup`, and **no** row of any other kind exists for that field.
- `AnExtractedMakeOutranksTheLookupsOwn` — both rows exist, so the lookup's finding is not discarded, but the extracted fact remains what the case reads.
- `ASecondLookupDoesNotDuplicateOrOverwriteTheFirst` — a repeat lookup with a different mileage leaves exactly one row, still holding the first value.

## What this does not do

It does not make a lookup value acceptable to an EVA hand-off. `CaseEvaMapping.MapForProduction` reads `Confirmed ?? Fact` only, and `CaseOperatorExportTests.ASuggestedMileageStillCannotReachAHandoff` holds that line explicitly.
