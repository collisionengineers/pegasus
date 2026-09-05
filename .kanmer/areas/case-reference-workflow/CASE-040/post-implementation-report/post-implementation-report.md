# Post-implementation report — CASE-040

PR: https://github.com/collisionengineers/pegasus/pull/666
Branch: `task/case-040-sign-off-engineer-eva`
Head SHA: `f96af24355bafd078bef7422e9837184cd36dcdc`
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
`20260905010654_CaseSignOffEngineer.cs` + `.Designer.cs` +
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
`IntakePersistenceIntegrationTests.cs`, `Browser/OperatorJourneyTests.cs`.

Test UI: `docs/design/test-ui/pages/case-details--default.html`,
`case-eva-send--default.html`, `case-details--conflict.html` (all three
regenerated and committed; see Snapshot evidence below).

No file outside the ticket's owned paths is in the final diff.
`Browser/OperatorJourneyTests.cs` was added to the ticket's owned paths by
the controller during the second review round, since it is a browser test
this ticket's own label rename broke, not tooling.

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
  handoff without changing state or version. The same D47 start-work
  precondition (state is Review and an eligible Engineer is assigned) is
  now held once in Core (`CaseEngineerEligibilityPolicy.RequireStartCaseWorkAsync`)
  and applied by both EVA routes.
- A successful EVA submission durably records its `EvaSubmissions` and
  `eva_api_submitted` action-history rows even when the local post-transport
  state re-check fails afterward (state change, sign-off resolution, or a
  workflow version conflict) — the delivery is never lost, and the
  operation key is never silently left replayable into a second live EVA
  claim. The automatic worker treats a post-delivery version conflict the
  same way it treats a state or signatory refusal: a terminal answer for
  that work item, not a fault to retry.
- Delivered-submission uniqueness removed (`FindDeliveredAsync`,
  `EvaAlreadySubmittedException`, `UX_EvaSubmissions_CaseDelivered`
  dropped); operation-key replay, automatic once-only ownership, and
  `Unknown`-only automatic retries preserved.
- Shared `_EvaHandoff` partial + `EvaHandoffViewModel`, exactly two callers
  (`Details.cshtml`, `Eva/Send.cshtml`); "Download EVA package" retired,
  action always labelled "Send to EVA", export control labelled
  "Download ZIP"; Send via API renders disabled only for a composed
  transport a Principal setting forbids, absent when not composed.
- Sign-off Engineer rendered in the Case ribbon/Current position and
  Overview, and carried on the archived-case workflow projection.
- `EfAssessmentReportProjectionSource` now resolves the case's Sign-off
  Engineer through the Step-2 Core resolver and passes a complete
  `ReportSignatory` when one resolves (null otherwise), closing the interim
  state left by DOCS-017 in which every production report draft returned
  the Sign-off readiness item.
- Migration `20260905010654_CaseSignOffEngineer`: adds nullable
  `CaseWorkflows.SignOffEngineerId`, drops
  `UX_EvaSubmissions_CaseDelivered`. No new table, FK, index, or grant.

## Named acceptance tests

- D47 shared Core state rule:
  `EvaSubmissionPolicyTests.FirstManualSendMovesReviewToWithEngineer`,
  `EvaSubmissionPolicyTests.ManualResendDoesNotChangeWithEngineerState`.
- Real routes end to end (first-send transition, local failure atomicity,
  With Engineer re-send, exact replay, port-level no-eligible-sign-off
  refusal through both `IExportCaseBundle` and `ISubmitCaseToEva`,
  port-level no-assigned/ineligible-Engineer refusal, the post-delivery
  version-race durability guarantee, and the automatic-worker version-race
  durability guarantee):
  `CustodyOutboxIntegrationTests.EvaRoutesTransitionFirstSendAtomicallyAndResendWithoutStateChange`
  and the review-round additions named below.
- Flagged/unflagged defaults:
  `AssignCaseEngineerTests.EnabledEngineerCanBeAssignedAndExactReplayDoesNotRecheckEligibility`,
  `AssignCaseEngineerTests.UnflaggedAssignedEngineerDefaultsToAdministratorDesignatedSignOffEngineer`.
- Real production report projection, end-to-end draft generation:
  `AssessmentPersistenceIntegrationTests.ReportDraftGenerationThroughProductionProjectionResolvesSignOffAndFailsClosedWithoutIt`.
- Keyboard-operable end-to-end export journey through the real Send page:
  `OperatorJourneyTests.CustodyRecoveryAndExportAreKeyboardUsableWithoutInternalIdentifiersOrExternalClaims`.

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
  history/submission record, and the second row's `afterJson` now carries
  the exact `assignedEngineerId`/`signOffEngineerId` GUIDs for both routes.
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
- `OperatorJourneyTests.ExportByKeyboardAsync` now looks up "Download ZIP"
  (the renamed control) instead of the retired "Download export", the
  journey assigns an eligible Engineer before the export so the D47
  precondition is met, and the second export's framing comment now
  describes it as the D36 re-send it is.

## Commands run and exit codes (independently re-run by the wrapper, not
only trusted from the implementer)

