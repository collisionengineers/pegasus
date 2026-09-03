# Research — CASE-040 (2026-09-02, gpt-5.6-terra high, wrapper-checked)

## Scope and evidence

**VERIFIED** (`git rev-parse HEAD; git status --short; git log -1
--oneline`): this detached checkout is clean at `cad00be9`; no build or test
command was run.

**VERIFIED** (`rg -n -i 'signoff|sign-off|signatory|ReportEngineer|
AcceptedSignatories' src tests docs`): origin/dev has no Case sign-off field
or account flag. It has an assessment-report signatory implementation.

## Current Case and Engineer behaviour

**VERIFIED** (`git grep -n 'AssignedEngineerId' -- src tests`): the sole
persisted Case Engineer field is `AssignedEngineerId`, in
`src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs:100-111` and
`src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs:5-13`.

**VERIFIED** (`rg -n -C 6 'AssignCaseEngineer|AssignedEngineerId'
src/Pegasus.Core/Lifecycle/CaseLifecycle.cs
src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs
src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs`): the assignment path is
`WorkflowModel.OnPostAssignEngineerAsync`, `AssignCaseEngineer`, then
`EfCaseWorkflowStore.AssignEngineerAsync`, which writes
`AssignedEngineerId` at `EfCaseWorkflowStore.cs:479-484` (wrapper-confirmed:
`MutateAsync(request, "case_engineer_assigned", ...)`).

**VERIFIED** (`rg -n -C 5 'ICaseEngineerEligibility|CaseEngineerEligibility'
src/Pegasus.Core/Identity/CaseEngineerEligibility.cs
src/Pegasus.Infrastructure/Persistence/EfCaseEngineerEligibility.cs`):
Engineer eligibility is presently only account-exists, enabled, and Engineer
role. It has no sign-off capability.

**VERIFIED** (`rg -n -C 5 'staffAccountQueries|EngineerOptions'
src/Pegasus.Web/Pages/Cases/Details.cshtml.cs
src/Pegasus.Core/Identity/StaffAccountAdministration.cs`): the existing
Details handoff dialog gets Engineer choices from
`IStaffAccountQueries.ListAsync(0, 100)` and filters enabled accounts with the
Engineer role at `Details.cshtml.cs:477-481`. `Eva/Send` does not inject a
staff query and currently offers no Engineer selector.

**VERIFIED** (`rg -n -C 5 'EngineerDisplayName|AssignedEngineerId'
src/Pegasus.Web/Pages/Cases/Details.cshtml.cs
src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml`): Details resolves the
assigned Engineer display name through `IStaffAccountQueries.GetAsync` at
`Details.cshtml.cs:466-469`; Overview renders it at
`_CaseSummary.cshtml:27-31` (`<dt>Engineer</dt><dd>@(Model.EngineerDisplayName
?? "Unassigned")</dd>`).

## EVA handoff and state behaviour

**VERIFIED** (`rg -n -C 6 'canSendToEva|Download EVA package|Send to EVA'
src/Pegasus.Web/Pages/Cases/Details.cshtml`): the Case action is currently
available only in Review (`Details.cshtml:46-49`, `var canSendToEva =
isReview;`). It says "Download EVA package" after a bundle has already been
exported, but that condition is still inside the Review-only control
(`Details.cshtml:247-252`), not With Engineer or Complete.

**VERIFIED (wrapper)** (`sed -n '561,620p' Details.cshtml`): the script
EVA handoff dialog (`eva-handoff-dialog`) is markup inside `Details.cshtml`
itself, not a partial: an Engineer `<select name="engineerId">` posting
"Assign Engineer" to the Workflow page, then the export form and (when
`Model.CanSubmitToEva`) the API form posting to `/Cases/Eva/Send`. That file
is CASE-038's. CASE-040's dialog work therefore lands in `Eva/Send.*` (the
script-off route that the dialog mirrors) and the frame lane must either host
the sign-off select in the dialog from CASE-040's view-model values or hand
the dialog block over; the plan must name which.

