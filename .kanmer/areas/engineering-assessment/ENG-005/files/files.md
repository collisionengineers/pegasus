## Files touched

- `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` — `FromVehicleField` (~line 822): replace the hyphenated literal `"staff-correction"` with the one owner `CaseDataCodes.StaffCorrection` (`"staff_correction"`, defined in `CaseDataEntities.cs`, same namespace `Pegasus.Infrastructure.Persistence` — no new `using` needed).
- `tests/Pegasus.IntegrationTests/EvaHandoffPersistenceTests.cs` — add a red-first test asserting that a vehicle field confirmed with `SourceKind = CaseDataCodes.StaffCorrection` is emitted into the generated EVA bundle's provenance (`VRM` field) with `status: "corrected"`, not `"accepted"`.

## Grep confirmation (verification checklist item 2)

`grep -rn "staff-correction" src/` finds exactly one hit — the bug at `EvaHandoffStore.cs:822`. All other `SourceKind`/`CaseDataSourceKind` comparisons in the codebase already use `CaseDataCodes.StaffCorrection` / `CaseDataSourceKind.StaffCorrection` (the correct persisted underscore form), so no other literal mismatch exists.
