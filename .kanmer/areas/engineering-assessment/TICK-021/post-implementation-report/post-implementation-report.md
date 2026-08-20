# EXT-02 post-implementation report — 2026-08-20

PR: https://github.com/collisionengineers/pegasus/pull/448 (base `dev`, branch `task/tick-021-ext-02-mot-chronology`, commit `64dbfc2f`).

## What shipped vs the plan

Everything the plan named, nothing else:

- `src/Pegasus.Core/Vehicle/VehicleMileagePolicy.cs` — `VehicleMileageEvidenceClass { Supplied, External, Estimated }` and `VehicleMileageEvidenceClassification.Classify(CaseDataSourceKind)` (`VehicleLookup` → Estimated; all else → Supplied), with the operator rule documented at the policy.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — `MileageEvidence` word map (added during the simplification pass to honour the documented code→word convention; a deviation from the plan's raw-enum rendering, recorded there).
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` — MOT chronology table (test date / result / expiry / mileage — External; newest first; em dash for absent values), derived-estimate mileage row on the latest observation labelled Estimated with its observed-on date, classification suffix on the confirmed mileage and on the facts-table Mileage row (fact/confirmed/suggestion each classified by its own source).
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` — Odometer row classification suffix.
- `tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs` — 7 new tests: lookup-sourced mileage is Estimated and explicitly never Supplied; theory over the five remaining source kinds → Supplied; accept-resolution proposes exactly the derived `VehicleMileageCalculation`.

## Evidence

- Release build: 0 warnings. Focused Vehicle filter: 23/23. Full `Pegasus.Core.Tests`: 703/703. `Pegasus.ArchitectureTests`: 97/97.
- No migrations, no Infrastructure/Worker/MCP changes, no new port/store; EVA deterministic bundle content untouched.

## Failure behaviour

Unchanged and fail-closed: conflicting latest MOT readings still produce no estimate (no invented value; the chronology still renders); a missing typed case-data row omits the classification suffix rather than guessing.

## Known limits / follow-ups

- The activation note "live adapter/provider contract remains unresolved" is EXT-01's boundary (TICK-020), untouched here — this ticket displays evidence already persisted by the existing replay/production adapters.
- Out-of-scope bug reported for its own ticket: `EvaHandoffStore.cs` `"staff-correction"` vs `'staff_correction'` mismatch (EVA status never `Corrected` for staff-corrected vehicle fields).
- Intake draft preview mileage and the Assessment report "Mileage source" select were deliberately left out (different concepts; recorded in research).
