# Files touched — PLAT-010

Scope: every operator-facing Razor page under `src/Pegasus.Web/Pages/**` except
`Administration/Mailboxes.cshtml` (PLAT-009), and except `Pages/Unidentified/Index.cshtml`,
`Pages/Unidentified/Details.cshtml`, `Pages/Upload.cshtml`, `Pages/UploadStatus.cshtml`
(carved out mid-flight by the operator — separate structural-rebuild tickets own them; any
edits this ticket made to them were reverted).

## Pages edited (narration/lede/banned-terms/internal-identifier fixes)

- `Cases/Assessment/Index.cshtml` (+`.cshtml.cs`) — worst offender, 33 blocks
- `Cases/Shared/_CaseWorkflow.cshtml` — 24 blocks, `artifact`/`bounded` fixes
- `Intake/Details.cshtml` (+`.cshtml.cs`) — 21 blocks, `intake` banned-term sweep, raw-GUID removal
- `Triage/Details.cshtml` — 11 blocks
- `Cases/Create.cshtml` — 8 blocks
- `Administration/Index.cshtml` — 8 tile blurbs cut to short clauses
- `Administration/Automation/Index.cshtml` (+`.cshtml.cs`) — 7 blocks, `ingress`/`composed`/`correlation identifier` fixes
- `Cases/Assessment/Suggestions.cshtml` — 6 blocks, `ViewData["Lede"]` removed
- `ImageIntake/Details.cshtml` — 5 blocks, raw-GUID/hash/token leak removed (design :168)
- `Administration/Principals/Index.cshtml` — 5 blocks, `projection`/`bounded` fix
- `Operations/Index.cshtml` — `bounded` fix
- `Mail/Message.cshtml` — reviewed, no violations found (no changes needed)
- `Administration/Automation/Activity.cshtml` — `correlation identifier` fix (found via sweep)
- `Administration/Organizations/Edit.cshtml`, `Organizations/Index.cshtml` — `projection`/`bounded` fixes
- `Administration/Principals/Create.cshtml`, `Principals/Replace.cshtml` — `bounded` fix, `ViewData["Lede"]` removed (Create)
- `Cases/Details.cshtml` — `projection` fix
- `Cases/Shared/_CaseSummary.cshtml` — `artifact` fix
- `ImageIntake/Index.cshtml` — lede removed
- `Administration/Access/Index.cshtml`, `Accounts/Edit.cshtml`, `Configuration.cshtml`, `Roles/Index.cshtml` — multi-sentence guidance compressed
- `Cases/Index.cshtml` — `projection`/multi-sentence fix
- `Connect/Authorize.cshtml` — multi-sentence guidance compressed
- `Uploads/Request.cshtml` — multi-sentence guidance compressed
- `Shared/_PageHeader.cshtml` — lede slot removed (no caller passes one any more)
- `Administration/Principals/Create.cshtml` — `ViewData["Lede"]` removed

## Pages swept, no changes needed
`Account/*`, `Administration/Accounts/Index.cshtml`, `Cases/Custody.cshtml`,
`Cases/Documents/*`, `Cases/Eva/Download.cshtml`, `Cases/Tasks.cshtml`, `Cases/Vehicle.cshtml`,
`Cases/Workflow.cshtml`, `Cases/Shared/_CaseDocuments.cshtml`, `Cases/Shared/_CaseHistory.cshtml`,
`Search/Index.cshtml`, `Intake/Source.cshtml`, `Mail/Index.cshtml`, `Triage/Index.cshtml`,
`Shared/_ErrorSummary.cshtml`, `Shared/_FreshnessBanner.cshtml`, `Shared/_InstructionDraftFields.cshtml`,
`Shared/_Layout.cshtml`, `Shared/_LayoutAuth.cshtml`, `Shared/_LayoutExternal.cshtml`,
`Shared/_LucideSprite.cshtml`, `Shared/_MetricCard.cshtml`, `Shared/_Provenance.cshtml`,
`Shared/_ProvenancePanel.cshtml`, `Shared/_ReasonDialog.cshtml`, `Shared/_StatusChip.cshtml`,
`Uploads/_ViewStart.cshtml`, `Index.cshtml`.

## Excluded (owned elsewhere)
`Administration/Mailboxes.cshtml` (PLAT-009). `Unidentified/Index.cshtml`,
`Unidentified/Details.cshtml`, `Upload.cshtml`, `UploadStatus.cshtml`,
`UploadGroupStatus.cshtml` (does not exist in this checkout), `wwwroot/js/site.js`
(operator-directed structural rebuilds — new tickets; carved out mid-flight, edits reverted).

## Tests updated
- `tests/Pegasus.IntegrationTests/CaseReportApprovalWebTests.cs` — button label rename
- `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` — sentence-join casing
- `tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs` — removed-lede assertion replaced with the surviving field hint
