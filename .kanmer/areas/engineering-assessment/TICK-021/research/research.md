# EXT-02 research — MOT chronology + supplied/external/estimated mileage classification

All premises below were verified by read-only checks on `origin/dev` @ `29b81000` (worktree `../pegasus-worktrees/tick-021`) unless marked assumed.

## Governing truth

- FRD-06 §Vehicle data and MOT enrichment (`docs/frd/frd-06-vehicle-and-engineering-evidence.md:127-149`): lookups preserve provider/source/version/age and typed outcomes; acceptance enters permanent history.
- Binding operator truth `docs/operator-notes.md` §Vehicle-source and classification distinctions (~line 232-247): "Mileage in instructions → Supplied fact"; "Mileage calculated from accepted MOT observations → Derived estimate with its observations and method; **never relabel as supplied mileage**"; "MOT observation → separately sourced test chronology/status and recorded mileage/value/unit evidence".
- Design authority `docs/design/README.md` §Case (line ~649): Case work includes "DVLA/DVSA and MOT/mileage observations with source/version/age and **supplied/external/estimated classification**" — the three-word vocabulary is already the settled design language. Data tables, no narration, banned-word list applies.

## What exists (verified)

- Port + contracts: `src/Pegasus.Core/Vehicle/LookupContracts.cs` — `MotTestObservation(TestDate, TestStatus, ExpiryDate, Mileage, MileageUnit)`, `VehicleLookupResult.MotTests`, validated in `EnsureValidFor`.
- Derived estimate: `src/Pegasus.Core/Vehicle/VehicleMileagePolicy.cs` — `VehicleMileageCalculation(Value, Unit, ObservedOn, MethodKey "latest-mot-observation", MethodVersion 1, SupportingObservationCount)`; conservative, no extrapolation (ADR-0012).
- Evidence records: `src/Pegasus.Core/Vehicle/VehicleWorkflow.cs` — `VehicleLookupObservation.MotTests` (hydrated from `MotTestsJson`, `EfVehicleLookupWorkStore.cs:188`), `ConfirmedVehicleField<T>` (string `SourceKind`/`SourceLabel`), `CaseVehicleEvidence`.
- Accept path (`src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs:273-350`): decision `Accept` stores the **derived calculation** (`observation.Mileage?.Value`) as the confirmed mileage with `SourceKind = 'vehicle_lookup'`; decision `Correct` stores staff values with `'staff_correction'`. So a confirmed lookup-sourced mileage IS the derived estimate — this is the value that must never display as Supplied.
- Case-data source vocabulary: `CaseDataSourceKind` enum in `src/Pegasus.Core/Cases/CaseDataContracts.cs:14` (IntakeEvidence, MailRoute, CaseAcceptance, StaffCorrection, VehicleLookup, ProviderSetting) — the one Core list; Infrastructure `CaseDataCodes` strings are its persistence encoding.
- `GetCase` (`src/Pegasus.Core/Cases/CaseQueries.cs:210`) always attaches `Data` (throws when missing) and `VehicleEvidence`; both views of the confirmed mileage read the same `CaseDataFields` row, so `data.Vehicle.Mileage.Confirmed.Source.Kind` (typed enum) is available wherever the confirmed panel renders.

## Confirmed gaps (the missing halves)

1. **MOT chronology display**: `MotTests` is never rendered anywhere in `src/Pegasus.Web` (verified: no `MotTests` hits under Pages/Presentation). The stored chronology (test date, status, expiry, mileage) is invisible to operators.
2. **Classification labels**: mileage figures render with no supplied/external/estimated classification —
   - `_CaseWorkflow.cshtml:141` facts row "Mileage" (provenance icon word "Lookup"/"Extracted" only, hover-revealed);
   - `_CaseWorkflow.cshtml:464` "Confirmed mileage" — bare value + unit, no source at all;
   - the latest observation's derived `VehicleMileageCalculation` is never displayed as a value (only pre-fills the correction form input, unlabelled);
   - `_CaseSummary.cshtml:92-99` "Odometer" row — value + provenance icon only.

## Reuse (existing seams named)

- Classification rule → new tiny Core policy beside `VehicleMileagePolicy` keyed on the existing `CaseDataSourceKind` enum (no new string list; no second vocabulary).
- Rendering enums raw is the existing convention (`@observation.Outcome`, `_CaseWorkflow.cshtml:470`), so `VehicleMileageEvidenceClass` enum names ARE the operator words (Supplied/External/Estimated) — no label map needed.
- Table markup: existing `table-wrap > table` convention (`_CaseWorkflow.cshtml:106-120`).
- Value—label suffix convention: `@confirmed.Registration.Value — @confirmed.Registration.SourceLabel` (`_CaseWorkflow.cshtml:461`).

## Deliberately out of scope

- Intake draft preview (`Pages/Intake/Details.cshtml:346`) shows the extracted (pre-case) draft mileage — an extraction preview, not case mileage evidence.
- Assessment report form "Mileage source" select (`Assessment/Index.cshtml`) is the separate assessment-report narrative source (owner/repairer/online data…), not this classification.
- No adapter/provider work: EXT-02's display halves need no live call; the activation boundary ("live adapter contract unresolved") is EXT-01's concern and untouched here.
- EVA bundle strings (`EvaHandoffStore.cs`) untouched — deterministic bundle content must not change.

## Latent bug found (not fixed here — report to review)

`src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs:822` compares `field.SourceKind.Equals("staff-correction")` but the store writes `'staff_correction'` (underscore, `CaseDataCodes.StaffCorrection`) — the EVA evidence status can never become `Corrected` for staff-corrected vehicle fields. Behaviour-affecting; belongs in its own ticket, not this diff.

## Premises assumed (not re-verified)

- Local test suite conventions in `tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs` (xunit, plain Fact/Theory) — verified by reading test list; runtime behaviour assumed from repo runbook.
