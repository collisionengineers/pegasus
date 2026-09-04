# Post-implementation report — CASE-040

PR: https://github.com/collisionengineers/pegasus/pull/666
Branch: `task/case-040-sign-off-engineer-eva`
Head SHA: `d061171c8e2cab6066102af4d8a96f010b215e55`
Base: `origin/dev` at `90a759184` (no dev-tail movement during implementation —
`git merge --no-edit origin/dev` before capture reported "Already up to
date").

## CASE-038 Details hand-off (recorded before the first commit to those files)

CASE-040 took over only:

- `Details.cshtml`: the `canSendToEva` declaration and its EVA-send
  commentary (then-current lines 50-53), the action-bar `canSendToEva`
  branch opening `eva-handoff-dialog` (then-current lines 264-270), the
  scaffolded Sign-off Engineer ribbon value (then-current lines 132-137),
  and the complete `eva-handoff-dialog` block (then-current lines 594-665).
- `Details.cshtml.cs`: only `HasExportedBundle`, `EngineerDisplayName`,
  `EngineerOptions` (later removed in the simplification pass — see below),
  `CanSubmitToEva`, their supporting constructor dependencies, and the
  corresponding logic in `DescribeWorkspaceExtrasAsync`.

Every other region of both files remained CASE-038's.

## Files changed

Core: `CaseWorkflowContracts.cs`, `CaseLifecycle.cs`,
`Eva/EvaBundleSchema.cs`, `Eva/EvaSubmissionPolicy.cs`,
`Eva/EvaApiContracts.cs`, `Eva/EvaSubmissionWorkItem.cs`.

Infrastructure: `DependencyInjection.cs`, `CaseWorkflowEntities.cs`,
`EfCaseWorkflowStore.cs`, `EfCaseQueryStore.cs`, `EvaHandoffStore.cs`,
`EvaSubmissionStore.cs`, `EvaSubmissionModelConfiguration.cs`,
`EfAssessmentReportProjectionSource.cs`, migration
`20260904185256_CaseSignOffEngineer.cs` + `.Designer.cs` +
`PegasusDbContextModelSnapshot.cs`.

Web: `Presentation/OperatorLabels.cs` (new CASE-040-delimited block),
`Pages/Cases/Workflow.cshtml.cs`, `Pages/Cases/Shared/_CaseSummary.cshtml`,
`Pages/Cases/Shared/EvaHandoffViewModel.cs` (new),
`Pages/Cases/Shared/_EvaHandoff.cshtml` (new),
`Pages/Cases/Details.cshtml` + `.cshtml.cs` (narrow regions only),
`Pages/Cases/Eva/Send.cshtml` + `.cshtml.cs`.

Docs: `docs/frd/frd-07-eva-and-external-engineering-handoff.md` (D47
correction).

Tests: `AssignCaseEngineerTests.cs`, `EvaSubmissionPolicyTests.cs`,
`AssessmentPersistenceIntegrationTests.cs`, `CaseDetailsWebTests.cs`,
`CaseWorkflowPersistenceTests.cs`, `CaseWorkflowWebTests.cs`,
`CustodyOutboxIntegrationTests.cs`, `EvaSubmissionPersistenceTests.cs`,
`IntakePersistenceIntegrationTests.cs`.

Test UI: `docs/design/test-ui/pages/case-details--default.html`,
`case-eva-send--default.html` (regenerated only).

No file outside the ticket's owned paths is in the final diff. Three
non-owned snapshots the scoped generator also regenerated as a side effect
(`index.html`, `case-details--conflict.html`,
`case-details--unavailable.html`) were reverted to checkout content before
committing.

## Behaviour delivered

- Sign-off Engineer resolver (Core, `CaseSignOffEngineerResolver.Resolve`):
  persisted eligible selection → eligible assigned Engineer →
  Administrator-designated default → none. No account identity is
  hard-coded.
- `AssignCaseEngineer` derives and persists the default at assignment time;
  `SetCaseSignOffEngineer` is the reasoned, lease-backed, explicit selection
  action for Review/ReportPreparation/PostReport, absent in Complete.
- Centralized EVA state policy: manual handoffs (Download ZIP, Send via API)
  permitted in Review and With Engineer; automatic submission Review-only;
  the "eligible resolved Sign-off Engineer" precondition enforced once in
  Core and consumed by both `EvaHandoffStore` (pre-flight and inside the
  locked export transaction) and `EvaSubmissionStore`.
- **D47**: the first manual send from Review atomically persists the
  handoff and moves the case to `ReportPreparation` (With Engineer) with one
  version increment; failure of either half leaves the case in Review with
  no partial handoff. Re-sends from With Engineer/PostReport persist a new
  handoff without changing state or version.
- Delivered-submission uniqueness removed (`FindDeliveredAsync`,
  `EvaAlreadySubmittedException`, `UX_EvaSubmissions_CaseDelivered`
  dropped); operation-key replay, automatic once-only ownership, and
  `Unknown`-only automatic retries preserved.
- Shared `_EvaHandoff` partial + `EvaHandoffViewModel`, exactly two callers
  (`Details.cshtml`, `Eva/Send.cshtml`); "Download EVA package" retired,
  action always labelled "Send to EVA"; Send via API renders disabled only
  for a composed transport a Principal setting forbids, absent when not
  composed.
- Sign-off Engineer rendered in the Case ribbon/Current position and
  Overview.
- `EfAssessmentReportProjectionSource` now resolves the case's Sign-off
  Engineer through the Step-2 Core resolver and passes a complete
  `ReportSignatory` when one resolves (null otherwise), closing the interim
  state left by DOCS-017 in which every production report draft returned
  the Sign-off readiness item.
- Migration `20260904185256_CaseSignOffEngineer`: adds nullable
  `CaseWorkflows.SignOffEngineerId`, drops
  `UX_EvaSubmissions_CaseDelivered`. No new table, FK, index, or grant.

## Named acceptance tests

- D47 shared Core state rule:
  `EvaSubmissionPolicyTests.FirstManualSendMovesReviewToWithEngineer`,
  `EvaSubmissionPolicyTests.ManualResendDoesNotChangeWithEngineerState`.
- Real routes end to end (first-send transition, local failure atomicity,
  With Engineer re-send, exact replay, port-level no-eligible-sign-off
  refusal through both `IExportCaseBundle` and `ISubmitCaseToEva`):
  `CustodyOutboxIntegrationTests.EvaRoutesTransitionFirstSendAtomicallyAndResendWithoutStateChange`.
- Flagged/unflagged defaults:
  `AssignCaseEngineerTests.EnabledEngineerCanBeAssignedAndExactReplayDoesNotRecheckEligibility`,
  `AssignCaseEngineerTests.UnflaggedAssignedEngineerDefaultsToAdministratorDesignatedSignOffEngineer`.
- Real production report projection, end-to-end draft generation:
  `AssessmentPersistenceIntegrationTests.ReportDraftGenerationThroughProductionProjectionResolvesSignOffAndFailsClosedWithoutIt`.

## Corrected (not deleted) assertions

- `SendToEvaRendersOnlyInReview` → `SendToEvaRendersInReviewAndWithEngineer`:
  ReportPreparation/PostReport now assert the action is present (both render
  as With Engineer); terminal/non-handoff states still assert absence.
- `SendPageRendersItsChoiceOnlyInReview` →
  `SendPageRendersItsChoiceInReviewAndWithEngineer`: Review and
  ReportPreparation both assert the shared Download ZIP / API choice.
- `TwoDeliveredSubmissionsCannotBePersisted` →
  `TwoManualDeliveredSubmissionsAreRetainedAsDistinctHandoffs`: now asserts
  two delivered rows (D36 permits explicit re-send).
- `PartialDeliveryBlocksSecondSubmission` →
  `PartialDeliveryDoesNotBlockAnExplicitManualResend`: now asserts both a
  Partial and a later Succeeded handoff are retained.
- In `EvaRoutesTransitionFirstSendAtomicallyAndResendWithoutStateChange`,
  first ZIP/API sends assert `Review -> ReportPreparation` and one version
  increment; re-sends assert unchanged state/version plus a new
  history/submission record.
- Assertions that expected `CaseNotInReviewException` on the two EVA routes
  (locked export race, terminal export) now expect the new
  `EvaHandoffStateException`. The nearby `IExportCaseDocuments` assertion
  (ordinary document export, not an EVA route) still expects
  `CaseNotInReviewException` unchanged — out of scope.
- Both port-level missing-signatory assertions additionally prove the
  workflow row is byte-for-record unchanged, no API transport call occurs,
  and neither EVA handoff table gains a partial record.
- The report photo fixture's placeholder custody hashes were corrected to
  real SHA-256 hashes of the returned bytes, so draft validation proves the
  real custody contract.

## Commands run and exit codes (independently re-run by the wrapper, not
only trusted from the implementer)

