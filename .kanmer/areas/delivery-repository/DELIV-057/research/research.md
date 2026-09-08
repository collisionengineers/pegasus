# Research — DELIV-057: historical vehicle lookup migration fixture

## Question

Why do the three `VehicleLookupBackfillTests` fail during setup, and what exact schema does their pre-backfill migration target require?

## Findings

- `tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs` migrates each test database to `20260822195419_CorrectIntakePhotographSemanticRole`, seeds a `Cases` row, then migrates to head. All three facts share `SeedCaseWithObservationAsync`.
- The target migration designer, `src/Pegasus.Infrastructure/Persistence/Migrations/20260822195419_CorrectIntakePhotographSemanticRole.Designer.cs`, declares non-null `bit` properties `InstructionConfirmedByStaff` and `ImagesConfirmedByStaff` on `Cases`. The following backfill migration designer preserves the same shape.
- The fixture's current `INSERT INTO Cases` omits both historical columns. The ticket's supplied PR700 run34196369756 evidence identifies the first resulting setup error as the required `InstructionConfirmedByStaff` value; SQL Server will require both non-null historical columns.
- Commit `d442366787d452da22d36719272d4eb79dc1afde` (PLAT-072) removed both values from this fixture while adding `20260907221500_RemoveCaseStaffConfirmation`. That later migration drops both columns from current `Cases`; its current model snapshot contains neither.
- The backfill migration only inserts `CaseDataFields` suggestion values from existing lookup observations. Its assertions cover mileage, fact precedence, suggestion preservation, and idempotency; no assertion needs changing.
- `docs/engineering.md` requires focused evidence for affected executable test inputs and says to retain exact command/exit evidence. The ticket assigns execution to sole host verifier DELIV-053; no local verification command is authorized.
- `get_sources` returned no declared research sources for the ticket's area and labels.

## Implications

Restore the two historical, required `Cases` seed columns and their existing boolean fixture values in the one shared setup statement. This changes only the pre-migration fixture to match its explicit target schema; it does not reintroduce current production state, alter a migration/model/grant, change test data semantics, or weaken any assertion.

## Open questions

None.
