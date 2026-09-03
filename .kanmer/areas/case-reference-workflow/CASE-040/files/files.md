# Files — CASE-040 (2026-09-02, gpt-5.6-terra high, wrapper-checked)

Every path below was confirmed to exist (or not, for `create`) in the main
checkout at `cad00be9`. Wrapper corrections: the label `Reuses` cell names
the existing `OperatorLabels.CaseWorkspace` / `OperatorLabels.EvaHandoffs`
classes (Codex cited `CaseStage`, which is a method); the EVA dialog note
below records that the script dialog markup is inside CASE-038-owned
`Details.cshtml`.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` | change | Add persisted Case sign-off account identity and command data. | `CaseWorkflowRecord`, `AssignCaseEngineerRequest` |
| `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs` | change | Keep the pure default/validation rule and compound send transition in Core. | `AssignCaseEngineer`, `StartCaseWork`, eligibility policy |
| `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs` | change | Add nullable `SignOffEngineerId` on `CaseWorkflows`. | `AssignedEngineerId` |
| `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs` | change | Persist, map, history-record, replay-protect, and attribute the selection/handoff. | `MutateAsync`, `AddEvent`, `HistoryValue` |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | change | Project the additional workflow identity into Case Details. | Existing workflow projection (`AssignedEngineerId` at lines 247, 388) |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseSignOffEngineer.cs` | create | Add the CaseWorkflows column after PLAT-068's account migration (shared lock, capacity one; serialized). | Existing EF migration convention |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseSignOffEngineer.Designer.cs` | create | EF-generated migration metadata. | Existing generated migration pair |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | change | EF-generated current model snapshot. | EF migration convention |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change (shared lock, capacity one) | Add `Engineer`, `SignOffEngineer`, `SendToEva`, `DownloadZip`, `SendViaApi` to the existing Case vocabulary. | `OperatorLabels.CaseWorkspace` (line 1297), `OperatorLabels.EvaHandoffs` (line 1059) |
| `src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs` | change | Bind the selected sign-off account and invoke the Core command. | `OnPostAssignEngineerAsync` |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` | change (shared lock, capacity one) | Show Sign-off Engineer in Overview beside Engineer. | Existing Engineer fact (lines 27-31) |
| `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml.cs` | change | Load Engineer/sign-off options, allow Review and With Engineer, bind both identities/routes. | `IStaffAccountQueries`, `IEvaSubmissionModeStore`, `CaseMutationPageModel` |
| `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml` | change | Render Engineer, Sign-off Engineer, Download ZIP, and API choices. | CASE-012 send-page structure |
| `tests/Pegasus.Core.Tests/Lifecycle/AssignCaseEngineerTests.cs` | change | Prove flagged/unflagged default resolution and explicit eligible selection. | Existing eligibility/replay fake |
| `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs` | change | Prove Case identity persistence, event history, and re-send semantics. | Existing workflow harness |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | change | Prove sign-off UI input, route visibility, and With Engineer re-send (extends `SendToEvaRendersOnlyInReview`, `SendPageRendersItsChoiceForAReviewCase`). | Existing EVA handoff tests |
| `docs/design/test-ui/pages/case-eva-send--default.html` | change (shared lock, capacity one) | Regenerate the altered Send page snapshot with `scripts/Update-TestUiSnapshots.ps1`. | Catalogue scenario `case-eva-send--default` (catalogue.json line 361-369) |

The snapshot capture also changes
`docs/design/test-ui/pages/case-details--default.html` only when CASE-038's
slot lands with CASE-040's value. `catalogue.json` already names both files and
needs no entry change unless the owning lane adds a new state.

EVA dialog note: the script `eva-handoff-dialog` (Engineer select posting
Assign Engineer, export form, API form) is markup inside
`src/Pegasus.Web/Pages/Cases/Details.cshtml` lines 561-620, and the
"Download EVA package" label to retire is at line 251 — both CASE-038's file.
CASE-040 changes the script-off route (`Eva/Send.*`) and the view-model
values; the plan must record whether CASE-038 hosts the Sign-off select and
the label retirement in the dialog, or hands that dialog block to CASE-040
before it starts.

## Must not touch

- `src/Pegasus.Core/Identity/**`,
  `src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfStaffAccountQueries.cs`,
  `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`,
  `src/Pegasus.Web/Pages/Administration/Accounts/**`, and PLAT-068's
  `AspNetUsers` migration: [[PLAT-068]] owns the flag, qualifications,
  signature, and account-profile query.

- `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`,
  `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`,
  `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`,
  the Scriban report template, and `docs/frd/frd-11-*`: [[DOCS-017]] owns the
  report tuple and FRD reconciliation.

- `src/Pegasus.Web/Pages/Cases/Details.cshtml`,
  `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`,
  `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml`,
  `src/Pegasus.Web/wwwroot/css/site.css`, and
  `src/Pegasus.Web/wwwroot/js/site.js`: [[CASE-038]] owns the frame, ribbon,
  Current position slot, and action-bar placement.

- `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml`,
  `_CaseEstimate.cshtml`, `_CaseSettlement.cshtml`, `_CaseReport.cshtml`,
  `_CaseValuation.cshtml`, and `_CaseEngineerNotes.cshtml`: [[ENG-034]],
  [[CASE-029]], and [[CASE-039]] own those sections.

- [[CASE-041]] inspect-at and [[CASE-042]] Awaiting instruction paths: their
  data and UI lanes are independent.

- `docs/design/test-ui/**` is [[UIIMP-014]]'s shared-lock area beyond the one
  Send-page snapshot row above; do not edit it concurrently.

- `docs/frd/frd-07-eva-and-external-engineering-handoff.md`, governing
  decisions, and other governing documentation: [[DELIV-041]] owns
  reconciliation (FRD-07 lines 8-20, 79-102, 118-120 conflict with D36 and
  were outside DELIV-041's file list; a docs follow-up is needed).