**VERIFIED** (`rg -n -C 6 'OnGet|OnPost|CanSubmitToApi|LastSubmission'
src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml.cs`): `Eva/Send` is a Review-only
page (`Send.cshtml.cs:67-72`) that presents Download export and, when enabled,
Send via API. Its GET records nothing; Download posts to
`/Cases/Documents/Export` handler `Bundle` (`Send.cshtml:78-79`); API posts
`ISubmitCaseToEva` (`OnPostSubmitAsync`, `Send.cshtml.cs:91`).

**VERIFIED** (`rg -n -C 5 'AllowsManualSubmission|AlreadySubmitted'
src/Pegasus.Core/Eva/EvaSubmissionPolicy.cs
src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml.cs`): API availability requires a
composed submitter, the principal's manual toggle, and no delivered prior
submission. A delivered API submission is refused as already submitted.

**VERIFIED** (`rg -n -C 5 'Review|does not change the Case state'
docs/frd/frd-07-eva-and-external-engineering-handoff.md`): FRD-07 currently
requires Review and says API submission records history without changing Case
state at `frd-07-eva-and-external-engineering-handoff.md:118-120`.

**VERIFIED** (`rg -n -C 5 'StartCaseWork|ReportPreparation'
src/Pegasus.Core/Lifecycle/CaseLifecycle.cs
src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs`): the existing Review to
With Engineer transition is the separate `StartCaseWork` action; it requires
an assigned Engineer and changes state to `ReportPreparation` at
`CaseLifecycle.cs:115-129`. Sending to EVA does not perform this transition.

**VERIFIED** (`rg -n -C 5 'SendToEvaRendersOnlyInReview|SendPageRenders'
tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`): current Web tests
(`SendToEvaRendersOnlyInReview` line 64, `SendPageRendersItsChoiceForAReviewCase`
line 331) assert that the handoff appears only in Review and that the Send
page offers the two routes. They do not cover sign-off selection or re-send
from With Engineer.

## Report signatory

**VERIFIED** (`rg -n -C 6 'TryResolveAcceptedEngineer|EngineerSignature'
src/Pegasus.Core/Reports/AssessmentReportProjection.cs
tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs`): report
projection validates an exact accepted Engineer tuple. The current usable tuple
is `A Patterson | M.Inst.IAEA | andy_patterson`
(`AssessmentReportRendering.cs:163`); report tests reject mismatched tuples.

**VERIFIED** (`rg -n -C 4 'signature|qualifications|engineer'
src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`):
the renderer consumes one `ReportEngineer` tuple at
`PlaywrightAssessmentReportRenderer.cs:56-58`. DOCS-017 must replace that
fixed dictionary and project the Case-selected sign-off account into it.

**VERIFIED** (`rg -n -C 4 'A Patterson|D18|DOCS-017'
docs/capabilities.md docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`):
the documentation explicitly defers future signature-policy work to DOCS-017.
CASE-040 should persist only `SignOffEngineerId`; DOCS-017 owns rendering the
account's name, qualifications, and signature.

## Persistence and migrations

**VERIFIED** (`rg -n -C 4 'CaseWorkflowEntity|AssignedEngineerId'
src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs
src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs
src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs`): the required
persistence seam exists. CASE-040 must add a nullable account identity to the
entity, Core record, query mapping (`EfCaseQueryStore.cs:247,388` project
`AssignedEngineerId` today), mutation mapping, and workflow history value;
`CaseWorkflowModelConfiguration.cs` has no explicit mapping for the existing
Engineer column, so convention maps it.

**VERIFIED** (`rg --files src/Pegasus.Infrastructure/Persistence/Migrations |
rg '20260829.*\.cs$' | rg -v 'Designer'`): the newest non-designer migration
is `20260829212237_GrantProviderSubmissionAcceptRecovery.cs`.

