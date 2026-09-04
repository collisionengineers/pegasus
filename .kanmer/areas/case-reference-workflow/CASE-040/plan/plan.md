# Plan — CASE-040 (2026-09-02, gpt-5.6-terra xhigh)

## Premise checks

| Status | Read-only command | Confirmed or corrected |
| --- | --- | --- |
| VERIFIED | `Get-Content -Raw CLAUDE.md; Get-Content -Raw AGENTS.md` | Repository and Kanmer conduct, the one-Core-owner rule, shared-lock rules, migration/grant requirements, and the required delivery boundary. |
| VERIFIED | `git rev-parse HEAD; git status --short; git log -1 --oneline` | The detached research checkout is clean at `897db953`, the DELIV-041 merge commit. |
| VERIFIED | `Get-Content` targeted ranges from `docs/frd/frd-01-case-identity-and-lifecycle.md`, `docs/frd/frd-07-eva-and-external-engineering-handoff.md`, `docs/frd/frd-04-parties-accounts-and-access.md`, and `docs/design/README.md` | D31/D36 are now governed: Sign-off Engineer is a Case field; manual API re-send in With Engineer is a new submission; sending does not change Case state or version. |
| VERIFIED | `rg -n -C 5 'canSendToEva|Download EVA package|eva-handoff-dialog|EngineerOptions' src/Pegasus.Web/Pages/Cases/Details.cshtml src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | The current action is Review-only, the retired label remains, and the dialog is inline in CASE-038-owned `Details.cshtml`. |
| VERIFIED | `rg -n -C 5 'CaseLifecycleState.Review|UX_EvaSubmissions_CaseDelivered|FindDeliveredAsync|RecordExportAsync|RecordSubmissionAsync' src/Pegasus.Core/Eva src/Pegasus.Infrastructure/Persistence` | Both routes currently require Review; the filtered delivered-submission index and store check reject re-send; each route already writes its own action-history record. |
| VERIFIED | `rg -n -C 4 'AssignedEngineerId|AssignCaseEngineer|CaseWorkflowRecord|HistoryValue' src/Pegasus.Core src/Pegasus.Infrastructure/Persistence` | The Case workflow record, entity, query projections, `MutateAsync`, and workflow-history convention are the correct persistence seams. |
| VERIFIED | `rg --files src/Pegasus.Core/Eva; rg -n 'class EvaHandoffPolicy' src/Pegasus.Core/Eva/EvaBundleSchema.cs` | The research path needs one correction: `EvaHandoffPolicy` is declared in `src/Pegasus.Core/Eva/EvaBundleSchema.cs`, not a separate policy file. |
| VERIFIED | `rg -n -C 3 'SendToEvaRendersOnlyInReview|SendPageRendersItsChoiceForAReviewCase|EvaSubmissionPersistence' tests` | Existing Web and persistence tests are the appropriate seams to extend. |
| ASSUMED | Supplied PLAT-068 plan and files document | PLAT-068 will expose eligible sign-off profiles and a non-hard-coded, Administrator-maintained default designation before CASE-040 begins. |
| ASSUMED | Supplied CASE-038 files document | CASE-038 will first merge the Case frame and its no-value Sign-off slot, then transfer only the listed dialog/action-bar portions of `Details.cshtml(.cs)` to CASE-040. |
| ASSUMED | Supplied DOCS-017 dependency | DOCS-017 will own report projection/rendering of the persisted Case sign-off tuple; CASE-040 will not alter report files. |

## Objective

Persist and present the Case Sign-off Engineer, using the eligible account
profiles supplied by PLAT-068. Replace the two divergent EVA handoff surfaces
with one shared form, offered in Review and With Engineer, without changing
Case state when a package is exported or an API submission is sent.

## Governing behaviour

- FRD-01 requires a Sign-off Engineer beside Engineer and the D31 default:
  eligible assigned Engineer first, otherwise the designated A Patterson
  account; account data is never hard-coded.
- FRD-07 requires Download ZIP in Review and With Engineer, plus a manual API
  re-send in With Engineer as a new submission with distinct outcome and EVA
  identifiers. Automatic submission remains once-only on entering Review.
- FRD-04 makes Sign-off Engineer an Administrator-managed account setting;
  CASE-040 consumes that setting and does not reimplement it.
- The design rules require concise settled labels, no explanatory copy, labels
  only in `src/Pegasus.Web/Presentation/OperatorLabels.cs`, exact state labels,
  and absence rather than an inert disabled capability.

## Dependencies

| Dependency | Must deliver first |
| --- | --- |
| [[PLAT-068]] | The `SignOffEngineerProfile`, `ListSignOffEngineersAsync`, eligibility rule, account migration, and an Administrator-maintained default designation exposed on the profile. CASE-040 must not hard-code A Patterson, a username, or an ID. |
| [[CASE-038]] | The merged Case workspace frame, Sign-off ribbon/current-position slot, and release of the narrow action-bar/dialog regions in `Details.cshtml(.cs)`. CASE-040 then owns only the `canSendToEva` condition, retired action label, shared partial host, and corresponding view-model values. |
| [[PLAT-070]] | D44's deletion of the staff review function. `CaseWorkflowContracts.cs` (`InstructionsReviewedByStaff` / `ImagesReviewedByStaff`), `CaseLifecycle`'s configured review clause in `ValidateReadiness`, and `Workflow.cshtml.cs:98`'s four review parameters are PLAT-070's to remove; CASE-040 changes the same three files and must build on the post-PLAT-070 shapes. Moving the Engineer form into the shared partial before PLAT-070 lands would carry the retired review inputs forward, against D44. |
| [[DOCS-017]] | The report-signatory seam that consumes a Case-selected account tuple rather than the fixed report tuple. CASE-040 persists only the account identity and does not modify report projection, renderer, template, or FRD-11 files. |

The implementation starts only after those dependencies have landed, the
migration lane is free after PLAT-068, and the CASE-040 branch has refreshed
from `origin/dev`.

## Resolved implementation decisions

- The Core resolver receives the persisted Sign-off Engineer ID, assigned
  Engineer ID, and PLAT-068 eligible profiles. It returns: persisted eligible
  selection; otherwise assigned eligible Engineer; otherwise the profile marked
  as default; otherwise no selection. This is the sole default rule.
- Assigning an Engineer derives and persists the default in the existing
  assignment mutation. A separate reasoned Sign-off Engineer mutation validates
  an explicit eligible selection in Review, ReportPreparation, or PostReport.
  It is unavailable in Complete.
- The shared EVA handoff partial has exactly two callers: the Case dialog and
  the script-off `Eva/Send` page. It exists to prevent a second field/control
  list. The Case dialog is its editable host; the script-off route presents
  persisted values and the same two route choices without creating a second
  edit-lease mechanism.
- Engineer remains a lease-backed selector only in Review. In With Engineer it
  is shown as the assigned value. Sign-off selection uses its own reasoned,
  lease-backed workflow action in Review and With Engineer.
- Export and API submission are not bundled with the Sign-off mutation. Once
  the selection is persisted, each route remains a no-state/no-version handoff
  action. This preserves FRD-07's state rule. The Review to With Engineer
  transition stays the separate `StartCaseWork` action unless the operator
  answers the open question below the other way.
- The precondition "this case has an eligible resolved Sign-off Engineer" is a
  Core rule, held once in the EVA handoff/submission policy and consumed by
  `EvaHandoffStore` and `EvaSubmissionStore` beside their existing state and
  image gates. The pages present that decision; they never are it. A direct
  POST to the export or submit handler on a case with no eligible sign-off is
  refused in the store, not merely hidden in the view.
- The EVA routes stop throwing `CaseNotInReviewException`
  (`src/Pegasus.Core/Documents/DocumentContracts.cs:238`, whose message states
  the Review-only rule and which ordinary document export also throws). The
  centralized EVA state policy returns its own accurate refusal for a case
  outside the permitted handoff states; the document-export use of the existing
  exception is left exactly as it is.
- Each route snapshots the assigned and Sign-off Engineer IDs in its existing
  action-history payload. `eva_bundle_exported` remains one history row per
  successful export, and `eva_api_submitted` remains one row and one
  `EvaSubmissions` row per API attempt. No new handoff table is introduced.
- The filtered unique delivered-submission index and its pre-flight lookup are
  removed. Manual API sends in With Engineer may therefore create a second
  claim. Automatic submission remains restricted to Review and is still
  once-only; the existing `Unknown` retry rule remains unchanged.
- A composed API route disabled by a Principal setting renders disabled, as
  D36 requires. If the API transport is not composed, it is absent. Download
  ZIP remains an enabled route only when the Case is in a permitted handoff
  state and has eligible images.

## Expected files

| Files | Action |
| --- | --- |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs`<br>`src/Pegasus.Core/Lifecycle/CaseLifecycle.cs` | Change Case workflow contracts, store port, assignment defaulting, and explicit Sign-off mutation. |
| `src/Pegasus.Core/Eva/EvaBundleSchema.cs`<br>`src/Pegasus.Core/Eva/EvaSubmissionPolicy.cs`<br>`src/Pegasus.Core/Eva/EvaApiContracts.cs`<br>`src/Pegasus.Core/Eva/EvaSubmissionWorkItem.cs` | Centralize manual versus automatic state gates and the resolved-sign-off precondition; add the EVA state refusal that replaces `CaseNotInReviewException` on these two routes; remove the superseded one-delivery exception path. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register the new Core Sign-off action as a production caller. |
| `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs`<br>`src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs`<br>`src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | Persist, project, replay-protect, and history-record `SignOffEngineerId`. |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs`<br>`src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs`<br>`src/Pegasus.Infrastructure/Persistence/EvaSubmissionModelConfiguration.cs` | Permit manual With Engineer handoffs, snapshot identities in route history, and remove the delivered-row uniqueness rule. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseSignOffEngineer.cs`<br>`src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseSignOffEngineer.Designer.cs`<br>`src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Add nullable `CaseWorkflows.SignOffEngineerId` and drop `UX_EvaSubmissions_CaseDelivered` in the single CASE-040 migration. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Add only missing labels, all in `OperatorLabels.CaseWorkspace`, including the exact `Unassigned` no-value label; delete `EvaSubmissionPolicy.NotEnabledReason`'s duplicate wording home. |
| `src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs` | Bind the reasoned Sign-off selection handler. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` | Render Sign-off Engineer beside Engineer in Overview. |
| `src/Pegasus.Web/Pages/Cases/Shared/EvaHandoffViewModel.cs`<br>`src/Pegasus.Web/Pages/Cases/Shared/_EvaHandoff.cshtml` | Create the two-caller view model and shared handoff partial. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml`<br>`src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | After CASE-038, host the partial, supply resolved values/options, offer both permitted states, and retire the Download EVA package switch. |
| `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml`<br>`src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml.cs` | Render the same partial for script-off use and admit Review/With Engineer. |
| `tests/Pegasus.Core.Tests/Lifecycle/AssignCaseEngineerTests.cs`<br>`tests/Pegasus.Core.Tests/Qdos/EvaSubmissionPolicyTests.cs` | Test Core default/selection and manual-versus-automatic EVA state policy. |
| `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs`<br>`tests/Pegasus.IntegrationTests/EvaSubmissionPersistenceTests.cs`<br>`tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | Test persisted identity/history, distinct delivered re-send rows driven through `ISubmitCaseToEva` itself, and rendered routes/controls. |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | The bundle-export state, locked-state race, replay, first-send proxy and history assertions live here (lines 795, 1277 and 1436 assert `CaseNotInReviewException`). Prove With Engineer export through `IExportCaseBundle` and move those three refusal assertions to the new EVA state refusal. |
| `tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs` | Bind and prove the new reasoned Sign-off handler on the Workflow page. |
| `docs/design/test-ui/pages/case-eva-send--default.html`<br>`docs/design/test-ui/pages/case-details--default.html` | Regenerated only by the required snapshot capture after the shared partial and Details host change. `catalogue.json` remains unchanged. |

