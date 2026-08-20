# Files — INTK-022

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | `CaseSearchOrder` enum + `Order` on `SearchCasesQuery` (default newest-first); validation |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | Order switch in `SearchAsync`; `NextChaseAtUtc` on the search projection (DueWork left join) |
| `src/Pegasus.Web/Pages/Triage/Index.cshtml(.cs)` | One merged Not-ready table; origin + Principal dropdown form (auto-submit, no-script Apply); sortable headers; `sort`/`principal` query params |
| `src/Pegasus.Web/wwwroot/js/site.js` | `data-auto-submit` change handler (if not already present) |
| `src/Pegasus.Web/wwwroot/css/site.css` | Filter-row + sort-header styling as needed |
| `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` | Updated for dropdown surface + merged table; new sort/merge assertions |

No migration; `CaseSearchItem` gains an optional trailing `NextChaseAtUtc` (existing constructors unaffected).
