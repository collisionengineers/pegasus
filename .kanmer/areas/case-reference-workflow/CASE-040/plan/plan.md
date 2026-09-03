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
  action. This preserves FRD-07's state rule.
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
| `src/Pegasus.Core/Eva/EvaBundleSchema.cs`<br>`src/Pegasus.Core/Eva/EvaSubmissionPolicy.cs`<br>`src/Pegasus.Core/Eva/EvaApiContracts.cs`<br>`src/Pegasus.Core/Eva/EvaSubmissionWorkItem.cs` | Centralize manual versus automatic state gates and remove the superseded one-delivery exception path. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register the new Core Sign-off action as a production caller. |
| `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs`<br>`src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs`<br>`src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | Persist, project, replay-protect, and history-record `SignOffEngineerId`. |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs`<br>`src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs`<br>`src/Pegasus.Infrastructure/Persistence/EvaSubmissionModelConfiguration.cs` | Permit manual With Engineer handoffs, snapshot identities in route history, and remove the delivered-row uniqueness rule. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseSignOffEngineer.cs`<br>`src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseSignOffEngineer.Designer.cs`<br>`src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Add nullable `CaseWorkflows.SignOffEngineerId` and drop `UX_EvaSubmissions_CaseDelivered` in the single CASE-040 migration. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Add only missing Case workspace and EVA handoff labels, including the exact `Unassigned` no-value label. |
| `src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs` | Bind the reasoned Sign-off selection handler. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` | Render Sign-off Engineer beside Engineer in Overview. |
| `src/Pegasus.Web/Pages/Cases/Shared/EvaHandoffViewModel.cs`<br>`src/Pegasus.Web/Pages/Cases/Shared/_EvaHandoff.cshtml` | Create the two-caller view model and shared handoff partial. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml`<br>`src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | After CASE-038, host the partial, supply resolved values/options, offer both permitted states, and retire the Download EVA package switch. |
| `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml`<br>`src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml.cs` | Render the same partial for script-off use and admit Review/With Engineer. |
| `tests/Pegasus.Core.Tests/Lifecycle/AssignCaseEngineerTests.cs`<br>`tests/Pegasus.Core.Tests/Qdos/EvaSubmissionPolicyTests.cs` | Test Core default/selection and manual-versus-automatic EVA state policy. |
| `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs`<br>`tests/Pegasus.IntegrationTests/EvaSubmissionPersistenceTests.cs`<br>`tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | Test persisted identity/history, distinct delivered re-send rows, and rendered routes/controls. |
| `tests/Pegasus.IntegrationTests/CaseTaskArchivePersistenceTests.cs`<br>`tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs` | Add the new workflow-store member to existing test fakes only; retain their assertions. |
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
- Confirm PLAT-068's profile/default contract, CASE-038's transferred dialog
  boundary, DOCS-017's report seam, and the available migration/shared-file
  locks before taking implementation work.
- Stop and report if the profile lacks a durable default designation, if
  CASE-038 has not released the narrow Details region, or if another unmerged
  migration occupies the lane.

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
  With Engineer from automatic submission allowed only in Review. Both
  persistence stores consume that policy rather than retaining duplicate
  Review-only checks.
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
  `OperatorLabels.CaseWorkspace` / `OperatorLabels.EvaHandoffs`.
- Add only missing labels to `OperatorLabels`; render no newly introduced
  literal label, raw state, account ID, or explanatory sentence.
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
  Require an eligible resolved Sign-off Engineer before either route proceeds.
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
  `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`,
  `tests/Pegasus.IntegrationTests/CaseTaskArchivePersistenceTests.cs`,
  `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs`.
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
- Update only workflow-store test fakes required by the added store member;
  do not alter their unrelated behaviours or assertions.

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

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
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
exist and the latter two carry `ICaseWorkflowStore` fakes;
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
