# Files — CASE-042 (2026-09-02)

Produced by gpt-5.6-terra (medium) in a read-only checkout; the wrapper added
the `EfDashboardQueries.cs` row and the ownership note beneath the table.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` | change | Add the Awaiting instruction tab, row loader/count, selected quick-detail shape, and remove image rows from Not ready. | `Tabs`, `QueueRow`, `ImageRow`, `LoadNotReadyAsync`, `IImageIntakeQueries`; CASE-032 projection. |
| `src/Pegasus.Web/Pages/Cases/Index.cshtml` | change | Render the tab and its quick-view actions only once their handlers are named and wired. | Existing rail, row, definition-list, and button-row markup. |
| `src/Pegasus.Web/Presentation/RailCountsPageFilter.cs` | change | Include the AwaitingInstruction count in the Cases shell rail total once it leaves the Not ready addend. | Existing parallel stage/Triage/Unidentified count pattern; CASE-032 count/query. |
| `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` | change (wrapper-added; ownership to settle in plan) | `GetCaseStageCountsAsync` adds unassociated AwaitingInstruction intakes to Not ready (lines 56-68, INTK-013); the addend must move to a separate awaiting count or the tab and rail double count. May also touch `src/Pegasus.Core/Operations/DashboardCounts.cs` if `CaseStageCounts` gains a field. | Existing `CountAsync` over `ImageIntakes`; `EfImageIntakeStore.ToCode`. |
| `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` | change | Prove tab count equals rows, Not ready no longer includes image rows, shell rail includes Awaiting instruction; update `NotReadyRailCountMatchesRowsAcrossBothOrigins` (INTK-013) to the new split. | `RegisterImageIntakeAsync`, existing regex count assertions, `SeedNotReadyCaseAsync`. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change only after shared-lock handoff | Add the approved Awaiting instruction and group-label entries; no new view literals. | `ImageIntakeLifecycleState` and central label convention. |

No new file is presently justified. CASE-042 cannot independently create or
change Core/Infrastructure image-intake projection files while CASE-032 blocks
it; the `EfDashboardQueries.cs` count change is outside CASE-032's stated
scope and the plan must record which ticket carries it (CASE-042 or a
CASE-032 amendment) before implementation. The Work Centre Not ready metric
(`Pages/Index.*`, EPIC-011 lane A) reads the same count and changes value as
a side effect; the plan states this and names no edit there.

## Files CASE-042 must not touch

- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` and
  `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs`:
  **CASE-032** owns the required projection contract and adapter changes.
- `src/Pegasus.Web/Pages/Cases/Details.cshtml`,
  `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`, and
  `src/Pegasus.Web/Pages/Cases/Shared/*`: **CASE-038** and **ENG-034** lanes.
- `src/Pegasus.Core/Assessment/AssessmentContracts.cs`,
  `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`, and report
  templates: **ENG-035**.
- `src/Pegasus.Web/wwwroot/js/damage-diagram.js` and renderer SVG:
  **ENG-036**.
- `src/Pegasus.Web/Pages/Cases/Assessment/*`: **ENG-034**.
- `src/Pegasus.Web/Pages/Administration/**`: **PLAT-068**.
- `src/Pegasus.Web/Pages/Operations/**`: **PLAT-069**.
- `src/Pegasus.Core/AiWork/**`: **AUTO-018**.
- `src/Pegasus.Web/Pages/Index.*` (Work Centre): EPIC-011 lane A
  (UIIMP-008); report the metric side effect, do not edit.
- `src/Pegasus.Web/wwwroot/css/site.css`, `src/Pegasus.Web/wwwroot/js/site.js`,
  `src/Pegasus.Web/Pages/Shared/*`, `src/Pegasus.Web/Pages/Cases/Shared/*`,
  `src/Pegasus.Web/Pages/Administration/Shared/*`,
  `src/Pegasus.Infrastructure/Persistence/Migrations/**`, and governing docs:
  shared-lock paths held by **CASE-038** this wave.
- `docs/design/test-ui/**`, including `catalogue.json` and `/Cases` captures:
  **UIIMP-014** owns snapshot states for this wave.