```
dotnet restore ./Pegasus.slnx --locked-mode                                   — exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore              — exit 0, 0 warnings, 0 errors
dotnet test tests/Pegasus.Core.Tests/...csproj --configuration Release --no-build           — exit 0, 1245 passed
dotnet test tests/Pegasus.ArchitectureTests/...csproj --configuration Release --no-build    — exit 0, 100 passed
dotnet test tests/Pegasus.IntegrationTests/...csproj --configuration Release --no-build \
  --filter "FullyQualifiedName~CaseWorkflowPersistenceTests|FullyQualifiedName~EvaSubmissionPersistenceTests|
            FullyQualifiedName~CustodyOutboxIntegrationTests|FullyQualifiedName~CaseDetailsWebTests|
            FullyQualifiedName~IntakePersistenceIntegrationTests|FullyQualifiedName~AssessmentPersistenceIntegrationTests|
            FullyQualifiedName~CaseWorkflowWebTests" -- xUnit.MaxParallelThreads=2 — exit 0, 174 passed, 1 pre-existing skip
dotnet test tests/Pegasus.IntegrationTests/...csproj --configuration Release --no-build \
  --filter "FullyQualifiedName~OperatorJourneyTests" -- xUnit.MaxParallelThreads=2 — exit 0, 5 passed
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1                      — exit 0, 94 migrations checked
```

## Snapshot evidence

- `docs/design/test-ui/pages/case-details--default.html`: 66,771 bytes,
  begins `<!DOCTYPE html>`, one `class="case-sticky"`, eleven distinct
  `id="section-..."` hosts, no `<img src="#">`.
- `docs/design/test-ui/pages/case-eva-send--default.html`: 25,888 bytes,
  begins `<!DOCTYPE html>`, carries the Sign-off Engineer field.
- `docs/design/test-ui/pages/case-details--conflict.html`: 40,383 bytes,
  begins `<!DOCTYPE html>`, same markers. This file IS an owned change in
  this diff (`git diff --stat origin/dev...HEAD` includes it) — the shared
  ribbon it renders changed for every Case state, and CI's unscoped
  `Update-TestUiSnapshots.ps1 -Verify` requires the regenerated file to be
  committed. An earlier version of this report incorrectly claimed this
  file was reverted to checkout content; that claim was false and is
  corrected here.

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
- `Browser/OperatorJourneyTests.cs` was added to the ticket's owned paths
  during the second review round (the controller widened scope for a
  browser test broken by this ticket's own label rename), not part of the
  original plan.

No file outside the owned-paths list is in the final diff; no package,
tooling script (`TestUiSnapshotTests.cs`, `ci.yml`, `scripts/*.ps1`), other
worktree, or `.kanmer` file was touched.

## Review round fixes (2026-09-04)

PR: https://github.com/collisionengineers/pegasus/pull/666
Branch: `task/case-040-sign-off-engineer-eva`
Fixes commit: `bfd089394` (parent `51e7fe5c78f4643d549b86ce30051d7d3f01edcc`,
the reviewed head).

Built by gpt-5.6-sol (medium effort), driven from a fix packet naming all
five review findings and the RULES that bind; independently reviewed and
verified by the wrapper (Claude) before commit and push.

### Blocker 1 — D47 transition skipped StartCaseWork's own preconditions

Fixed. `CaseEngineerEligibilityPolicy` (`src/Pegasus.Core/Lifecycle/CaseLifecycle.cs`)
gained one new public member, `RequireStartCaseWorkAsync(source, state,
assignedEngineerId, cancellationToken)`, holding both of `StartCaseWork`'s
runtime preconditions (state is Review and an Engineer is assigned; the
assigned Engineer is eligible) in the one place `StartCaseWork` itself now
calls. Both `EvaHandoffStore.RecordExportAsync` and
`EvaSubmissionStore.RecordSubmissionAsync` call the same method, guarded by
`if (resultingState != currentState)` so it runs only on the branch that is
actually leaving Review, inside the same locked/transactional section as the
state write — never on a With Engineer re-send, which still has no engineer
re-check (matching the plan). No second implementation of the rule exists.

Port-level tests added in `CustodyOutboxIntegrationTests.cs` (same method as
the existing D47 acceptance test): a Review case with `AssignedEngineerId =
null` is refused by both `IExportCaseBundle.ExecuteAsync` and
`ISubmitCaseToEva.ExecuteAsync` with the exact `StartCaseWork` message
("...after an Engineer is assigned"); a Review case whose assigned Engineer
is disabled is refused by both routes with the eligibility message
("...Engineer account is disabled"), and the EVA transport is never invoked
for either refusal (`evaTransport.CallCount == 0`).

### Blocker 2 — a completed EVA submission could be lost on a local re-check failure

Fixed. `EvaSubmissionStore.RecordSubmissionAsync` no longer lets
`EvaSubmissionPolicy.StateAfterSend`, `EvaHandoffPolicy.ResolveRequiredSignOffEngineer`,
the new `CaseEngineerEligibilityPolicy.RequireStartCaseWorkAsync` check, or
`CaseVersionConflictException` prevent the durable record of a transport call
that already succeeded. Those four checks now run inside a local `try`, and
on failure the exception is captured (not thrown) rather than aborting the
method: the `EvaSubmissions` row and the `eva_api_submitted` action-history
row are still added unconditionally (with the resolved Sign-off Engineer
captured before the transport call, so it does not depend on the post-call
re-check succeeding), `SaveChangesAsync`/`CommitAsync` still run, and only
after the commit does the method re-throw the captured exception via
`ExceptionDispatchInfo.Capture(...).Throw()` so the caller still observes the
failure and the stack trace is preserved. The local state transition itself
(workflow.State/Version/edit-lease clear) is skipped when a failure was
captured, so the case's own workflow row is untouched, but the submission
outcome is never lost.

A version-race test in `CustodyOutboxIntegrationTests.cs` drives this exact
window: a `RecordingEvaTransport` that mutates the workflow's `Version`
column (via raw SQL) from inside the transport call itself, so the local
re-check inside `RecordSubmissionAsync` observes a stale version and throws
`CaseVersionConflictException` after EVA has already "accepted" the
instruction. The test asserts: the caller sees the thrown
`CaseVersionConflictException`; exactly one `EvaSubmissions` row and one
`eva_api_submitted` history row exist for that operation key with the real
EVA identifiers; the transport was called exactly once; and a second call
with the same operation key replays that row (`IsSubmitted: true`, same
`EvaId`) without invoking the transport again — proving the operation key is
no longer replayable into a second live EVA claim.

This round's own re-review found that this fix, while correct for the
manual routes, left the automatic worker with no branch for the same
exception — closed by Blocker B below in the next round.

### Blocker 3 — SignOffEngineerId dropped on the archived read path

Fixed. `EfCaseQueryStore.MapWorkflow` now sets `SignOffEngineerId =
entity.SignOffEngineerId` once, in the initial `CaseWorkflowRecord` object
initializer, before the archived/non-archived branch; the non-archived
branch's redundant `with { SignOffEngineerId = ... }` was replaced with a
plain `return workflow;`, and the archived branch (`workflow with { Archive
= ... }`) now inherits the field automatically.

A new projection test, `ArchivedCaseProjectionRetainsPersistedSignOffEngineer`
in `CaseWorkflowPersistenceTests.cs`, seeds a workflow with a persisted
`SignOffEngineerId` distinct from the assigned Engineer, archives it
directly at the entity level, reads it back through the real query store,
and asserts the archived projection's `SignOffEngineerId` equals the
persisted value (not null, not a resolver fallback, and not equal to
`AssignedEngineerId`).

### Should-fix 4 — re-send test never asserted the second history row/identities

Fixed. `CustodyOutboxIntegrationTests.cs`'s existing With-Engineer re-send
coverage now asserts, for both routes:

- the export route: a second `eva_bundle_exported` action-history row keyed
  on the re-send's own operation key, whose `afterJson` carries the exact
  `assignedEngineerId`/`signOffEngineerId` GUIDs used for that send;
- the API route: `ActionHistory` count for `eva_api_submitted` is 2 after
  the re-send, and the second row's `afterJson` likewise carries the exact
  `assignedEngineerId`/`signOffEngineerId` GUIDs.

No existing assertion was weakened or removed to add these.

### Should-fix 5 — report accuracy at final head

This round's rewrite was itself found stale by the second re-review (it
still named the head, migration and snapshot sizes as they stood before the
`origin/dev` merge that regenerated the migration and one more snapshot).
See "Review round fixes (2026-09-05) — second re-review" below for the
finally-accurate figures; the corrections above (Files changed, Behaviour
delivered, Snapshot evidence) now reflect that final state directly rather
than repeating the stale numbers here.

