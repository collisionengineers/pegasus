# Post-implementation report — INTK-013

## What changed

- `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` —
  `GetCaseStageCountsAsync` now adds a count of unmerged Image Intakes still
  in `AwaitingInstruction` (image-initiated Not ready) to the existing
  CaseWorkflows `NotReady` count (instruction-initiated), so the returned
  `CaseStageCounts.NotReady` spans both origins.
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` — widened
  `ToCode(ImageInitiatedCaseState)` from `private static` to `internal
  static` so the state-code literal is defined once and reused.
- `src/Pegasus.Core/Operations/DashboardCounts.cs` — documented that
  `CaseStageCounts.NotReady` now spans both case origins (no shape change).
- `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` — added
  `NotReadyBadgeCountMatchesRowsAcrossBothOrigins`: seeds one
  instruction-initiated NotReady case and one image-initiated
  awaiting-instruction Image Intake, then asserts the `/Triage?queue=not_ready`
  badge reads 2 (matching both rows rendered) and that the Dashboard's `/`
  Not-ready tile reports the identical figure.

## Why this shape

The Dashboard tile (`Pages/Index.cshtml.cs` `CaseStages.NotReady`) and the
Queues tab badge (`Triage/Index.cshtml.cs` `StageCounts.NotReady`) both read
`GetCaseStageCountsAsync`. Fixing the one shared query keeps both screens
consistent with a single change, per the ticket's own note. The added count
uses the identical filter (`MergedIntoCaseId == null`, state
`AwaitingInstruction`) that `LoadNotReadyAsync` already applies to build the
tab's row list, so the badge and the rows agree by construction rather than
by two independently-maintained definitions.

## Test evidence

- `dotnet build ./Pegasus.slnx -c Release --no-restore` — Build succeeded, 0
  warnings, 0 errors.
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
  -c Release --no-build --filter "FullyQualifiedName~TriageQueuesWebTests"` —
  Passed: 4, Failed: 0, Skipped: 0 (includes the new regression test).
- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj -c
  Release --no-build --filter "FullyQualifiedName~DashboardBoundaryTests"` —
  Passed: 3, Failed: 0, Skipped: 0 (regression; contract untouched).

## Simplification pass

Recorded in the ticket `plan` doc under "Simplification pass — 2026-08-20":
reuse, simplification, efficiency and altitude lenses reviewed; no findings —
diff reuses `ToCode` and the existing test fixtures, adds a single aggregate
`CountAsync` matching the class's own no-row-projection convention.

## Left out / parked

Nothing parked. No operator question arose — the fix is a direct
implementation of the ticket's own stated resolution.
