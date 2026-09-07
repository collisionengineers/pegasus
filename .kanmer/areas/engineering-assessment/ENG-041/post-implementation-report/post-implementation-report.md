# Post-implementation report — ENG-041

## Candidate

Worktree .worktrees/eng-041; branch ENG-041-glass-recovery; exact base
1d972f05c0f10c2ecf804f271a4fd3155242f1ef (origin/dev at execution packet).
Implementation verified by root and authorized for PR/Review handoff.
16 source/document/test files plus fresh routed snapshots/catalogue where changed;
no package, schema, migration, deployment or live provider call.

## Changes and callers

GlassRepairEstimateGateway reuses the protected ProviderState and versioned
session port. The original callback is retained at Prepared, the provider vehicle
is durably checkpointed immediately after its answer, and the estimate-start
attempt is recorded before that external write. Cancellation is settled using
an uncancelled persistence token. Prepared and known-vehicle stages can resume;
an uncertain vehicle/start answer is not replayed. Known estimate IDs reopen
the same estimate. Unknown does not expire into an available account slot.
Core owns the shared account-occupying predicate and owner/confirmation rules.

The existing Case estimate section exposes Resume for occupied stages. Its
CloseGlass POST allows the owning Engineer to close an Unknown record only
with explicit confirmation that Glass is closed/no estimate remains open and
a reason. The store CAS and permanent ActionHistory record commit together;
stale or foreign requests do not release the account. Session checkpoints
also write content-safe history. No credentials, callback URLs/tokens or raw
provider content are included. Existing gateway registrations remain.

The existing SaveEstimate POST carries its submitted Case version and line
identities rather than replacing them from a current read. EstimatePolicy
resolves hidden provenance, source/rate retention and amendment stamps inside
the store after the permanent request-hash replay guard. K2's permanent action
result Id remains usable after K3: the replay returns that same estimate at its
current state, without reapplying the old edit or minting a historical snapshot.
Missing operation-result identity fails visibly. Changed intent under one key
still conflicts; stale new operations still fail version/lease checks.

FRD-06 records those end-state rules. The approved Razor action is an ordinary
POST with labelled reason and confirmation and one consequence sentence.

## Focused evidence authored

- Core EstimateTests: posted identity validation and unchanged submitted intent
  while hidden source/material/amendment evidence is retained.
- GlassRepairEstimateGatewayTests: cancel before provider mutation, lose the
  vehicle/start response after the provider acted, reconstruct the gateway,
  retain Unknown past expiry without a second launch; crash after the vehicle
  checkpoint and resume without another vehicle. Existing launch/history
  assertions now assert all durable checkpoint positions.
- GlassRepairEstimatePersistenceTests: owner, confirmation, reason and CAS
  negatives; real Web-runtime-role closure and permanent history; account
  unavailable before and available after explicit closure.
- GlassRepairEstimateCallbackWebTests: actual Case Resume/Close forms and
  handlers, unconfirmed closure refusal and no extra provider start.
- AssessmentEstimateImportWebTests: identical posted editor requests preserve
  original Case version, line IDs and hashable intent across mutable estimate
  replacement; missing version is refused. Existing provenance/rate assertions
  are preserved; the fake command delegates enrichment to the Core policy.
- AssessmentPersistenceIntegrationTests:
  EarlierEstimateUpdateReplaysItsRecordedIdentityWithoutRevertingLaterEdits
  exercises K1/K2/K3/K2 against SQL with reconstructed Web-role store, asserting
  same Id/current K3 lines, unchanged amendment time, exactly three operations
  and workflow version3; changed-key intent and stale new writes fail.

## Commands and result

Author ran only bounded source inspection and repository-configured
git diff --check: exit0, line-ending warnings only. No build, tests, snapshot
capture, CI, cloud or mail ran in this lane. Tests are candidates, never PASS
claims. Root is sole heavy verifier.

Core filter:
FullyQualifiedName~Pegasus.Core.Tests.Assessment.EstimateTests

Integration filter:
FullyQualifiedName~GlassRepairEstimateGatewayTests|FullyQualifiedName~GlassRepairEstimatePersistenceTests|FullyQualifiedName~GlassRepairEstimateCallbackWebTests|FullyQualifiedName~AssessmentEstimateImportWebTests|FullyQualifiedName~AssessmentPersistenceIntegrationTests.EarlierEstimateUpdateReplaysItsRecordedIdentityWithoutRevertingLaterEdits