### Rejected findings (no action taken)

- Moving `"The Sign-off Engineer was set."` into `OperatorLabels` —
  confirmed still rejected per the reviewer's own disposition (every sibling
  handler in `Workflow.cshtml.cs` keeps its inline literal).
- `CaseSignOffEngineerResolver.Resolve`'s `SingleOrDefault(IsDefault)` throw
  on a double default — confirmed still accepted as risk (PLAT-068 owns
  enforcement; fails closed).

### Commands run and exit codes

By the fix wrapper (gpt-5.6-sol), after applying the fixes, in
`.worktrees/case-040`:

```
dotnet restore ./Pegasus.slnx --locked-mode                              — exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore         — exit 0, 0 warnings, 0 errors
dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build  — exit 0, 1245 passed
dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build — exit 0, 100 passed
dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build \
  --filter "FullyQualifiedName~CaseWorkflowPersistenceTests|~EvaSubmissionPersistenceTests|
            ~CustodyOutboxIntegrationTests|~CaseDetailsWebTests|~IntakePersistenceIntegrationTests|
            ~AssessmentPersistenceIntegrationTests|~CaseWorkflowWebTests"
  -- xUnit.MaxParallelThreads=2                                          — exit 0, 174 passed, 1 pre-existing skip
```

Independently re-run by the wrapper (Claude) at commit `bfd089394`:

```
dotnet build ./Pegasus.slnx --configuration Release --no-restore         — exit 0
dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build \
  --filter "FullyQualifiedName~CustodyOutboxIntegrationTests|FullyQualifiedName~CaseWorkflowPersistenceTests" \
  -- xUnit.MaxParallelThreads=2                                          — exit 0, 60 passed, 1 pre-existing skip
dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build  — exit 0
dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build — exit 0
```

No migration changed in this round, so `Test-MigrationGrants.ps1` was not
re-run. No snapshot procedure applies (no routed page/partial/catalogue
changed).

Pushed: `51e7fe5c7..bfd089394 task/case-040-sign-off-engineer-eva`.

## Review round fixes (2026-09-05) — second re-review

PR: https://github.com/collisionengineers/pegasus/pull/666
Branch: `task/case-040-sign-off-engineer-eva`
Fixes commit: `f96af24355bafd078bef7422e9837184cd36dcdc` (parent
`bfd0893943f2b81c4446b7756860c02f415a17b7`, the second-round reviewed
head).

Built by gpt-5.6-sol (medium effort), driven from a fix packet naming
blockers A and B and should-fix C and the RULES that bind; independently
verified by the wrapper (Claude) before this write.

