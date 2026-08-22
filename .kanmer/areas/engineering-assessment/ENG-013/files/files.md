# Files — ENG-013

| File | Change |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfVehicleLookupWorkStore.cs` | In `RecordOutcomeAsync`, after the observation is added, write the observation's make, model, mileage and mileage unit as `suggestion` rows on `CaseDataFields` — only where no row of that field name and kind already exists, and never touching `fact` or `confirmed` rows. |
| `tests/Pegasus.IntegrationTests/…` | A completed lookup on a case with no mileage produces a `suggestion` mileage row; a completed lookup on a case that already carries an extracted `fact` make leaves that fact as the current value. |

## Why this file and not another

`RecordOutcomeAsync` is the one place a lookup outcome becomes durable. It already:

- runs inside a `Serializable` transaction,
- has the `CaseWorkflowEntity` loaded and calls `CaseMutationGuard.Complete(workflow)`, so the case version moves with the write,
- writes the observation, the `CaseWorkflowEvent` and the `ActionHistory` row in that same transaction.

Adding the suggestion rows here makes them atomic with the observation they came from. Writing them anywhere else would mean a second transaction that could fail independently and leave the two out of step.

## Scope confirmed against the schema

`CaseDataFields` PK is `(CaseId, FieldName, ValueKind)` and `CK_CaseDataFields_FieldName` pins `FieldName` to `CaseDataFieldNames.All`. Of the observation's values, exactly four have a field name that already exists:

| Observation column | Field name | Value type |
| --- | --- | --- |
| `Make` | `vehicle_make` | `text` |
| `Model` | `vehicle_model` | `text` |
| `MileageValue` | `vehicle_mileage` | `integer` |
| `MileageUnit` | `vehicle_mileage_unit` | `text` |

`ManufactureYear`, `EngineCapacityCc` and `FuelType` have no field name and are out of scope — see [[CASE-018]]'s rejected-during-mapping note. They remain readable on the observation.

## Not touched

- `EfVehicleWorkflowStore.AcceptAsync` — staff acceptance still promotes a value to `confirmed`, unchanged.
- `VehicleMileagePolicy` and `VehicleMileageEvidenceClassification` — the mileage is still classified as a derived estimate wherever it is shown ([[ENG-010]]).
- `CaseField<T>.Current` — the `Confirmed ?? Fact ?? Suggestion` precedence is what makes this additive, and is already correct.

## Read-only checks run

Prod, 2026-08-22, all three live cases: every `VehicleLookupObservations` row carries a mileage, and no case carries a `vehicle_mileage` `CaseDataFields` row of any kind. The gap this ticket closes is real on every case in the estate, not only the one reported.