## Must not modify

- PLAT-068-owned identity, administration, staff query, `AspNetUsers`, and
  account-setting files.
- DOCS-017-owned report projection, rendering, templates, and report FRD.
- CASE-038-owned frame, navigation, CSS, JavaScript, and all
  `Details.cshtml(.cs)` regions outside the transferred EVA dialog/action-bar
  block.
- Engineer-section files owned by ENG-034, CASE-029, and CASE-039.
- Governing documents, including FRD-01, FRD-04, and FRD-07.
- Any Test UI catalogue file other than the two capture-generated snapshots.

### Step 1 — Confirm the merge and lock boundary

- **Files:** none.
- **Reuse:** Kanmer dependency records, the migration serialization rule, and
  `git merge --no-edit origin/dev`.
- Confirm PLAT-068's profile/default contract, PLAT-070's completed deletion,
  CASE-038's transferred dialog boundary, DOCS-017's report seam, and the
  available migration/shared-file locks before taking implementation work.
- Record the CASE-038 transfer in writing on the ticket before the first
  commit: the exact `Details.cshtml` / `Details.cshtml.cs` regions CASE-040
  owns (the action-bar `canSendToEva` branch and the `eva-handoff-dialog`
  block), and that every other region of those two files stays CASE-038's.
  Without that written grant the Details work does not start.
