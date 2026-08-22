# Plan — ENG-013

Same lane and branch as [[CASE-018]]: `task/qdos26011-regressions`.

## Steps

1. **A private `AddSuggestion` local in `EfVehicleLookupWorkStore`.** Takes the context, case id, field name, value type, value and the observation's source identity; returns immediately when the value is null or when a row already exists for `(CaseId, FieldName, "suggestion")`. Never reads or writes `fact` or `confirmed` rows, so precedence is preserved by construction rather than by a rule someone has to remember.

   *Reuses:* `CaseDataFieldNames`, `CaseDataCodes`, `CaseDataFieldEntity` — no new type. It deliberately does **not** reuse `EfVehicleWorkflowStore.SetConfirmedField`: that helper stamps `ConfirmedByActor`/`ConfirmedAtUtc` and the acceptance policy key, which are exactly the things a suggestion must not carry.

2. **Call it four times** in `RecordOutcomeAsync`, immediately after the observation entity is added, guarded by `result.Outcome` being one that produced data. Source identity is the observation id; source label names the provider and its version, as `SourceLabel` does elsewhere; policy key/version are `VehicleMileagePolicy.MethodKey` / `MethodVersion` for the mileage and the lookup provider version for the rest.

3. **Load the existing rows once** before the four calls — a single `context.CaseDataFields.Where(item => item.CaseId == work.CaseId)` inside the open transaction — rather than four round trips.

4. **Tests.** Two integration tests, both driving the real store:
   - a completed lookup on a case with no vehicle rows writes four suggestions, and the projection's `Current` mileage is the lookup value;
   - a completed lookup on a case whose `vehicle_make` is an extracted `fact` still writes the suggestion row but leaves `Current` reading the fact.

## What deliberately does not change

The suggestion is not accepted evidence. It shows on the case, and it is carried into the operator export ([[CASE-019]]) with its real `suggested` status. It does **not** satisfy `CaseEvaMapping.MapForProduction`, so it can never let an EVA hand-off through on unaccepted data.

## Acceptance

- QDOS26011's Vehicle block reads 121,823 Miles with lookup provenance instead of "Not recorded".
- A case with an extracted make keeps that make as its current value.
- Re-running a lookup does not create a second suggestion row or overwrite an existing one.

## Simplification pass

Recorded after implementation, before the PR.
