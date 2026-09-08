# Files — DELIV-057

## Where the change lands

| Path | Why |
|---|---|
| `tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs` | Restore the two non-null historical `Cases` columns in the one shared pre-backfill seed so all three tests can reach their existing assertions. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260822195419_CorrectIntakePhotographSemanticRole.Designer.cs` | The exact migration target named by the tests retains required `InstructionConfirmedByStaff` and `ImagesConfirmedByStaff` columns on `Cases`. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260822223626_BackfillVehicleLookupSuggestions.cs` | The migration under test backfills suggestion-tier case data from lookup observations; its transition, fact-precedence, and idempotency assertions must remain unchanged. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260907221500_RemoveCaseStaffConfirmation.cs` | PLAT-072 later drops the two staff-confirmation columns, so the correction belongs solely in the historical fixture. |
| `docs/engineering.md` | A changed executable test input needs focused test evidence, to be requested from the designated sole host verifier. |

## Ripple effects

The one shared seed is used by all three `VehicleLookupBackfillTests`; restoring the required values lets their existing assertions exercise the migration again. There are no callers, production schema artifacts, grants, models, documentation, or generated files to update.

## Out of scope

Do not modify production migrations, models, permissions/grants, current-schema snapshots, production data, test assertions, other fixtures, or documentation. Do not add compatibility behavior or new domain input data.