### Blocker A — CI-red keyboard export journey after this PR's own label rename

Fixed. `OperatorJourneyTests.ExportByKeyboardAsync` now looks up the button
by its actual current name, "Download ZIP" (matching
`OperatorLabels.CaseWorkspace.DownloadZip`, which `_EvaHandoff.cshtml`
renders), instead of the retired "Download export". The journey now
assigns an eligible, enabled Engineer to the seeded case (new private
helper `AssignEligibleEngineerAsync`, seeding a `PegasusIdentityUser` with
the Engineer role and calling `IAssignCaseEngineer` under a claimed edit
lease) before the first export, satisfying the D47
`RequireStartCaseWorkAsync` precondition the branch's own blocker-1 fix
already enforces. The comment above the second `ExportByKeyboardAsync`
call was corrected to describe it as the D36 re-send it now is (the case
is already in `ReportPreparation` after the first export moved it there),
rather than implying the first export left state unchanged; the
same-archive/same-filename assertions on the second download were kept
unchanged, since a re-send is still expected to produce the same bytes.

Verified locally:
`dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OperatorJourneyTests" -- xUnit.MaxParallelThreads=2`
— exit 0, 5 passed.

### Blocker B — automatic EVA worker could double-submit after a post-delivery version conflict

Fixed. `ProcessQueuedEvaSubmission.ExecuteAsync`
(`src/Pegasus.Core/Eva/EvaSubmissionWorkItem.cs`) now includes
`CaseVersionConflictException` in its terminal "no longer applicable"
catch filter, alongside `EvaHandoffStateException`,
`EvaSubmissionNotEnabledException`, and `EvaSignOffEngineerRequiredException`
— all four are recorded as `EvaSubmissionWorkState.Completed` with failure
code `eva_submission_no_longer_applicable`, so a version conflict
discovered strictly after `EvaSubmissionStore.RecordSubmissionAsync`
already committed the `EvaSubmissions` row and the `eva_api_submitted`
history row for a delivered instruction is treated as a terminal answer
for the work item, not a fault to retry. Without this, the exception
escaped `ExecuteAsync` uncaught, the `ExternalWorkItems` row stayed
`Processing`, its lease expired and was re-claimed under a new
`AttemptCount`, and the next attempt computed a different
`AttemptOperationKey` that the exact-key replay guard could not match —
resubmitting an already-delivered case to EVA a second time.

A new test,
`CustodyOutboxIntegrationTests.AutomaticEvaSubmissionCompletesAfterDeliveredVersionConflictWithoutRetrying`,
drives the real automatic path: it seeds a case with automatic EVA
submission enabled and an eligible default sign-off Engineer, enqueues its
automatic work item through `ReconcileAutomaticEvaSubmissions`, then runs
`ProcessQueuedEvaSubmission.ExecuteAsync` against an `EvaSubmissionStore`
whose fake transport mutates `CaseWorkflows.Version` by raw SQL from
inside the transport call — the same race shape as the existing manual
version-race test. It asserts: exactly one transport call; exactly one
`EvaSubmissions` row for the case with the real EVA identifiers; exactly
one `eva_api_submitted` action-history row; and the `ExternalWorkItems`
row for the work item ends `Completed` (not re-claimable), with its lease
token cleared. No exception escapes `ExecuteAsync`.

Verified locally:
`dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CustodyOutboxIntegrationTests"`
— exit 0, 23 passed, 1 pre-existing skip.

### Should-fix C — report accuracy (this write)

The stale claims the second re-review found are corrected directly in the
sections above (Files changed, Behaviour delivered, and Snapshot evidence),
not restated as a separate appendix this time, since that pattern is
exactly what produced the drift the second re-review caught:

- The Head SHA line now reads the branch's actual current head,
  `f96af24355bafd078bef7422e9837184cd36dcdc`, in place of the stale
  `d061171c8e2cab6066102af4d8a96f010b215e55`.
- The migration is named `20260905010654_CaseSignOffEngineer` everywhere
  in this report (corrected from the stale `20260904185256_CaseSignOffEngineer`
  — that file no longer exists; the migration was regenerated against
  dev's tail during the `origin/dev` merge that also produced commit
  `75020938c`).
- The Snapshot evidence byte sizes are corrected to the sizes actually
  committed at this head, measured directly with `wc -c
  docs/design/test-ui/pages/*.html`: `case-details--default.html`
  **66,771** bytes, `case-eva-send--default.html` **25,888** bytes, and
  `case-details--conflict.html` **40,383** bytes is now correctly listed
  as an owned change in this diff — `git diff --stat origin/dev...HEAD`
  includes it, and CI's unscoped `Update-TestUiSnapshots.ps1 -Verify`
  requires the regenerated file to be committed, since the shared ribbon
  it renders changed for every Case state. The false claim that this file
  was "reverted to checkout content" is removed.
- Neither the migration nor any snapshot changed again in this round —
  findings A and B touched only test files and `EvaSubmissionWorkItem.cs`;
  this write brings the report's prose in line with what was already
  committed instead of leaving it pointed at the first round's stale
  figures.

### Rejected/accepted findings (unchanged, re-confirmed this round)

- The inline literal "The Sign-off Engineer was set." — still rejected
  (every sibling handler in `Workflow.cshtml.cs` keeps its own inline
  literal).
- `CaseSignOffEngineerResolver.Resolve`'s `SingleOrDefault(IsDefault)`
  throw on a double default — still accepted as risk (PLAT-068 owns
  enforcement; fails closed).

