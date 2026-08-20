## What changed

- `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` (`FromVehicleField`, line 822): `field.SourceKind.Equals("staff-correction", StringComparison.Ordinal)` → `field.SourceKind.Equals(CaseDataCodes.StaffCorrection, StringComparison.Ordinal)`. Same namespace (`Pegasus.Infrastructure.Persistence`), no new `using`.
- `tests/Pegasus.IntegrationTests/EvaHandoffPersistenceTests.cs`: added `StaffCorrectedVehicleRegistrationIsReportedAsCorrectedInGeneratedBundle` — builds a vehicle registration field with `SourceKind = CaseDataCodes.StaffCorrection`, generates an EVA bundle, and asserts the `VRM` provenance field's `status` is `"corrected"`. Extended the existing `VehicleField<T>` fixture helper with an optional `sourceKind` parameter (default `"staff-confirmation"` preserves every other call site) instead of adding a second helper.

## Grep confirmation

`grep -rn "staff-correction" src/` — exactly one hit, the fixed line. All other `SourceKind`/`CaseDataSourceKind` comparisons in the repo already use the correct underscore constant/enum.

## Test evidence

- Red-first: new test run against pre-fix code failed as expected — `Assert.Equal() Failure: Strings differ — Expected: "corrected", Actual: "accepted"`.
- Post-fix, full focused class: `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~EvaHandoffPersistenceTests"` → **Passed! Failed: 0, Passed: 8, Skipped: 0, Total: 8** (7 pre-existing + 1 new).
- Related Core.Tests: `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~Eva"` → **Passed! Failed: 0, Passed: 40, Skipped: 0, Total: 40**.
- `dotnet build ./Pegasus.slnx -c Release --no-restore` → **Build succeeded. 0 Warning(s). 0 Error(s).**

## Verification checklist (from ticket body)

- [x] Test: staff-corrected vehicle field → `Corrected` status in the EVA evidence value.
- [x] No other hyphen/underscore literal mismatches against `CaseDataCodes` (grep — one hit, fixed).

## Notes

This changes deterministic EVA bundle content for any case where a vehicle field carries a staff correction — a regenerated hand-off is a new revision, which is the existing designed behaviour (no schema or revision-semantics change needed).

## Simplification pass

Recorded in `plan` doc under "Simplification pass (2026-08-20)" — n/a beyond what the fix already reuses (existing constant, existing test helper extended rather than duplicated).
