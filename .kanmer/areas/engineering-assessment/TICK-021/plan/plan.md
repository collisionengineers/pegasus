# EXT-02 plan — MOT chronology + mileage classification

Feature contract: every operator-facing mileage figure on the Case workspace carries its supplied/external/estimated classification, the stored MOT test history renders as a dated chronology, and a mileage derived from MOT observations is never presented as supplied (binding: `docs/operator-notes.md` vehicle-source table; design words: `docs/design/README.md` §Case).

Caller: the existing Case Details page (`GetCase` → `_CaseWorkflow.cshtml` / `_CaseSummary.cshtml`). No new caller, port, store, or migration. Failure behaviour unchanged: absent evidence keeps the existing explicit empty states; no value is invented when `VehicleMileagePolicy.Calculate` returns null (conflicting latest readings) — the chronology still renders, the estimate row simply does not.

## Steps

1. **Core classification** (reuses `CaseDataSourceKind`, sits in `VehicleMileagePolicy.cs`):
   - `public enum VehicleMileageEvidenceClass { Supplied, External, Estimated }`
   - `public static class VehicleMileageEvidenceClassification { public static VehicleMileageEvidenceClass Classify(CaseDataSourceKind sourceKind); }`
     - `VehicleLookup` → `Estimated` — the accept path stores the derived `VehicleMileageCalculation`, so a lookup-sourced case mileage is by construction the derived estimate (EfVehicleWorkflowStore accept path, verified in research).
     - every other kind → `Supplied` (instructions/extraction/staff-entered values are directly supplied by a person or document).
   - XML doc records the invariants used directly at display sites: an `MotTestObservation` reading is `External`; a `VehicleMileageCalculation` is `Estimated`.
2. **Core tests** (`tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs`, reuses existing file + observation builders):
   - `Classify(VehicleLookup)` is `Estimated` and explicitly not `Supplied` (pins the operator rule).
   - Theory over all remaining `CaseDataSourceKind` values → `Supplied`.
   - Pin the linkage: `VehicleSuggestionAcceptancePolicy.Resolve(..., Accept, null)` proposes exactly `observation.Mileage?.Value` (the derived calculation) — so the value later classified `Estimated` is the estimate, not a raw supplied figure. (Extend/confirm existing accept-resolution test.)
3. **`_CaseWorkflow.cshtml`** (reuses `table-wrap` table markup, raw-enum rendering, "value — label" suffix convention):
   - Facts row "Mileage": suffix the accepted/suggested value with ` — @Classify(source.Kind)`.
   - Vehicle evidence panel, confirmed block: `Confirmed mileage` renders `value unit — @Classify(data.Vehicle.Mileage.Confirmed.Source.Kind)` (omit suffix if the typed row is unavailable; never guess).
   - Latest observation block: when `observation.Mileage is { } calc`, add `<dt>Mileage</dt><dd>calc.Value calc.Unit — Estimated</dd>` plus the observed-on date; when MOT readings conflict (calc null) nothing renders (no invented value).
   - New MOT chronology table when `observation.MotTests.Count > 0`, ordered `TestDate` descending: columns `Test date | Result | Expiry | Mileage (External)`; empty mileage cells render an em dash. No narration.
4. **`_CaseSummary.cshtml`**: Odometer `DataRow` format delegate appends ` — @Classify(...)` from `data.Vehicle.Mileage.Current.Source.Kind`.
5. **Verify**: `dotnet build ./Pegasus.slnx -c Release` (zero warnings), `dotnet test tests/Pegasus.Core.Tests --filter "FullyQualifiedName~Vehicle"`.

## Acceptance

- Chronology table renders every stored `MotTestObservation` field (date, status, expiry, mileage+unit) newest first.
- Every mileage figure on `_CaseWorkflow`/`_CaseSummary` carries Supplied/External/Estimated; the derived value is labelled Estimated at both its suggestion and confirmed surfaces; Core test pins `VehicleLookup ≠ Supplied`.
- No new store/port/migration; EVA bundle strings untouched.

## Out of scope (recorded in research)

Intake draft preview mileage; Assessment report "Mileage source" select; live adapter activation (EXT-01); EvaHandoffStore `"staff-correction"` comparison bug (reported for its own ticket).