### Commands run and exit codes (this round, in `.worktrees/case-040`)

```
dotnet restore ./Pegasus.slnx --locked-mode                                    — exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore               — exit 0, 0 warnings, 0 errors
dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build       — exit 0, 1245 passed
dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build — exit 0, 100 passed
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CustodyOutboxIntegrationTests" — exit 0, 23 passed, 1 pre-existing skip
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OperatorJourneyTests" -- xUnit.MaxParallelThreads=2 — exit 0, 5 passed
```

No migration changed in this round, so `Test-MigrationGrants.ps1` was not
re-run. No snapshot procedure applies — neither commit in this round
touched a routed Razor page, a partial it composes, or `catalogue.json`.

Pushed: `bfd089394..f96af2435 task/case-040-sign-off-engineer-eva`.

## Review round fixes (2026-09-05)

PR: https://github.com/collisionengineers/pegasus/pull/666
Branch: `task/case-040-sign-off-engineer-eva`
Fixes commit: `64889c42444492a0efd33626a53d407de8479734` (parent
`f96af24355bafd078bef7422e9837184cd36dcdc`, the third-round reviewed head).

Built by gpt-5.6-sol (medium effort), driven from a fix packet naming all
four round-3 findings and the RULES that bind; independently verified by
the wrapper (Claude) before this write.

### Blocker 1 — automatic once-only EVA submission no longer guaranteed

Fixed. `EvaSubmissionPolicy` gained a new Core-owned rule,
`RequireOnceOnlyAutomaticSubmission(trigger, hasDeliveredSubmission)`: it
throws the new `EvaAutomaticSubmissionAlreadyDeliveredException`
(`src/Pegasus.Core/Eva/EvaApiContracts.cs`) when the trigger is
`Automatic` and a delivered `EvaSubmissions` row already exists for the
case, and does nothing for `Manual` (of either delivered state) or for a
not-yet-delivered `Automatic` case. `EvaSubmissionStore.ExecuteAsync`
(`src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs`) queries
`context.EvaSubmissions.AnyAsync(item => item.CaseId == ... &&
item.IsDelivered)` immediately after the existing exact-key replay check
and before the transport call, and calls the new policy method with that
fact and `request.Trigger` — the store supplies the fact, Core owns the
decision, exactly as the review asked. The new exception was added to
`ProcessQueuedEvaSubmission`'s terminal "no longer applicable" catch
filter (`src/Pegasus.Core/Eva/EvaSubmissionWorkItem.cs`), alongside
`EvaHandoffStateException`, `EvaSubmissionNotEnabledException`,
`EvaSignOffEngineerRequiredException` and `CaseVersionConflictException`,
so it ends the work item as `Completed` rather than leaving it
`Processing` for a retry.

This closes the exact window round 3 identified: when the D47 local
transition fails on a post-delivery version conflict, the workflow row
stays in `Review` even though the `EvaSubmissions` row was already
committed as delivered; if the process then crashes or the lease expires
before the worker records completion, the re-claimed retry now hits the
new pre-transport refusal (its own `hasDeliveredSubmission` check, not the
state-based `StateAfterSend` guard that was inert in `Review`) instead of
calling the transport a second time. Manual re-sends are unaffected —
`RequireOnceOnlyAutomaticSubmission` is a no-op for `Manual` regardless of
delivery state, matching D36.

Proof, extending the existing round-2 automatic version-race test
`CustodyOutboxIntegrationTests.AutomaticEvaSubmissionCompletesAfterDeliveredVersionConflictWithoutRetrying`
rather than duplicating its fixture: after the first automatic attempt
throws `CaseVersionConflictException` (one transport call, workflow still
`Review`, `EvaSubmissions` row delivered), the `Processing` work item's
lease is set to already-expired, `ProcessQueuedEvaSubmission.ExecuteAsync`
re-claims it (`AttemptCount` becomes 2), and the test asserts
`transport.CallCount` is **still 1** (no second live call to EVA) and the
work row ends `completed` with `AttemptCount == 2` and
`FailureCode == "eva_submission_no_longer_applicable"`. Two focused unit
tests were also added to `EvaSubmissionPolicyTests.cs`:
`DeliveredAutomaticSubmissionIsRefused` (throws for
`Automatic`+delivered) and `FirstAutomaticAndAllManualSubmissionsRemainAllowed`
(a `[Theory]` over `Automatic`+not-delivered, `Manual`+not-delivered,
`Manual`+delivered — none throw).

### Blocker 2 — Current position card missing Sign-off Engineer

