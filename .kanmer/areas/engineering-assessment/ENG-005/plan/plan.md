## Plan

1. **Red-first test.** In `EvaHandoffPersistenceTests.cs`, add a test that builds a `CaseVehicleEvidence` whose `Registration` field's `SourceKind` is `CaseDataCodes.StaffCorrection` (reusing the existing `VehicleField<T>` fixture helper pattern, `ConfirmedVehicle`, `Factory`, `SeedCaseAsync`, `LocalDbTestDatabase` conventions already in the file), executes `EvaHandoffStore.ExecuteAsync`, parses `generated.Bundle.ProvenanceContent` (reusing the existing `JsonDocument.Parse` + `fields` array pattern from `StaffConfirmedThirdPartyVehicleImagesAreExcludedFromPreparationAndGeneratedBundle`), and asserts the `VRM` field's `status` is `"corrected"`. Confirm it fails against current code (hyphen literal never matches).
2. **Fix.** In `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs::FromVehicleField` (~line 822), replace `field.SourceKind.Equals("staff-correction", StringComparison.Ordinal)` with `field.SourceKind.Equals(CaseDataCodes.StaffCorrection, StringComparison.Ordinal)` — reusing the one existing owner constant (`CaseDataCodes.StaffCorrection`, `CaseDataEntities.cs`), same namespace, no new `using`.
3. **Grep check.** Confirm no other hyphen/underscore literal mismatch exists against `CaseDataCodes` (already checked in `files` doc — only the one hit).
4. **Verify.** Run the new test red, then green after the fix; run the full `EvaHandoffPersistenceTests` class (SqlServer/LocalDB category) to confirm no regression; `dotnet build -c Release` for 0 warnings.

No new abstractions, no schema change — this is a one-line literal fix reusing the existing `CaseDataCodes` constant that every other call site in the codebase already uses.

## Simplification pass

n/a until implementation diff exists — pass recorded before PR per lane instructions.

## Simplification pass (2026-08-20)

Diff is a 2-file, ~60-line change (1 production line, 1 new test + 1 backward-compatible optional parameter on an existing test helper). Reviewed against the four lenses:

- **Reuse.** Fix reuses the existing single owner constant `CaseDataCodes.StaffCorrection` (no new constant, no new list). Test reuses the existing `VehicleField<T>` fixture helper, extended with an optional `sourceKind` parameter (default preserves every existing call site) rather than adding a second helper or duplicating the record construction.
- **Simplification.** No further reduction possible — this is already the minimal one-line literal fix.
- **Efficiency.** No behavioural/perf-relevant change beyond the corrected comparison.
- **Altitude.** No abstraction, no interface, no new file added. Nothing to flatten.

No findings to disposition — nothing applied beyond the plan's own steps.