Complementary existing estimate persistence (same compiled run if desired):
FullyQualifiedName~AssessmentPersistenceIntegrationTests.NamedEstimatesSaveDuplicateDiscardSetCurrentAndListWithOneCurrentPerCase|FullyQualifiedName~AssessmentPersistenceIntegrationTests.Estimate

Routed Case estimate UI changed. Root must capture case-details with the
minimal default/unavailable/conflict capture tests selected by root (and the
two changed estimate/Glass Web cohorts for actual form assertions), then scoped snapshot verify and
catalogue. Generated case-details HTML and index are declared in plan/files.

## Initial stop and handoff

The initial author stop was before commit/PR/Review/merge. Keep this exact worktree and claim for
compiler/test feedback. Root verifies first, then independent kanmer-review
reviews a pinned head after PR. No self-review or delivery claim. DOCS-020
separate verification is ongoing; no report/intake/principal source file was
modified here. The temporary DOCS-020 compiler fix was recorded on that ticket
and worktree, not in this diff.

## Root compiler feedback — 2026-09-07

Locked restore PASS. First Release build FAIL, exit 1, 88.27 s: CS9113 at
Details.cshtml.cs:77, unused primary-constructor TimeProvider clock after
amendment timestamps moved to the Core/store owner. No tests started. Author
removed only the unused injection; source/test search found no explicit
DetailsModel constructor callers needing adjustment (Razor uses DI).
Repository-configured git diff --check PASS, exit 0. No author build/test.
Candidate frozen again for root incremental verification. Earlier failure is
preserved; this correction is not a claimed compiler PASS.

## Root focused test feedback — 2026-09-07

Core EstimateTests PASS: 56 cases, 187 ms. Integration focused run completed
with 154 total, 153 PASS, 1 FAIL, 0 skipped in 154 s; TRX is
 tests/Pegasus.IntegrationTests/TestResults/eng-041-focused.trx.
Only SavingTheEditorStampsTheChangedLineAndKeepsTheUntouchedOnes failed: expected
the injected 2031-05-06T10:30Z host clock, received system 2026 time. Production
EfRepairSpecificationStore already uses its injected TimeProvider. The test's
RecordingStores double had invoked ApplyEditorEvidence with DateTimeOffset.UtcNow.
After root confirmed the run complete, its existing ISaveEstimate registration
was changed to supply the host TimeProvider to RecordingStores.Clock; the
Core policy invocation now uses Clock.GetUtcNow(). Timestamp and provenance
assertions are unchanged. No production edit or author build/test occurred.
Author git diff --check PASS exit 0; candidate frozen. Root reruns only this
one case after incremental build. Three minimal Case-detail captures in the
154-case run passed; no whole-cohort recapture/rerun is requested.

## Final root verification and handoff — 2026-09-07

Root's corrected incremental Release build PASS, exit 0, 16.26 s, zero warnings
and errors. Only the previous failed timestamp test plus the actual default
Review-state capture test passed: 2 PASS in 36 s. The capture test was
CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey.
No 154-test rerun occurred. Core's 56 PASS and the other 153 passing integration
cases remain the matching earlier evidence; the failed attempt is retained above.

Initial scoped snapshot update was FAIL (1 PASS / 1 FAIL) because default capture
was missing. The earlier minimal cohort had rendered NotReady rather than the
required Review plus edit-lease state. The actual route capture above corrected
the verification input, not production behavior. Final snapshot update PASS:
2 tests, 292 ms; verify PASS: 2 tests, 5 s; catalogue PASS: 62 routes,
69 prototypes, zero broken links. Captures and generated case-details snapshots
were refreshed by root. No manual visual review is claimed.

Author git diff --check PASS, exit 0, after final capture. Root authorized commit
with [skip ci], push to dev-targeting PR and move Review. Independent reviewer
must bind attestation to the pushed SHA; no author self-review, merge or deployment.

## Pushed candidate

PR https://github.com/collisionengineers/pegasus/pull/683 targets dev.
Head 1ac8bc428e0432b510b745342fdadd849d726878; commit uses root-authorized
[skip ci]. Eighteen files changed, 750 insertions and 140 deletions; refreshed
index/unavailable snapshot normalized to unchanged Git content, while default
and conflict snapshots changed. Worktree is clean. Review attestation pending.
