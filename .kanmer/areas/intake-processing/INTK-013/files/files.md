# Files — INTK-013

## Root cause (confirmed by prod-diagnostics §2 and code read)

- `src/Pegasus.Web/Pages/Triage/Index.cshtml` renders the Not ready badge from
  `Model.StageCounts.NotReady` (line 32).
- `Model.StageCounts` is populated in `src/Pegasus.Web/Pages/Triage/Index.cshtml.cs`
  `OnGetAsync` from `IDashboardQueries.GetCaseStageCountsAsync`.
- `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs`
  `GetCaseStageCountsAsync` (lines 22-42) counts **only** `CaseWorkflows` rows
  in state `NotReady` — i.e. instruction-initiated Not ready cases.
- The Not ready tab's *rows* come from `LoadNotReadyAsync`
  (`src/Pegasus.Web/Pages/Triage/Index.cshtml.cs`, private method) which unions
  two independent queries: `ISearchCases` (CaseWorkflows NotReady, instruction
  origin) **and** `IImageIntakeQueries.ListAsync(false, ...)` filtered to
  `ImageInitiatedCaseState.AwaitingInstruction` (image origin, unmerged Image
  Intakes with no formal Case yet).
- `src/Pegasus.Web/Pages/Index.cshtml.cs` (`CaseStages` property) and
  `src/Pegasus.Web/Pages/Index.cshtml` (line 25) read the **same**
  `GetCaseStageCountsAsync` result for the Dashboard's Not ready tile, so it
  has the identical defect and must be fixed by the same change to stay
  consistent with the tab (ticket's own instruction).

## Files to change

- `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` —
  `GetCaseStageCountsAsync`: add the image-initiated awaiting-instruction
  count (unassociated `ImageIntakes` rows, i.e. `MergedIntoCaseId == null`, in
  lifecycle state `AwaitingInstruction`) into the `NotReady` figure, as a
  second cheap aggregate `CountAsync` — no row projection, matching this
  class's own documented convention.
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` — widen the
  existing `private static string ToCode(ImageInitiatedCaseState state)`
  (line 909) to `internal static`, so `EfDashboardQueries` reuses the single
  state-code mapping instead of duplicating the literal `"awaiting_instruction"`
  (one list per concept).
- `src/Pegasus.Core/Operations/DashboardCounts.cs` — no field/shape change;
  update the XML doc on `CaseStageCounts.NotReady`/the class remarks to state
  it now spans both case origins (documentation only, no interface break).
- Tests:
  - `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` — add a
    mixed-origin regression test (one instruction-initiated NotReady case via
    the existing `SeedNotReadyCaseAsync` fixture, one image-initiated
    awaiting-instruction Image Intake via the existing
    `IRegisterImageIntake` flow already used in
    `NotReadyOriginFilterReturnsOnlyTheMatchingOriginsRows`) asserting the
    `/Triage?queue=not_ready` badge count equals 2 and equals the number of
    rows rendered (both origins present).
  - Same file/fixture reused for a Dashboard-tile consistency assertion
    (`GET /` shows the same NotReady figure), since `Index.cshtml.cs` reads
    the identical `GetCaseStageCountsAsync` call.

## Existing code reused (no new abstractions)

- `IDashboardQueries.GetCaseStageCountsAsync` — extended in place, not
  duplicated.
- `EfImageIntakeStore.ToCode(ImageInitiatedCaseState)` — reused by widening
  visibility, not re-implemented.
- `SeedNotReadyCaseAsync` / `IRegisterImageIntake` / `IImageIntakeOriginResolver`
  — existing integration-test fixtures, reused as-is.
