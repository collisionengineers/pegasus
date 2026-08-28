# File map — CASE-026

Lane-owned files (wave 2, D):

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Search/Index.cshtml.cs` | Rewrite page model to the ported design: view-row records, selected handling, staff-name resolve, freshness. |
| `src/Pegasus.Web/Pages/Search/Index.cshtml` | Rewrite view: page-header + freshness + Create Case, advanced-search-grid, two-pane case-search-layout, selectable rows with per-row preview templates. |
| `src/Pegasus.Web/Pages/Search/_CasePreview.cshtml` | New partial: the Selected Case preview rendered once server-side and once per row template. |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | Extend `CaseSearchItem` with `VehicleMake`, `VehicleModel`, `AccidentCircumstances` (the named extension). |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | Project the three new fields in `SearchRow`/`MapSearchItem` from the joined instruction draft. |
| `tests/Pegasus.IntegrationTests/CasesIndexWebTests.cs` | Keep the three contracts; extend with ported-page assertions (preview pane, selected row, Closed · outcome chip). |

Read-only reuse (no edits): `Pages/Shared/_FreshnessBanner.cshtml`,
`Pages/Shared/_StatusChip.cshtml`, `Presentation/OperatorLabels.cs`,
`wwwroot/css/site.css`, `wwwroot/js/site.js`, `Pages/Cases/Index.*`
(precedent), `Pages/Cases/Shared/_CaseSummary.cshtml` (vocabulary).
`AdministrationSearchAccountWebTests.cs` search part already passes as-is
(301 + empty sentence kept) — no edit needed unless the build says
otherwise.