- Serialize the migration behind the other lanes holding the
  `Persistence/Migrations/**` shared lock — PLAT-068, PLAT-070, [[CASE-039]]
  and [[CASE-041]] each carry one — and take the `OperatorLabels.cs`,
  `Pages/Cases/Shared/*` and `docs/design/test-ui/**` locks one at a time.
- After the refresh, confirm
  `git grep -i "ReviewedByStaff\|RequireStaffImageReview"` returns nothing, so
  no retired review input is carried into the assignment contract, the Workflow
  handler, or the shared handoff partial (D44).
- Stop and report if the profile lacks a durable default designation, if
  PLAT-070 has not landed, if CASE-038 has not released the narrow Details
  region in writing, or if another unmerged migration occupies the lane.

### Step 2 — Add the Case Sign-off Engineer Core action

- **Files:** `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs`,
  `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs`,
  `src/Pegasus.Infrastructure/DependencyInjection.cs`.
- **Reuse:** `CaseWorkflowRecord`, `AssignCaseEngineerRequest`,
  `IAssignCaseEngineer`, `CaseLifecycleRules`, `CaseEngineerEligibilityPolicy`,
  `ICaseWorkflowStore`, and existing scoped Core registrations.
- Append nullable `SignOffEngineerId` to the Case workflow contract and add
  the smallest store/action contract for explicit Sign-off Engineer selection.
- Keep the resolver and eligibility validation in `CaseLifecycle.cs`. It uses
  PLAT-068's `ListSignOffEngineersAsync` profiles; it has no account data,
  usernames, GUIDs, or fallback list of its own.
- Extend existing Engineer assignment to persist the derived selection in the
  same replay-safe mutation. Add the explicit, reasoned, lease-backed selection
  action for Review, ReportPreparation, and PostReport only.
- Register the new action in Infrastructure so
  `WorkflowModel.OnPostSetSignOffEngineerAsync` is a production caller.

### Step 3 — Persist the identity and amend EVA handoff policy