```
dotnet restore ./Pegasus.slnx --locked-mode                                   — exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore              — exit 0, 0 warnings, 0 errors
dotnet test tests/Pegasus.Core.Tests/...csproj --configuration Release --no-build           — exit 0, 1230 passed
dotnet test tests/Pegasus.ArchitectureTests/...csproj --configuration Release --no-build    — exit 0, 100 passed
dotnet test tests/Pegasus.IntegrationTests/...csproj --configuration Release --no-build \
  --filter "FullyQualifiedName~CaseWorkflowPersistenceTests|FullyQualifiedName~EvaSubmissionPersistenceTests|
            FullyQualifiedName~CustodyOutboxIntegrationTests|FullyQualifiedName~CaseDetailsWebTests|
            FullyQualifiedName~IntakePersistenceIntegrationTests|FullyQualifiedName~AssessmentPersistenceIntegrationTests|
            FullyQualifiedName~CaseWorkflowWebTests" -- xUnit.MaxParallelThreads=2 — exit 0, 166 passed, 1 pre-existing skip
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1                      — exit 0, 92 migrations checked
git merge --no-edit origin/dev (pre-capture refresh)                          — exit 0, already up to date
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope 'case-details,case-eva-send' \
  -CaptureFilter 'FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~TestUiFocusedRenderTests' — exit 0, 73 capture tests + 1 update test passed
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope 'case-details,case-eva-send' \
  -CaptureFilter '...' -Verify -SkipCapture                                   — exit 0, 1 verify test passed
pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1                          — exit 0, 54 routed sources, 59 prototypes, 0 broken references
```