Fixed. `src/Pegasus.Web/Pages/Cases/Details.cshtml`'s "Current position"
context card now renders a `decision-row` for Sign-off Engineer
immediately beside Engineer, using
`OperatorLabels.CaseWorkspace.SignOffEngineer` and
`Model.SignOffEngineerDisplayName` — the same label/value pair the ribbon
already uses, no new plumbing. This changes a routed Razor page, so the
Case details snapshots were regenerated with the scoped capture
(`-Scope case-details -CaptureFilter
"FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~TestUiFocusedRenderTests"`),
then verified (`-Verify -SkipCapture`) and the catalogue re-checked —
both green. Two pages changed:
`case-details--default.html` (now 66,881 bytes; carries "Sign-off
Engineer" four times — ribbon, Overview, Current position, dialog — one
`class="case-sticky"`, eleven distinct base `id="section-…"` hosts
(overview, engineer-notes, inspection, vehicle, damage, valuation,
estimate, settlement, report, files, notes) plus their five `-title`
variants, no `<img src="#">`) and `case-details--conflict.html` (now
40,493 bytes, same markers, three occurrences of "Sign-off Engineer" since
the conflict scenario's dialog differs). `case-eva-send--default.html`
did not move (25,888 bytes, unchanged) — expected, since finding 4 below
only changes the source of an identical rendered string.

### Should-fix 3 — dead RibbonSignOff label

Fixed. Deleted `OperatorLabels.CaseWorkspace.RibbonSignOff` ("Sign-off").
`grep -rn "RibbonSignOff" src/ tests/ docs/` returns nothing after the
change.

### Should-fix 4 — Eva/Send.cshtml literals duplicate EvaHandoff

Fixed. Both `ViewData["Title"]` and the `<h1>` in
`src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml` now read
`OperatorLabels.CaseWorkspace.EvaHandoff` instead of the literal
`"EVA handoff"` twice; a `@using Pegasus.Web.Presentation` was added. The
rendered text is unchanged, and indeed `case-eva-send--default.html` did
not move in the regenerated capture, confirming the review's own
expectation.

### Commands run and exit codes (this round, in `.worktrees/case-040`)

By the fix wrapper (gpt-5.6-sol):

```
dotnet restore ./Pegasus.slnx --locked-mode                                   — exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore              — exit 0, 0 warnings, 0 errors
dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build       — exit 0, 1249 passed
dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build — exit 0, 100 passed
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CustodyOutboxIntegrationTests" — exit 0, 23 passed, 1 pre-existing skip
```

Independently re-run by the wrapper (Claude) at commit `64889c424`:

```
dotnet restore ./Pegasus.slnx --locked-mode                                   — exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore              — exit 0, 0 warnings, 0 errors
dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build       — exit 0, 1249 passed
dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build — exit 0, 100 passed
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CustodyOutboxIntegrationTests" — exit 0, 23 passed, 1 pre-existing skip
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~TestUiFocusedRenderTests" — exit 0
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details — exit 0
pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1                          — exit 0, 54 routed sources, 59 prototypes, 0 broken local references
```

No migration changed in this round, so `Test-MigrationGrants.ps1` was not
re-run.

Pushed: `f96af2435..64889c424 task/case-040-sign-off-engineer-eva`.

## Review round fixes (2026-09-05) — round 4

PR: https://github.com/collisionengineers/pegasus/pull/666
Branch: `task/case-040-sign-off-engineer-eva`
Fixes commit: `861391d9ad6bc3a42e83d107fc0d219d5346e347` (parent `3d82259f5fa1e37c5d4fcc7081f8c54f091115b4`, the branch tip at review time).

Built and independently verified by the wrapper (Claude), driven from a fix
packet naming BLOCKER 1, SHOULD-FIX 2-4 and NIT 5 and the RULES that bind.

### Blocker 1 — a rejected or unreachable EVA send still moved the case to With Engineer

Fixed. `EvaSubmissionPolicy.StateAfterSend` and
`EvaHandoffPolicy.StateAfterManualSend` (`src/Pegasus.Core/Eva/EvaSubmissionPolicy.cs`,
`src/Pegasus.Core/Eva/EvaBundleSchema.cs`) each gain an `isDelivered`
parameter defaulting to `true`. The default preserves both existing callers
that must keep assuming success: the export route's own
`StateAfterManualSend(state)`, whose write is atomic with the state change
so a committed row always means a real handoff, and the API route's
pre-transport preflight call in `EvaSubmissionStore.ExecuteAsync` (line 87),
which asks "what state would this leave the case in if it succeeds?" before
the outcome is known, purely to decide whether the D47 start-work
preconditions need checking before attempting the transport call at all.
`EvaSubmissionStore.RecordSubmissionAsync` (the actual post-transport
commit) now passes `result.IsDelivered` explicitly — the same property
`RequireOnceOnlyAutomaticSubmission` already consumes — so a `Rejected` or
`Unknown` manual send from Review leaves the case exactly where it was: no
state change, no version increment, no edit-lease clear. A `Partial`
outcome still transitions, because EVA did create a claim.

The local re-check's catch filter is unaffected by this change: a
`Rejected`/`Unknown` outcome no longer even reaches the
`resultingState != currentState` branch (both equal `Review`), so no
`CaseEngineerEligibilityPolicy` re-check or version-conflict check runs on
that branch — matching the plan's Resolutions §2 ("If either half fails the
whole command fails: the case stays in Review"), now true for a
transport-level failure as well as a local one.

Proof:

- Core unit tests in `EvaSubmissionPolicyTests.cs`:
  `UndeliveredManualSendFromReviewDoesNotMoveTheCase` (`[Theory]` over
  `Rejected` and `Unknown`, asserting `StateAfterSend(..., isDelivered:
  false)` returns `Review` unchanged) and
  `PartialManualSendFromReviewStillMovesTheCase` (asserts `Partial` still
  returns `ReportPreparation`).
- A new integration block in
  `CustodyOutboxIntegrationTests.EvaRoutesTransitionFirstSendAtomicallyAndResendWithoutStateChange`,
  using a new `FixedOutcomeEvaTransport` fake (every other store-level test
  in this file drives `RecordingEvaTransport`, which always returns
  `Succeeded` — the gap the review named): with the case in `Review` and an
  edit lease already held, a `Rejected` then an `Unknown` manual
  `EvaSubmissionStore.ExecuteAsync` call are each asserted to: return
  `IsSubmitted: true` with the real (undelivered) outcome; leave
  `CaseWorkflows.State == "Review"` and `Version` unchanged; leave
  `EditLeaseToken` exactly as seeded (proving the lease is untouched, not
  merely absent); and still commit one `EvaSubmissions` row
  (`IsDelivered == false`) and one `eva_api_submitted` action-history row
  keyed on that send's own operation key.

### Should-fix 2 — FRD-07 contradicted this PR's own durability fix

Fixed. `docs/frd/frd-07-eva-and-external-engineering-handoff.md`'s API
submission paragraph now distinguishes the three cases instead of the one
blanket sentence: a failure detected before the transport call records
nothing and leaves the Case in Review (unchanged); a `Rejected` or `Unknown`
outcome is stated explicitly as not a handoff, for the same reason as
Blocker 1 above; and a failure discovered only after EVA already accepted
the instruction (a state or version conflict found on the post-delivery
re-check) is now documented as still recording the submission and its
action history — since the delivery already happened and must not be lost
— while likewise leaving the Case in Review. The export paragraph (lines
62-67) was already accurate (unconditional atomicity) and is unchanged.

### Should-fix 3 — report/checklist stale at the head, fourth round writing this one

This report's own Head SHA line above is not rewritten for every round (that
repeated rewrite is exactly the structural defect round 3 identified); this
section instead records the branch tip *at this round's review time* as
`3d82259f5fa1e37c5d4fcc7081f8c54f091115b4`. As committed blobs at that SHA
(`wc -c`): `case-details--default.html` is **68,567** bytes,
`case-details--conflict.html` is **42,179** bytes, `case-eva-send--default.html`
is **25,888** bytes (unchanged since round 3), and
`docs/design/test-ui/pages/queues--empty.html` (**29,803** bytes) is also
present at that head. All four moved or appeared via the `origin/dev` merge
commit `916177da7` (bringing ENG-034's Engineer-sections move) and the
follow-up regeneration commit `3d82259f5` — neither is a round-4 change; this
round's own commit (`861391d9a`) touches only Core, Infrastructure, one FRD,
and two test files, no routed page or `docs/design/test-ui/**` file. No
snapshot procedure applies to round 4 itself. Authoritative byte sizes for
whatever head actually merges belong in `proof.md`, written on merged `main`
after review and merge, not repeated here again.

### Should-fix 4 — three spellings of the readiness envelope

Not applied in this ticket; raised as follow-up ticket
[[CASE-046]] ("One spelling for the readiness envelope across reopen,
return, assignment and EVA handoff forms"), linked from CASE-040. Retyping
`_ReadinessHiddenFields.cshtml` off `DetailsModel` — the cheap-fix path the
review named — reaches into `DetailsModel.cs` outside this ticket's narrow
CASE-038 hand-off ownership of that file (recorded at the top of this
report), so it is out of CASE-040's scope per the repository's own
"scope is the ticket" rule rather than a judgement that the finding is
wrong. Nothing is broken today: the three spellings currently agree.

### Nit 5 — accepted, tightened

Fixed while applying Blocker 1.
`EvaSubmissionStore.RecordSubmissionAsync`'s local-recheck catch filter is
narrowed from `EvaHandoffStateException or EvaSignOffEngineerRequiredException
or CaseVersionConflictException or InvalidOperationException` to plain
`InvalidOperationException`, with a comment stating that all three named
types already derive from it and that the filter is deliberately just the
base type because the guarded block is a fixed, closed set of local
re-checks. Nothing was previously suppressed (the exception is always
re-thrown via `ExceptionDispatchInfo` after commit) and nothing is
suppressed now.

### Accepted, no action (unchanged from prior rounds)

- A post-delivery `CaseVersionConflictException` surfaces as "The case
  could not be sent to EVA." on `Send.cshtml.cs` though EVA did receive it —
  rare, delivery durable and visible on reload; worth a line in `proof.md`.
- A concurrent duplicate export from Review throws
  `CaseVersionConflictException` instead of replaying — error surfaces,
  nothing lost, no EVA claim; worth a line in `proof.md`.
- The inline `"The Sign-off Engineer was set."` literal — still rejected
  (every sibling handler in `Workflow.cshtml.cs` keeps its own).
- `CaseSignOffEngineerResolver.Resolve`'s `SingleOrDefault(IsDefault)` throw
  on a double default — still accepted as risk (PLAT-068 owns enforcement;
  fails closed).

### Commands run and exit codes (this round, in `.worktrees/case-040`)

Independently run by the wrapper (Claude):

```
dotnet build ./Pegasus.slnx --configuration Release --no-restore                                       — exit 0, 0 warnings, 0 errors
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build     — exit 0, 1252 passed (was 1249; +3 new)
dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build — exit 0, 100 passed
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build \
  --filter "FullyQualifiedName~CustodyOutboxIntegrationTests" -- xUnit.MaxParallelThreads=2            — exit 0, 23 passed, 1 pre-existing skip
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build \
  --filter "FullyQualifiedName~EvaSubmissionPersistenceTests|FullyQualifiedName~CaseWorkflowPersistenceTests" -- xUnit.MaxParallelThreads=2 — exit 0, 44 passed
```

No migration changed this round, so `Test-MigrationGrants.ps1` was not
re-run. No routed page, partial, or `catalogue.json` changed this round, so
no snapshot procedure applies.

Pushed: `3d82259f5..861391d9a task/case-040-sign-off-engineer-eva`.

## Review round fixes (2026-09-05) — round 5

PR: https://github.com/collisionengineers/pegasus/pull/666
Branch: `task/case-040-sign-off-engineer-eva`
Fixes commit: `41a92a1ed` (parent `861391d9ad6bc3a42e83d107fc0d219d5346e347`,
round 4's own fixes commit and the branch tip at this round's review time).

Codex was unavailable this round; the fixes below were applied directly by
the wrapper (Claude) from a fix packet naming one BLOCKER and two
SHOULD-FIX findings and the RULES that bind, and verified by the same
agent (no separate independent-verification step this round).

### Blocker 1 — stale queues--empty capture (0/0/0 replaced with 2/1/1)

Fixed. `docs/design/test-ui/pages/queues--empty.html` had drifted from
`origin/dev`'s committed capture — its nav-count read `2` instead of `0`,
and its "Not ready"/"Unidentified" tab counts read `1`/`1` instead of
`0`/`0` — while still keeping the `class="muted">0 items</span>` state
matcher, so a scoped local `-Verify` passed while CI's full, unscoped
capture regenerated the real 0/0/0 page and reported the file stale (runs
33976995699 and 33974413448). CASE-040 renders nothing on the `/Cases`
queues route, so no CASE-040 change should ever move this file; the round
4 report's own "Should-fix 3" entry recorded the file's presence (from the
`origin/dev` merge commit `916177da7` plus the regeneration commit
`3d82259f5`) but did not catch that its content had diverged from what
`origin/dev` actually carries.

Restored with `git checkout origin/dev -- docs/design/test-ui/pages/queues--empty.html`
(the finding's first fix option) rather than a full unscoped re-capture,
since the target content is exactly `origin/dev`'s own committed file and
no CASE-040 change touches that route. `git diff origin/dev -- docs/design/test-ui/pages/queues--empty.html`
is now empty. Committed byte size at the new head: 29,803 bytes (`git
cat-file -s`, matching `origin/dev`'s blob exactly — the larger `wc -c` a
plain checkout reports locally is this workstation's `core.autocrlf=true`
CRLF conversion on the working-tree copy, not a real content difference).

**Files changed → Test UI correction:** the round-4 report's "Files
changed" section never listed `queues--empty.html` as an owned CASE-040
change (it only ever appeared via the `origin/dev` merge/regeneration
commits, as round 4's own "Should-fix 3" entry states) — the finding's
observation that the file count read three named snapshots against a
four-file diff was about the branch's total working diff at review time,
not this report's Test UI list, which already excluded the file. No
"Files changed" edit was needed; that section stays accurate as written.

### Should-fix 2 — stale comment named the dropped unique index as the once-only guard

Fixed. `src/Pegasus.Infrastructure/Persistence/EvaSubmissionEntities.cs`'s
`ExternalRef` doc comment said "the unique index below is what actually
prevents the second send" — true before round 3's blocker-1 fix, false
since `UX_EvaSubmissions_CaseDelivered` was dropped by this PR's migration
and its configuration removed. Reworded to name
`EvaSubmissionPolicy.RequireOnceOnlyAutomaticSubmission` and the durable
`ExternalWorkItems` row (D36) as the actual once-only owners, and to state
that the database now deliberately permits an explicit manual re-send —
matching the wording already carried by the sibling comments in
`EvaSubmissionModelConfiguration.cs`, which the round-3 fix rewrote but
this one sibling comment was missed.

### Should-fix 3 — once-only guard proved only through the work-item wrapper, not the store directly

Fixed. Added one direct-store assertion to the existing
`CustodyOutboxIntegrationTests.AutomaticEvaSubmissionCompletesAfterDeliveredVersionConflictWithoutRetrying`:
after the existing flow proves the guard through
`ProcessQueuedEvaSubmission`'s exception-mapping onto the retried work
item (transport called once, work item completes as
`eva_submission_no_longer_applicable`), a further call directly against
the test's own `EvaSubmissionStore` instance — with a wholly new,
unrelated operation key, bypassing the work-item/processor plumbing
entirely — is asserted to throw
`EvaAutomaticSubmissionAlreadyDeliveredException` while `transport.CallCount`
does not increase. This proves the once-only guard at the store level on
its own terms (a fresh automatic submission call, not a retry of the same
queued work item), complementing the existing Core-level
`EvaSubmissionPolicyTests` coverage and the work-item-level completion
proof already in this test. No existing assertion was weakened, removed,
or duplicated to add this.

### Nit 4 — accepted, deferred (no action this round)

Left for the release task, per the reviewer's own disposition:
`docs/current-architecture.md:587`'s stale claim that the export "does not
take an edit lease or move the case version" is a current-architecture
(as-built/deployed) snapshot fact, and CASE-040 is not yet deployed.

### Commands run and exit codes (this round, in `.worktrees/case-040`)

```
dotnet restore ./Pegasus.slnx --locked-mode                                    — exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore               — exit 0, 0 warnings, 0 errors
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build       — exit 0, 1252 passed
dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build — exit 0, 100 passed
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CustodyOutboxIntegrationTests" — exit 0, 23 passed, 1 pre-existing skip
```

No migration changed this round, so `Test-MigrationGrants.ps1` was not
re-run. No routed Razor page, partial, or `catalogue.json` changed this
round — the restored `queues--empty.html` is a correction back to
`origin/dev`'s already-correct content, not a page change — so no snapshot
capture procedure applies; `Test-UiCatalogue.ps1` was not re-run for the
same reason.

Pushed: `861391d9a..41a92a1ed task/case-040-sign-off-engineer-eva`.