- **Files:** `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs`,
  `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs`,
  `src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs`,
  `src/Pegasus.Infrastructure/Persistence/EvaSubmissionModelConfiguration.cs`,
  `src/Pegasus.Core/Eva/EvaBundleSchema.cs`,
  `src/Pegasus.Core/Eva/EvaSubmissionPolicy.cs`,
  `src/Pegasus.Core/Eva/EvaApiContracts.cs`,
  `src/Pegasus.Core/Eva/EvaSubmissionWorkItem.cs`.
- **Reuse:** `EfCaseWorkflowStore.MutateAsync`, `HistoryValue`,
  `DocumentActionHistory.Succeeded`, `EvaHandoffPolicy`,
  `EvaSubmissionPolicy`, per-operation replay, and
  `EvaFirstHandoffProxies`.
- Add nullable `SignOffEngineerId` to the workflow entity and both workflow
  projections. Include it in before/after workflow history and exact replay.
- Make the Core EVA policy distinguish manual handoffs allowed in Review and
  With Engineer from automatic submission allowed only in Review, and hold the
  "eligible resolved Sign-off Engineer" precondition in the same one place.
  Both persistence stores consume that policy — at the pre-flight check and
  again inside the serializable locked-state section of
  `EvaHandoffStore.RecordExportAsync` — rather than retaining duplicate
  Review-only checks or leaving the sign-off rule to the page.
- Stop throwing `CaseNotInReviewException` from `EvaHandoffStore.cs:73`,
  `EvaHandoffStore.cs:146` and `EvaSubmissionStore.cs:70`; return the new EVA
  state refusal instead. Leave `EfDocumentCustodyStore.cs:327` and the
  exception itself untouched, and widen the `EvaSubmissionWorkItem.cs:168`
  catch to the new type.
- Keep `EvaFirstHandoffProxies` as the once-only first-export proxy, while
  retaining the existing per-export history row. Add assigned/sign-off IDs to
  the existing export and submission history payloads so every route records
  the identities current at that handoff.
- Remove `FindDeliveredAsync`, `EvaAlreadySubmittedException`, the page
  handling that depends on it, and `UX_EvaSubmissions_CaseDelivered`. Preserve
  operation-key replay, automatic work-row once-only behaviour, and
  `Unknown`-only retries.

### Step 4 — Create the one handoff presentation path

- **Files:** `src/Pegasus.Web/Presentation/OperatorLabels.cs`,
  `src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs`,
  `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml`,
  `src/Pegasus.Web/Pages/Cases/Shared/EvaHandoffViewModel.cs`,
  `src/Pegasus.Web/Pages/Cases/Shared/_EvaHandoff.cshtml`,
  `src/Pegasus.Web/Pages/Cases/Details.cshtml`,
  `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`,
  `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml`,
  `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml.cs`.
- **Reuse:** `IStaffAccountQueries.GetAsync`,
  `ListSignOffEngineersAsync`, the existing `AssignEngineer` handler,
  `CaseMutationPageModel`, the existing Export and Submit route handlers, and
  `OperatorLabels.CaseWorkspace`.
- Add only missing labels to `OperatorLabels`, all of them in
  `OperatorLabels.CaseWorkspace`: `OperatorLabels.EvaHandoffs` is documented as
  the Operations panel's list (PLAT-049), not the Case dialog's vocabulary.
  Render no newly introduced literal label, raw state, account ID, or
  explanatory sentence.
- When the API-disabled condition wording moves to `OperatorLabels`, delete
  `EvaSubmissionPolicy.NotEnabledReason` and point its only caller
  (`Send.cshtml.cs:158`) at the label. One wording, one home; do not leave a
  Core copy beside a label copy.
- Resolve the stored/default Sign-off Engineer display value in the Details
  model. Supply it to CASE-038's ribbon/current-position slot and render it
  beside Engineer in `_CaseSummary.cshtml`.
- Create the small shared view model and partial because Details and `Eva/Send`
  are its two concrete callers. Move the inline dialog's Engineer, Sign-off
  Engineer, Download ZIP, and Send via API controls into that partial.
- In Review, retain the existing lease-backed Engineer selector. In With
  Engineer, render Engineer as a value. Render the Sign-off mutation only in
  its permitted states and only with its named handler; it is absent in
  Complete.
- Replace the Review-only `canSendToEva` branch with the shared Core policy.
  Always label the action `Send to EVA`; remove the Download EVA package
  conditional and action text.
- Render Send via API disabled only for a composed transport whose Principal
  setting forbids manual submission. Omit it when the API is not composed.
  Present the Core precondition that an eligible resolved Sign-off Engineer
  exists; the refusal itself lives in the stores (Step 3), never only here.
- The script-off route uses the same partial and routes, admits Review and
  With Engineer, and does not invent a parallel selection or lifecycle action.

### Step 5 — Generate the single migration

- **Files:** `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseSignOffEngineer.cs`,
  `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseSignOffEngineer.Designer.cs`,
  `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`.
- **Reuse:** the repository's EF migration pair and current model snapshot.
- Generate one migration after PLAT-068's `AspNetUsers` migration. It adds
  nullable `CaseWorkflows.SignOffEngineerId` and drops the filtered unique
  delivered-submission index.