After the simplification-pass fixes (see below), re-run: build (0
warnings/errors), Core.Tests (1230 passed), ArchitectureTests (100 passed),
and the same integration filter (166 passed, 1 pre-existing skip) — all exit
0.

## Snapshot evidence

- `docs/design/test-ui/pages/case-details--default.html`: 64,565 bytes,
  begins `<!DOCTYPE html>`.
- `docs/design/test-ui/pages/case-eva-send--default.html`: 25,888 bytes,
  begins `<!DOCTYPE html>`.
- No `case-details--conflict.html` / `case-details--unavailable.html` /
  `index.html` change is in the diff — those three non-owned snapshots the
  generator also touched (the shared ribbon renders on every Case state)
  were reverted to checkout content, since the ticket's files document names
  only the two default-state snapshots as owned. A follow-up UI-tooling
  ticket regenerating those states will pick up the label change.

## Simplification pass (2026-09-04)

gpt-5.6-sol (low effort) read the branch diff and reported three
reuse/simplification findings; all three applied, then the build and the
above Core/Architecture/integration checks were re-run green:

1. `CaseLifecycle.AssignCaseEngineer`'s `IStaffAccountQueries` optional
   parameter (with an empty-list fallback) made required — production DI
   always supplies it and it's used on every non-replay path.
2. `EfAssessmentReportProjectionSource`'s same optional-dependency shape
   made required for the same reason.
3. `Details.cshtml.cs`'s redundant page-model `EngineerOptions` property
   (read by nothing but its own assignment into the view model) replaced
   with a local variable.

Full disposition table recorded in the ticket's plan document under
"Simplification pass (2026-09-04)".

## Deviations from the plan/checklist

- `AssessmentPersistenceIntegrationTests.cs` (not
  `AssessmentReportDraftWebTests.cs`, named as an alternative in the plan)
  was used for the end-to-end production-projection test; it is the
  existing persistence test that already exercises the real
  `EfAssessmentReportProjectionSource`, as the plan's Resolutions
  (2026-09-04) explicitly permits.
- One test call site (`CaseWorkflowPersistenceTests.MissingDisabledOrNonEngineerStaffCannotBeAssigned`)
  needed a scoped `EfStaffAccountQueries` added when the simplification pass
  made `AssignCaseEngineer`'s dependency required — a mechanical
  consequence of finding 1 above, not a scope change.

No file outside the owned-paths list is in the final diff; no package,
tooling script (`TestUiSnapshotTests.cs`, `ci.yml`, `scripts/*.ps1`), other
worktree, or `.kanmer` file was touched.
