# Files — CASE-007

| File | Change |
| --- | --- |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml | Empty/edit-only panels absent read-only; EVA panel → compact operator card with disclosure; narration lines removed; raw enum renders labelled |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml | Escaping icon fix support; "Details are incomplete" display map |
| src/Pegasus.Web/Pages/Cases/Details.cshtml(.cs) | Edit toggle button in the action bar (existing lease handlers); dirty-confirm dialog wiring |
| src/Pegasus.Web/Presentation/OperatorLabels.cs | Legacy chase-reason display map; inspection-mode label |
| src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs | Writer string "Details are incomplete" |
| src/Pegasus.Web/wwwroot/css/site.css | detail-list icon overflow fix; toggle styling |
| src/Pegasus.Web/wwwroot/js/site.js | Dirty tracking + toggle-off confirm dialog |
| tests (CaseDetailsWebTests / CaseWorkflowWebTests / browser) | Updated assertions: absent panels, new copy, toggle |
