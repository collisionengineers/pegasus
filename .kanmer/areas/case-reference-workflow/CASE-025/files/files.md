# Files — CASE-025

Whole files this ticket owns (wave-2 lane C1; EPIC-011 waves.md):

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` | Rework (95f69958 kept as base; repairs per research) |
| `src/Pegasus.Web/Pages/Cases/Index.cshtml` | Rewrite: three-pane queue layout per §1.4 |
| `src/Pegasus.Web/Pages/Unidentified/Index.*` | Owned; PLAT-029's 301 stub is correct — no change |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | 95f69958 completeness projection kept |
| `src/Pegasus.Core/Operations/DashboardCounts.cs` | 95f69958 Complete count kept |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | 95f69958 projection kept |
| `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` | 95f69958 count kept |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | 95f69958 TriageState + CaseRequirements kept |
| `src/Pegasus.Web/Pages/Intake/Details.cshtml` | Compile-forced one-line fixup from 95f69958 (lane C2's file — disclosed, not expanded) |
| `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` | Rewrite to the §1.4 contract |

Not touched (verified not this lane): `RailCountsPageFilter.cs` (§1.1
count is complete-free and correct), `site.css`/`site.js` (PLAT-029),
`Pages/Search/**` (lane D), `Pages/Triage/Details.*`,
`Pages/Unidentified/Details.*`, `Pages/ImageIntake/Details.*` (lane C2),
`Migrations/*`.