**VERIFIED** (`rg -n -C 4 'CaseWorkflows'
src/Pegasus.Infrastructure/Persistence/Migrations/20260729176000_AzureSqlRuntimeLeastPrivilege.cs
src/Pegasus.Infrastructure/Persistence/Migrations/20260729199000_RuntimeRoleReconciliation.cs`):
`CaseWorkflows` has table-level Web `SELECT, INSERT, UPDATE` and Worker
`SELECT, UPDATE` grants. A new column on that existing table needs no further
grant.

**VERIFIED** (`rg -n -C 4 'CreateTable|GRANT|no-runtime-grant'
scripts/Test-MigrationGrants.ps1`): the grant check inspects all migrations
and requires a grant only for newly created tables. The CASE-040 column
migration must retain the normal generated migration, designer, and model
snapshot updates, but needs no grant-only companion migration.

## Presentation labels and frame

**VERIFIED** (`rg -n 'OperatorLabels\.' src/Pegasus.Web/Pages/Cases/Details.cshtml
src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml
src/Pegasus.Web/Presentation/OperatorLabels.cs`): current Case text is mostly
literal; there is no `OperatorLabels.Cases` class. **Wrapper correction:**
`OperatorLabels.CaseWorkspace` (line 1297) and `OperatorLabels.EvaHandoffs`
(line 1059) already exist, and `CaseStage(string?)` is a method, not a class.
One list per concept: CASE-040 adds its keys to `OperatorLabels.CaseWorkspace`
(or `EvaHandoffs` for the dialog route words) rather than creating a new
class. Keys needed:

- `Engineer`
- `SignOffEngineer`
- `SendToEva`
- `DownloadZip`
- `SendViaApi`

**VERIFIED** (`rg -n -C 4 'case-details--default|case-eva-send--default'
docs/design/test-ui/catalogue.json`): the current visual snapshots are
`pages/case-details--default.html` (plus `--unavailable`, `--conflict`) and
`pages/case-eva-send--default.html` (catalogue lines 324-369). The current
Case Details default specifically covers the identity ribbon and action bar.

**ASSUMED**: CASE-038 will expose a named sign-off ribbon/current-position
slot in `Details.cshtml` and a corresponding Details view-model value before
CASE-040 starts. CASE-040 then supplies the persisted account identity and
display projection through that slot, without editing CASE-038-owned Details
files. Origin/dev contains no such slot.

## Mockup findings

**VERIFIED** (`rg -n -C 5 'signoffEngineers|defaultSignoff'
C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/05-state.js`): eligible sign-off
Engineers are enabled Engineers with a signature. The default is the selected
Engineer when eligible, else mockup username `a.patterson`, then the first
eligible sign-off Engineer, then empty.

**VERIFIED** (`rg -n -C 5 'case-eva|case-eva-save|eva-engineer'
C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/20-case.js`): the mockup dialog
selects Engineer and Sign-off Engineer, offers Download ZIP or Send via API,
moves Review to With Engineer on send, and records route, Engineer, Sign-off
Engineer, and reason in a new handoff/history entry.

**VERIFIED** (`rg -n -C 4 'Sign-off Engineer|engineer-change'
C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/21-case-sections.js`): Overview
shows a Sign-off Engineer field beside Engineer, editable only in edit mode;
changing Engineer re-derives the default.

**VERIFIED** (`rg -n -C 4 'signs|Patrick Rooney'
C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/04-fixtures.js`): A Patterson,
Ed Mawdsley, and Neil O'Reilly have signatures; Patrick Rooney is an Engineer
without one. The mockup stores names, whereas Pegasus must store account IDs.

## Gap list

**VERIFIED** (`git grep -n -I -e 'SignOffEngineerId' -e 'signoff' -- src
tests`): no Case sign-off account identity, selection command, default rule,
or sign-off-only query exists.

**VERIFIED** (`rg -n -C 4 'SendToEvaRendersOnlyInReview|AlreadySubmitted'
tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs
src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml.cs`): existing routes reject the
requested With Engineer re-send model: the UI is Review-only and delivered API
submissions are one-per-case.

**VERIFIED** (`rg -n -C 4 'First sent to Engineer|BundleExportedHistoryEventKind'
src tests`): existing ZIP export records a once-per-case first-send proxy, not
a new route-and-signatory handoff record on every send.

