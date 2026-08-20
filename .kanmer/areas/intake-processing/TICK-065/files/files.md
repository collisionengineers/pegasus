## Files — TICK-065 (INT-32 completion)

### New
- `src/Pegasus.Core/ImageIntake/ImageIntakeChaseSchedule.cs` — pure derived chase-due read for an Image-initiated Case Awaiting instruction, reusing `Pegasus.Core.Tasks.CaseChaseSchedule.FirstChaseAt` (one schedule policy, two owners of state).
- `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeChaseScheduleTests.cs` — boundary tests for the new function.

### Edited
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — add `ImageChaseState(bool)` label, reusing the exact "Chase due" text `ChaseState(CaseDueWorkState)` already uses.
- `src/Pegasus.Web/Pages/Triage/Index.cshtml.cs` — add `IsImageIntakeChaseDue(DateTimeOffset registeredAtUtc)` helper on `IndexModel`, backed by the injected `TimeProvider` (existing field).
- `src/Pegasus.Web/Pages/Triage/Index.cshtml` — add a "Chase" column to the Image-initiated Not-ready table, rendered with the existing `Shared/_StatusChip` partial.
- `src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml` — add tone entries for `"chase due"`, `"chasing paused"`, `"chasing stopped"` (previously unmapped, silently falling to neutral for the Case-side chip too) and the new `"not yet due"`.
- `docs/frd/frd-02-intake-and-source-identity.md` — document the Image-initiated chase-due read next to the existing association/lifecycle section.
- `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` — add a web-rendering assertion for the new Chase column.

### Read only (context / verification, no change)
- `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs` (`CaseChaseSchedule`, `CaseDueWork`, `ICaseDueWorkQueries`) — the case-side chase machinery being reused/mirrored, not duplicated.
- `src/Pegasus.Core/Tasks/RunDueChasers.cs`, `RecordManualCaseChase.cs` — confirmed out of scope (no chaser-draft generation for images is being added; see plan).
- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`, `ImageIntakeCasePairing.cs` — confirmed `ImageIntakeSummary.RegisteredAtUtc` is the existing age source; confirmed the merge path.
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` — confirmed the merge transition already writes a `CaseHistory` row (`image_initiated_case_merged`) in the same transaction.
- `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:209`, `docs/capabilities.md:232` — confirmed the pairing-visibility half (`Associated with Case` label) is already shipped and is the capability's actual notification commitment.
