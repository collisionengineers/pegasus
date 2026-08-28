# CASE-012 file map (lane E1 — whole files)

Owned and changed:

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | Rewritten: workspace frame (header, ribbon, presence, action bar, sticky edit bar, side nav, context column, Overview, section placeholders, dialogs) |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | Section routing, engineer account list + name resolution, export marker, EVA dialog state, outstanding-requirements projection; handlers unchanged |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml` | New: six-item side nav |
| `src/Pegasus.Web/Pages/Cases/Shared/_ReadinessHiddenFields.cshtml` | New: the one readiness envelope the Review-gated transitions post |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` | Rewritten: "Case overview" panel (Work facts / Parties / accident card) |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` | Rewritten: stepper, outstanding requirements, edit form + confirm-completeness, edit-cluster actions, proposed-values conflict panel |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseHistory.cshtml` | Rewritten: Notes section (timeline entries, Add note, Record chase) |
| `src/Pegasus.Web/Pages/EditModeDisplay.cs` | Added `HolderName` (the holder as a value, reusing the sentence's naming rule). No active lane owns this file; additive only |
| `src/Pegasus.Core/Eva/EvaBundleSchema.cs` | Added public const `BundleExportedHistoryEventKind` ("eva_bundle_exported"). No wave-2 lane owns Core/Eva |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | Uses the promoted const (one list per concept) |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | Retargeted to new markup |
| `tests/Pegasus.IntegrationTests/CaseReportApprovalWebTests.cs` | Typed-SHA form removed; handler contract pinned for its future caller |
| `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs` | `?tab=evidence` → `?section=case-files` |
| `tests/Pegasus.IntegrationTests/ImageViewingWebTests.cs` | `?tab=evidence` → `?section=case-files` |
| `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` | Review round 1: custody-recovery journey retargeted to Operations Attention required + case-files confirmation + Send-page export; reason-dialog journey retargeted to the case-files section; `Edit Case` casing in the shared helper |

Unchanged in lane (verified no edit needed): `Workflow.cshtml(.cs)` and
`Closure.cshtml(.cs)` (handler-only pages, no markup), `Create.*` (no drawn
contract; the shell Add dialog already links to it), `Eva/Send.*` (kept as a
working route — and the scriptless export route; the handoff dialog posts to
its handlers directly). Test files verified unchanged and correctly so
(review round 1 correction to an earlier draft of this map):
`CaseEditModeWebTests.cs`, `CaseWorkflowWebTests.cs`,
`CaseClosureWebTests.cs` post straight to the handlers and assert only
TempData notice text, which the new page still renders.

Neighbour lanes — not touched: `Vehicle.*`, `Custody.*`, `Tasks.*`,
`_CaseDocuments.cshtml`, `Documents/**`, `Assessment/**` (CASE-027/ENG-025),
`site.css`/`site.js`/`Pages/Shared/*` (PLAT-029), `Cases/Index.*` (CASE-025),
`Pages/Operations/**` (PLAT-023; its existing Attention required retry
surface is driven by the retargeted journey, not modified).
