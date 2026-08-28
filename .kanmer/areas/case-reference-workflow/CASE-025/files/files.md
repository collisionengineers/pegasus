# Files — CASE-025

Diff estimate: ~900 lines changed (view rewrite ~330, PageModel ~450,
tests ~250, small Core/Infra/labels/catalogue edits).

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Index.cshtml` | Rewrite to `queue-layout`: rail groups, filter bar, per-kind rows, quick detail |
| `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` | Seven tabs, group counts, Principal/Missing filters, selected-record load, `StateLabel` removed |
| `src/Pegasus.Web/Pages/Unidentified/Index.cshtml(.cs)` | Unchanged (301 kept) |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | `TriageState` label map (moved), `CaseRequirement` texts |
| `src/Pegasus.Web/Pages/Intake/Details.cshtml` | Retarget `StateLabel` call to `OperatorLabels.TriageState` |
| `src/Pegasus.Core/Operations/DashboardCounts.cs` | `CaseStageCounts.Complete` |
| `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` | Count `PostReportComplete` |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | `CaseSearchItem.InstructionComplete/ImagesComplete` |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | Project the two completeness columns |
| `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` | Retargeted at equal strength; new group-count, Missing, Blocked-uncounted, selected-pane tests |
| `docs/design/test-ui/catalogue.json`, `index.html` | `/Cases` state branch text |

Not touched: `Triage/Details.*`, `Pages/Cases/Details.*`, `site.css`,
`site.js`, `Pages/Shared/**`.