- Do not add a Case-workflow foreign key or speculative index: the existing
  assigned Engineer field has neither, and no current query requires one.
- Do not add grants. The migration creates no table, and existing runtime
  permissions already cover both changed tables.

### Step 6 — Prove the behaviour at Core, persistence, and Web boundaries

- **Files:** `tests/Pegasus.Core.Tests/Lifecycle/AssignCaseEngineerTests.cs`,
  `tests/Pegasus.Core.Tests/Qdos/EvaSubmissionPolicyTests.cs`,
  `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs`,
  `tests/Pegasus.IntegrationTests/EvaSubmissionPersistenceTests.cs`,
  `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`,
  `tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs`,
  `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`.
- **Reuse:** existing workflow harnesses, assignment eligibility fake, EVA
  persistence database fixture, `RecordingCaseDetailsStore`, and existing
  submission stubs.
- Extend assignment tests for flagged assigned Engineer, unflagged Engineer
  resolving to the designated default, explicit eligible selection, invalid
  selection, and exact replay.
- Extend persistence tests for the nullable Case column, workflow history,
  action-history handoff identity snapshots, and a With Engineer API re-send
  producing a second delivered submission without changing state.
- Replace assertions that a second delivered row is refused with assertions
  that two distinct manual operation keys can record distinct delivered rows.
  Retain automatic once-only and Unknown-only retry coverage.
- Update Web tests so Send to EVA appears in Review and With Engineer, the
  retired label is absent, the Sign-off field/options render correctly, and
  the route choices obey composition and Principal-setting rules.
- Prove the Core precondition at the port, not the page: a direct
  `IExportCaseBundle` or `ISubmitCaseToEva` call on a case with no eligible
  resolved Sign-off Engineer is refused. `EvaSubmissionPersistenceTests`
  manipulates entities directly today, so the re-send case must be driven
  through `ISubmitCaseToEva` to prove the real path and an unchanged workflow
  state and version.
- `CaseTaskArchivePersistenceTests.cs` and `UploadConfirmationWebTests.cs`
  resolve the production `ICaseWorkflowStore` and implement no fake, so they
  are not in the expected diff. Touch them only if the compiler proves
  otherwise.

### Step 7 — Capture UI evidence and complete delivery checks

- **Files:** `docs/design/test-ui/pages/case-eva-send--default.html`,
  `docs/design/test-ui/pages/case-details--default.html`.
- **Reuse:** the existing `case-eva-send--default` catalogue scenario and the
  repository snapshot scripts.
- Run the required capture after routed Razor changes. Review generated diffs
  for the two shared-host surfaces only; `catalogue.json` needs no scenario
  change.
- Verify all controls have named handlers, labels are sourced from
  `OperatorLabels`, state words remain exact, and no explanatory or
  how-it-works copy was introduced.

## Acceptance conditions

- A flagged assigned Engineer becomes the persisted Sign-off Engineer; an
  unflagged assigned Engineer resolves to the designated default profile.
- No Sign-off Engineer account, username, GUID, qualification, or signature is
  hard-coded in CASE-040.
- Only PLAT-068 eligible profiles are selectable. An ineligible persisted
  selection resolves through the same Core default rule; no eligible result
  displays the exact `Unassigned` state.
- The Case ribbon/current-position slot, Overview, and EVA handoff display the
  same resolved Sign-off Engineer value.
- Send to EVA appears in Review and With Engineer, not Complete. The retired
  Download EVA package action and label do not render.
- Engineer is editable only in Review with the existing edit lease. Sign-off
  mutation is a named reasoned action in Review and With Engineer; it is not an
  inert or disabled control in Complete.
- Export and manual API submission accept Review; manual re-send accepts
  ReportPreparation and PostReport; automatic submission remains Review-only.
- A With Engineer API re-send creates a new `EvaSubmissions` row, action-history
  row, and EVA outcome/identifiers without changing the Case state or version.
- Each successful export and API attempt records the selected Engineer and
  Sign-off Engineer identities in its existing route-specific history payload.
- A composed but Principal-disabled API route is disabled; an uncomposed API
  route is absent. Download ZIP remains separately available when permitted.
- The migration contains no new table grant requirement, and the migration
  grant script passes.
- The shared partial has exactly the Details dialog and `Eva/Send` page as
  callers, with no duplicate handoff field/control list.
- No retired staff-review flag, field, parameter or history value survives in
  any file CASE-040 touches (D44).
- A direct POST to the export or submit handler is refused by the store when
  the case has no eligible resolved Sign-off Engineer.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2
./scripts/Test-MigrationGrants.ps1
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

## Simplification pass (2026-09-02)

To be recorded by the implementer before the PR opens.

## Stop condition

Open the CASE-040 PR targeting `dev`, record the implementation evidence, and
move the ticket to Review. Do not merge the PR or begin another ticket.

## Wrapper checks (Claude, 2026-09-02, research checkout at 897db953)