**VERIFIED** (`rg -n -C 4 'Download EVA package|Review|submitted'
docs/frd/frd-07-eva-and-external-engineering-handoff.md`): FRD-07 conflicts
with D36: it describes Review-only operation and non-resubmission. DELIV-041
must reconcile the passages at lines 8-20, 79-102, and 118-120 before CASE-040
can claim the new behaviour is governed (DELIV-041's post-implementation
report says FRD-07 was outside its file list and names CASE-040 or a follow-up
docs ticket to reconcile it).

## Reuse and risks

**VERIFIED** (`rg -n -C 4 'MutateAsync|AddEvent|HistoryValue'
src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs`): reuse
`EfCaseWorkflowStore.MutateAsync` and its replay/concurrency/event convention;
do not revive the migrations that dropped the earlier EVA-handoff tables.

**VERIFIED** (`rg -n -C 4 'IStaffAccountQueries|ListAsync|GetAsync'
src/Pegasus.Core/Identity/StaffAccountAdministration.cs`): reuse
`IStaffAccountQueries` for account identity/display resolution after PLAT-068
extends the returned account profile.

**VERIFIED** (`rg -n -C 4 'CaseTransitionDestination.ReportPreparation'
src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs`): reuse the existing
Start-work transition semantics when Send to EVA makes Review become With
Engineer; do not create a parallel lifecycle state.

**ASSUMED**: a separate Core command can atomically validate selected
Engineer/sign-off accounts, persist both identities, record a route-specific
handoff event, and perform the existing Review-to-ReportPreparation transition.
The present contracts lack that compound command.

**ASSUMED**: API re-send requires a new semantics distinct from the current
EVA API delivery uniqueness constraint. ZIP can record repeat handoffs, but
the operator has not supplied whether an already-delivered API case may be
sent again, retried, or must offer ZIP only.

## Open questions

- [ ] Operator: what durable rule identifies the fallback A Patterson account:
  a reserved username `a.patterson`, an explicit account setting, or another
  immutable account identity? The current repository has only the fixed report
  tuple; the mockup's username is not a persisted Pegasus rule.

## Gate and dependency notes

**VERIFIED** (`rg -n -C 4 'CaseEngineerEligibility|IStaffAccountQueries'
src/Pegasus.Core/Identity src/Pegasus.Infrastructure/Persistence`): CASE-040
cannot prove the default rule or filter Sign-off Engineer choices until
PLAT-068 exposes the account flag/profile and migrates `AspNetUsers`.

**ASSUMED**: CASE-040 can prepare its Core workflow-record, command, and
persistence seams against that forthcoming profile contract, but cannot merge
a complete implementation before PLAT-068.

**VERIFIED** (`rg -n -C 4 'case-details--default|identity ribbon'
docs/design/test-ui/catalogue.json`): CASE-040 cannot prove the ribbon and
Current position render until CASE-038 owns and merges the frame/slot work.

**ASSUMED**: after CASE-038, CASE-040 supplies its value through that slot;
CASE-038 changes `Details.cshtml(.cs)`, while CASE-040 changes the Case
workflow projection and `_CaseSummary.cshtml`.

## Wrapper check (Claude, 2026-09-02)

Spot-checked in the main checkout at `cad00be9`: `EfCaseWorkflowStore.cs:
479-484`, `Details.cshtml.cs:466-481`, `CaseLifecycle.cs:115-129`, FRD-07
lines 118-120, `PlaywrightAssessmentReportRenderer.cs:56-58`, the
`CaseWorkflows` grants in both migrations, the two test names, the
`andy_patterson` tuple, the catalogue entries, and the three named test files
all confirmed. Corrected: the labels home (`OperatorLabels.CaseWorkspace`
exists; `CaseStage` is a method). Added: the EVA dialog markup lives in
CASE-038-owned `Details.cshtml`. Codex ran read-only in `.worktrees/research`;
the checkout was clean afterwards.
