# Files — CASE-012

## Owned (changed)

- `src/Pegasus.Web/Pages/Cases/Details.cshtml` — rewritten frame.
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` — sections, assessment
  access state, Engineer name, blockers, load time.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` — Overview.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` — deleted.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml` — new.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml` — new (moved).
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml` — new (moved).
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseHistory.cshtml` — chase content
  moved in.
- `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml(.cs)` — EVA handoff.
- `src/Pegasus.Web/Pages/Cases/Create.cshtml` — vocabulary.
- `docs/design/test-ui/catalogue.json` — Details branch text.
- `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`,
  `CaseEditModeWebTests.cs`, `CaseReportApprovalWebTests.cs`,
  `CaseWorkflowWebTests.cs`, `Browser/OperatorJourneyTests.cs`.

## Read only

`Presentation/OperatorLabels.cs`, `Pages/EditModeDisplay.cs`,
`CaseMutationPageModel.cs`, `Workflow.cshtml.cs`, `Closure.cshtml.cs`,
`Custody.cshtml.cs`, `Tasks.cshtml.cs`, `Documents/Export.cshtml.cs`,
`Shared/_*` partials, `wwwroot/css/site.css`, `wwwroot/js/site.js`, Core
`Cases/CaseQueries.cs`, `Workflow/CaseWorkflowContracts.cs`,
`Lifecycle/CaseLifecycle.cs`, `Assessment/AssessmentWorkspace.cs`,
`Identity/StaffAccountAdministration.cs`.

## Not touched

`Vehicle.*`, `Custody.*`, `Tasks.*`, `_CaseDocuments.cshtml`, `Documents/**`,
`Assessment/**`, `site.css`, `site.js`, `Pages/Shared/**`.