Codex ran read-only in `.worktrees/research` (`git status --porcelain` empty
before and after); prompt and raw output are in the session scratchpad
`prep/CASE-040/plan-prompt.md` and `plan-out.md`. Confirmed by independent
read at `897db953`: `EvaHandoffPolicy` is declared in
`src/Pegasus.Core/Eva/EvaBundleSchema.cs:101` (the research's separate
policy-file path was wrong); the Review gates the plan opens are
`EvaHandoffStore.cs:71` and `:143` (export) and `EvaSubmissionStore.cs:68`
(API); the one-delivery rule is `FindDeliveredAsync`
(`EvaSubmissionStore.cs:172-179`) plus the filtered unique index
`UX_EvaSubmissions_CaseDelivered` in `EvaSubmissionModelConfiguration.cs:53`
(created by `20260827143132_EvaApiSubmissions.cs:77`); the API history kind
is `eva_api_submitted` (`EvaSubmissionStore.cs:226`);
`tests/Pegasus.Core.Tests/Qdos/EvaSubmissionPolicyTests.cs`,
`CaseTaskArchivePersistenceTests.cs` and `UploadConfirmationWebTests.cs`
exist, but the claim that the latter two carry `ICaseWorkflowStore` fakes was
wrong and is corrected by finding 6 below;
`IAssignCaseEngineer` is registered at `DependencyInjection.cs:392`;
`AssignCaseEngineer` refuses assignment outside Review
(`CaseLifecycle.cs:83-86`). DELIV-041 (#647 = 897db953) has already
reconciled FRD-07, FRD-01 and FRD-04 with D31/D36, so the research's
"FRD-07 conflicts with D36" gap is closed and no docs follow-up is planned.

Wrapper additions to Step 4: the disabled Send via API control for a
composed transport whose Principal has not enabled manual submission uses
the existing `.gated` span with a `data-condition` label from
`OperatorLabels` (the `_CaseVehicle.cshtml:90-103` /
`CaseWorkspace.ExperianSeamCondition` convention, PLAT-061: a `.gated`
without `data-condition` paints an empty pill); the condition text is the
existing `EvaSubmissionPolicy.NotEnabledReason` wording moved to
`OperatorLabels`, not a second sentence. The file list above extends the
research files document: `Core/Eva/*`, `EvaHandoffStore.cs`,
`EvaSubmissionStore.cs`, `EvaSubmissionModelConfiguration.cs`,
`DependencyInjection.cs`, the new `Pages/Cases/Shared/_EvaHandoff.cshtml` +
view model, and the narrow `Details.cshtml(.cs)` regions transferred from
CASE-038 after its merge — none is claimed by another EPIC-012 lane
(checked against the CASE-038, PLAT-068 and DOCS-017 file lists).

Open on the ticket: the fallback A Patterson identity. The plan's default
(an Administrator-maintained Default designation on one flagged account,
exposed on PLAT-068's `SignOffEngineerProfile`) keeps the rule in one Core
resolver and hard-codes nothing; the operator's answer changes only that
resolver and the PLAT-068 dependency line.

## Resolutions (2026-09-03)

- Operator: the default Sign-off Engineer is the one flagged account carrying
  the Administrator-set "Default sign-off Engineer" designation on
  PLAT-068's `SignOffEngineerProfile`; the Core resolver reads it. No
  reserved username. Open question ticked.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

gpt-5.6-sol read the plan independently at `897db953` in the detached research
checkout (read-only; clean before and after). Verdict: REQUEST CHANGES, six
findings. Four further findings came from the wrapper's own read. Every line
cited below was re-checked by the wrapper in the same checkout.

| # | Severity | Step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker (sol) | 1-7 | The planned diff reaches beyond the dispatch brief's owned paths: `Core/Workflow`, `Core/Lifecycle`, `Core/Eva`, non-migration Infrastructure persistence, `DependencyInjection.cs`, `OperatorLabels.cs`, `Workflow.cshtml.cs`, the new shared partial and the `Details.cshtml(.cs)` regions. Migrations and shared UI paths need serialized locks. | Fixed in part; remedy rejected in part. The brief's owned-path list is explicitly approximate, and every file the plan names was checked against the actual file lists of CASE-038, CASE-039, CASE-041, CASE-029, CASE-042, PLAT-069, CASE-009, PLAT-068 and DOCS-017 — none is claimed twice, and `AssignedEngineerId` already lives in `Core/Workflow` and `Core/Lifecycle`, so the sign-off field cannot sit in `Core/Cases` without a second owner of one concept. Splitting into prerequisite tickets is therefore rejected. Step 1 now requires the CASE-038 transfer in writing before the first commit and names PLAT-068, PLAT-070, CASE-039 and CASE-041 as the migration-lane peers to serialize behind. |
| 2 | blocker (sol) | 1, 2, 4 | PLAT-070 is not a dependency, yet it deletes the staff-review members of the very contracts CASE-040 extends. Verified: `CaseWorkflowContracts.cs` carries `InstructionsReviewedByStaff`/`ImagesReviewedByStaff`, `Workflow.cshtml.cs:98` takes four review parameters, and `CaseLifecycle.ValidateReadiness` still applies the configured flags. Moving that form into the shared partial first would carry retired inputs forward, against D44. | Fixed. PLAT-070 added to the dependency table; Step 1 makes it a must-land prerequisite and adds the `ReviewedByStaff` / `RequireStaffImageReview` grep after the refresh. A matching checklist line was added. |
| 3 | blocker (sol) | 3, 4 | The "eligible resolved Sign-off Engineer" precondition was stated only as a presentation rule, while the export and submit handlers accept direct POSTs and the stores gate only state, settings and images — Core would not own the policy. | Fixed. The precondition now lives once in the Core EVA policy and is consumed by `EvaHandoffStore` (pre-flight and inside the serializable locked-state section) and `EvaSubmissionStore`; the pages present it only. Port-level refusal tests added to Step 6 and to the acceptance conditions. |
| 4 | should-fix (sol) | 6 | The test map omitted the seams the change actually moves: `CustodyOutboxIntegrationTests.cs` holds the export state, locked-state race, replay, proxy and history assertions, and `CaseWorkflowWebTests.cs` is where a new Workflow handler binds. `EvaSubmissionPersistenceTests` manipulates entities directly and never calls `ISubmitCaseToEva`, so changing its index assertions proves nothing about the real re-send path. | Fixed. Both files added to Step 6 and the expected-files table; the re-send case is now driven through `ISubmitCaseToEva`, and unchanged workflow state and version are asserted there. |
| 5 | should-fix (sol) | 3 | Opening the routes to With Engineer leaves `CaseNotInReviewException` (`DocumentContracts.cs:238`, message "A case can only be exported while it is in Review.") false. It is shared with ordinary document export (`EfDocumentCustodyStore.cs:327`), so redefining it would widen unrelated behaviour. | Fixed. Step 3 stops using it on the two EVA routes (`EvaHandoffStore.cs:73`, `:146`, `EvaSubmissionStore.cs:70`), returns the new EVA state refusal, leaves the document-export use and the exception itself alone, and widens the `EvaSubmissionWorkItem.cs:168` catch. The three `CustodyOutboxIntegrationTests` assertions move with it. |
| 6 | should-fix (sol) | 6 | `CaseTaskArchivePersistenceTests.cs:761` and `UploadConfirmationWebTests.cs:275` resolve the production `ICaseWorkflowStore`; neither implements a fake, so the planned "test fake" edits have no subject. The earlier wrapper check that said they carry fakes was wrong. | Fixed. Both removed from the expected diff; Step 6 now says to touch them only if the compiler proves otherwise. |
| 7 | blocker (wrapper) | 3, 4 | Does the first Send to EVA move the case from Review to With Engineer? FRD-07 says twice that neither route changes the Case state or version (lines 63 and 131, reconciled by DELIV-041 after D36), and the plan follows it. But D44 as recorded on PLAT-070 says "Review to With Engineer happens through Send to EVA", and the mockup does exactly that (`20-case.js:190` sets the case state to `with_engineer` on save). The two cannot both hold, and the answer changes CASE-040's Core action. | Operator question. Added to `open-questions/` unticked. The plan keeps the FRD-07 reading (`StartCaseWork` remains the only transition) and now says so explicitly, so the ticket is implementable the moment the answer lands either way. |
| 8 | should-fix (wrapper) | Commands | The plan and checklist ran the solution suite as `Category!=Corpus&Category!=Browser`, which drops the whole Browser lane with nothing run in its place. CLAUDE.md's delivery gate is `Category!=Corpus`, and the runbook's complementary lanes are lines 324-325. CASE-040 changes routed Razor pages, which is what the Browser lane covers. | Fixed. The canonical solution filter is restored and the Browser integration lane added, in both the plan's Commands block and the checklist. |
| 9 | nit (wrapper) | 4 | The plan offered `OperatorLabels.EvaHandoffs` as an alternative home for the dialog's route words. That class is documented as the Operations panel's list (PLAT-049); putting Case dialog vocabulary in it makes one list serve two concepts. | Fixed. Step 4 now names `OperatorLabels.CaseWorkspace` as the single home. |
| 10 | should-fix (wrapper) | 4 | The wrapper addition moved the API-disabled wording into `OperatorLabels` but left `EvaSubmissionPolicy.NotEnabledReason` in place — two copies of one sentence. Its only caller is `Send.cshtml.cs:158`. | Fixed. Step 4 now deletes the Core constant and points that caller at the label. |

Not found, checked and clear: no step assumes a damage type (D45) and none
enters crop-tool scope (D46); no new package is proposed; the shared partial is
justified by its two concrete callers; every other reuse the steps name was
grepped and exists (`MutateAsync`, `HistoryValue`, `EvaFirstHandoffProxies`,
`EvaHandoffPolicy` at `EvaBundleSchema.cs:101`, `IStaffAccountQueries`,
`CaseMutationPageModel`, the `.gated` / `data-condition` convention at
`_CaseVehicle.cshtml:90-103`, `IAssignCaseEngineer` at
`DependencyInjection.cs:392`). PLAT-068's plan does define
`SignOffEngineerProfile`, `ListSignOffEngineersAsync` and `IsDefault`, so this
plan's dependency naming is accurate.

## Resolutions (2026-09-03) — D47, Send to EVA moves the case state

The operator answered the second open question: **Send to EVA moves the case
state, by either route, and FRD-07 is wrong.** Recorded as D47. These
amendments bind and take precedence over the plan above where they differ.

1. **Core action.** The Send to EVA command, from `Review`, performs the
   existing `StartCaseWork` transition to `With Engineer` in the same unit of
   work as the handoff record, whichever route is chosen (Download ZIP or
   Send via API). The transition is not a second, separate operator action.
2. **Atomicity.** If either half fails the whole command fails: the case
   stays in `Review`, no partial handoff is recorded, and the failure
   surfaces. No catch-all suppression.
3. **Re-send.** A send from `With Engineer` records the handoff and changes
   no state, as before.
4. **Governing document.** This PR amends
   `docs/frd/frd-07-eva-and-external-engineering-handoff.md`, replacing both
   statements that neither route changes the Case state or version (around
   lines 63 and 131) with the D47 rule, and cites D44 ("Send to EVA is the
   implicit review"). [[PLAT-070]] carries only the D44/D45 lines; the FRD-07
   correction belongs to the ticket that owns the action. Add the D47 line to
   the checklist's document step.
5. **Tests.** A Core test asserts the state change on a first send by each
   route, and that a failed handoff leaves the case in `Review`. No existing
   assertion that the state is unchanged survives — it is corrected, not
   deleted, and the correction is named in the post-implementation report.

## Resolutions (2026-09-04) — report generation wiring is CASE-040's

Controller correction, from the scratch note of 2026-09-03: [[DOCS-017]]
merged at `86ce276d` leaving the one production input source,
`EfAssessmentReportProjectionSource.cs`, passing `Signatory: null`, so no
report draft can be generated on `dev` until the case's sign-off Engineer is
wired through it. This section overrides the "Must not modify — DOCS-017-owned
report projection" line above and the files document's must-not-touch row for
that one file. It binds.

- **Owned path added:**
  `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`.
  Still not owned: `PlaywrightAssessmentReportRenderer.cs`, the Scriban
  template, `AssessmentReportRendering.cs`, `AssessmentReportProjection.cs`
  (DOCS-017's shapes stand; `Prepare` already requires a complete signatory).
- **Step 3a (new, after Step 3):** the production projection source resolves
  the case's Sign-off Engineer through the Step-2 Core resolver (persisted
  selection → eligible assigned Engineer → the default designation) and, when
  one resolves, passes a complete `ReportSignatory` (printed name,
  qualifications, signature image from [[PLAT-068]]'s profile query) to
  `AssessmentReportProjection.Prepare`; when none resolves it keeps passing
  null so readiness reports the Sign-off item. No second resolver: the source
  calls the Core rule, it does not restate it.
- **Acceptance (added):** an integration test that composes the real
  `EfAssessmentReportProjectionSource` (in
  `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs` or
  the persistence test that already exercises the production source)
  generates a draft end to end for a case whose sign-off resolves, and asserts
  the Sign-off readiness item for one that does not. The post-implementation
  report and the proof name that test. Nothing is promoted to `main` until it
  passes at the verified SHA.
- Build policy for this ticket follows EPIC-012 `context.md` §Build policy
  (2026-09-04): build concurrently, merge in queue order after [[CASE-041]],
  regenerate the migration if `dev`'s tail moved.

## Simplification pass (2026-09-04)

gpt-5.6-sol (low) read `git diff origin/dev` in the task worktree
(`.worktrees/case-040`) and reported three findings, reuse/simplification
lenses. All three applied by the wrapper (Claude), rebuilt and re-verified
green.

| # | File | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs` (`AssignCaseEngineer`) | `IStaffAccountQueries` was an optional constructor parameter with an empty-list fallback, even though production DI always supplies it and sign-off resolution is now part of every non-replay assignment — a second execution path that exists only to accommodate callers. | Fixed. Made the dependency required (`IStaffAccountQueries staffAccounts`, null-checked in the constructor); removed the `is null` fallback and call the query directly. Updated the one integration-test call site (`CaseWorkflowPersistenceTests.MissingDisabledOrNonEngineerStaffCannotBeAssigned`) that previously omitted the argument to pass a scoped `EfStaffAccountQueries`, matching the pattern already used by the sibling tests in the same file. |
| 2 | `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs` | The same optional-dependency/empty-list-fallback shape, even though both production DI and the one test call site already supply the service. | Fixed. Made `IStaffAccountQueries` required and replaced the conditional with one direct `ListSignOffEngineersAsync` call. No call site needed updating — the sole test construction already passed it. |
| 3 | `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | The page model kept an `EngineerOptions` property only to copy it immediately into `EvaHandoffViewModel`; no view or test read the page-level property (the partial's `Model.EngineerOptions` refers to the view model's own property, not the page's). | Fixed. Replaced the page-model property with a local `engineerOptions` variable in `DescribeWorkspaceExtrasAsync`, passed directly to the `EvaHandoffViewModel` constructor. |

Re-verified after the fixes: `dotnet build ./Pegasus.slnx --configuration
Release --no-restore` (0 warnings, 0 errors), `Pegasus.Core.Tests` (1230
passed), `Pegasus.ArchitectureTests` (100 passed), and the same integration
filter used before the pass (166 passed, 1 pre-existing skip) — all exit 0.
