# Post-implementation report — ENG-041

## Candidate

Worktree .worktrees/eng-041; branch ENG-041-glass-recovery; exact base
1d972f05c0f10c2ecf804f271a4fd3155242f1ef (origin/dev at execution packet).
Original candidate was verified by root and submitted for independent review.
Round 1 below records the two findings and the uncommitted correction candidate.
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
and conflict snapshots changed. That original worktree was clean. Independent review subsequently required the
round-1 corrections below.

## Remediation round 1

Review cc5b9a51b2e4e5d3, against pushed head
1ac8bc428e0432b510b745342fdadd849d726878, returned F-001/F-002 needs-changes.
Both findings are accepted for correction; the independent record and earlier
verification attempts remain unchanged. Root assigned the bounded batch on
same PR683/branch/worktree; plan amended to 5a0b40d8006016eb before edits.
The correction is currently uncommitted and frozen for root verification.

F-001: GlassRepairEstimateGateway.ResumeAsync now refuses missing supplied Case
version/lease before continuing Prepared or known-vehicle work. It invokes the
existing IGlassRepairEstimateCaseAuthority with the current actor and submitted
version/token, then retains regained authority in protected provider state for
import. Known-estimate reopening and callback/export reconciliation are unchanged.
No separate Web policy or new authority abstraction was added.

F-002: GlassMvaClient passes its existing outcomeUnknown value through the
bounded ReadAsync path into GlassMvaStageException. Oversized create/start
responses remain uncertain; read-only/download failures retain their existing
definite refusal. All response size bounds remain unchanged.

Only those two production files and GlassRepairEstimateGatewayTests.cs changed:
174 insertions/10 deletions across three files. Two existing interrupted-launch
success tests now supply their Case version/token. The five new runtime cases
are two SQL Prepared/known-vehicle scenarios with the actual
EfGlassRepairEstimateCaseAuthority/CaseMutationGuard, two SQL oversized-response
create/start scenarios, and one in-memory read-only overflow scenario. They
prove missing authority fields, stale version, wrong token, foreign holder and
expired lease make no additional provider request or session write; valid
regained authority resumes once and reaches import unchanged. Oversized writes
remain Unknown with the SQL active-account key occupied after reconstructed
scope and local expiry, with no repeated create/start or second session.

Author git diff --check PASS, exit 0 (CRLF warnings only). No compiler, runtime
test, CI, provider/cloud call, commit or push ran in this round. No Razor file
changed, so this correction does not request repeated page captures. Root owns
incremental build and the focused filter below, then independent delta review.

FullyQualifiedName~GlassRepairEstimateGatewayTests.ResumedFreshWritesRequireCurrentPersistedCaseAuthorityAndRetainItForImport|FullyQualifiedName~GlassRepairEstimateGatewayTests.OversizedWriteResponsesRemainUnknownAndReservedAfterRestartAndExpiry|FullyQualifiedName~GlassRepairEstimateGatewayTests.AnOversizedReadOnlyResponseRemainsADefinitePreWriteRefusal|FullyQualifiedName~GlassRepairEstimateGatewayTests.CancellationIsDurableAndOnlyKnownStagesResume|FullyQualifiedName~GlassRepairEstimateGatewayTests.ARecordedVehicleResumesAfterHostLossWithoutCreatingAnotherVehicle|FullyQualifiedName~GlassRepairEstimateGatewayTests.AnOversizeExportIsRefusedRatherThanBuffered

Stop before commit/push or returning Review until root supplies exact runtime
evidence. Later commit must update this existing PR, not create another one;
record its new SHA here. No self-review, merge or deployment.

## Round-1 compiler feedback

Root correction build 1 FAIL, exit 1, 21.70 s: CA1068 on
GlassMvaClient.ReadAsync requires CancellationToken to be the final parameter.
Author reordered outcomeUnknown before CancellationToken in the private
signature and its two callers only; behavior and tests are unchanged.
Author git diff --check PASS, exit 0 (CRLF warnings only). Candidate frozen
for root incremental build and the same ten-case correction cohort. No author
build or test run, and the failed attempt remains recorded.

## Round-1 root verification completed

After the signature-only CA1068 correction, root's Integration project build
PASS, 49.39 s. The ten-case review-correction filter above then PASS: ten total,
ten executed/passed, zero failures, errors, skips or inconclusive results,
53 s. Author read the TRX counters and case names independently, without
rerunning the tests. Retained TRX:
tests/Pegasus.IntegrationTests/TestResults/eng-041-review-correction.trx
SHA-256 AC6BBEA97CFF7854B40D48515ABC0D900DCE79C3B5D92C48E97CE2F5046510B2.
All five new cases and the five selected existing recovery/size cases are
present and passed. Earlier failures remain above; no full suite, page capture,
live Glass call or cloud change was repeated. Final author diff check PASS exit0.
Root authorizes commit/push to existing PR683 and return Review. Both findings
are fixed in this candidate pending independent delta review; the author does
not change the reviewer's disposition record or merge.

## Round-1 pushed head

Correction commit 8bbceb4fd190ae80a8b656540fd0ae5973f49895 follows the original
reviewed head without rewriting it. Same PR683/branch, dev target; three files,
174 insertions and ten deletions. F-001 and F-002 remedies are both in this
commit. Root-authorized [skip ci] avoids duplicate speculative rails; independent
review/checks and later exact-merge verification remain separate obligations.
Worktree is clean after push. Hand off for independent SHA-bound delta review.

## Post-merge correction — frozen for root verification

Root's exact merged proof73d3327f6364834c is FAIL at
baafa29e0f7002b8235aa43bf333f5d9bb172828: locked restore/build and Core56 passed;
Integration157 passed and two actual Glass callback tests failed, expecting
Completed but receiving AwaitingImport. The full proof/TRXs, original failures,
review findings and earlier passes remain unchanged. No deployment claim.

Root approved correction planf0aa4318d6dca111/files1d4c177e23c8d8b9 and assigned
principal_delivery_audit as AUTHOR for this follow-up. That agent's earlier
independent PR683 review is historical and cannot review this correction.
Approved execution stop is plan c096b7ddf32366de. The retained claim/root/
branch/common Git were validated; only ENG-041 names this worktree.

### Base refresh and explicit merge exception

Normal non-rewriting merge of accepted dev
19e6f523bf6760cab39104b4dca3674b0ac8a512 into the clean retained author8bbceb4
paused at one generated snapshot conflict. Root inspected and authorized
merge-only resolution to the exact accepted-dev blob, performed with apply_patch:
docs/design/test-ui/pages/case-details--conflict.html.
Resolved and accepted blob both97f00094f956f20e37a873125150cb9c1f0ce17c.
Auto-merged application/test paths and then the whole tree compared equal to
accepted dev; no unmerged path remained. Normal merge commit
188d3e16fd0b93a78e37a0dbc5483f5bd06410f5 preserves parents
8bbceb4fd190ae80a8b656540fd0ae5973f49895 and19e6f523bf6760cab39104b4dca3674b0ac8a512.
This exception is not future snapshot edit authority; the correction has no UI
change and no generated-file delta against accepted dev.

### Correction and actual callers

Seven files, +208/-69, uncommitted over that merge; source is frozen.

- EfCaseArtifactCustody removes automatic workflow load/version increment/lease
  clearing. The existing immediate and reconciliation confirmation transactions
  still atomically save source staleness/history and exempt actual report-output
  operation identities. Explicit staff Add/Remove commands stay unchanged.
- EfAssessmentReportProjectionSource extracts its exact existing confirmed
  occurrence/version query and two mappings into internal static helpers in the
  same class. Its existing row is reused, not a new DTO/interface/store. Query
  preserves Case scope, matching document/version, current/not removed/Confirmed,
  generated-output operation exclusion and ordinal order.
- EfCaseReportGenerationStore uses those helpers inside its existing serializable
  freeze transaction, after ordinary authority/version checks, before readiness,
  generation reuse and writes. Ordered full source records and all occurrence-
  keyed confirmed metadata must equal captured inputs. A content-safe ordinary
  refusal writes nothing on mismatch. Existing Case-version/signatory guards
  remain; no provider version refresh, guard bypass or lease reacquisition.
- GlassRepairEstimateCallbackWebTests preserves both failing journey assertions
  and adds Case-version accounting: only imported Draft is a Case mutation;
  duplicate callback adds none.
- CaseArtifactCustodyRecoveryTests proves actual immediate/recovered custody
  preserves current Case version/live token/holder/expiry and exact identity
  replay. Existing pending/reconciliation/removed-document tests retain meaning.
- Reports/CaseReportGenerationPersistenceTests snapshots the complete mapped
  fixture census once, preserving stale-input behavior. Independent persisted
  mutation cases exercise addition, removal, equal-size changed occurrence,
  logical version, hash, name, media, length, Box file/version, currentness and
  custody state. Outside authority still passes at unchanged Case version, while
  freeze refuses with no generation/artifact. Existing runtime-role assertions
  now prove stale output and preserved authority, not arbitrary version2.
  Frozen sources explicitly include all three seeded documents with exact IDs.
- FRD-06 Glass's section states own artifact retention preserves valid authority;
  genuine intervening edits/expired or lost lease still defer import. FRD-11 and
  CASE-049 handoff/access files are unchanged; no current file overlap.

The production route remains callback -> Glass ExportAsync ->
registered EfCaseArtifactCustody -> FinishAsync -> ImportRawEstimate ->
existing estimate mutation. Report generation uses its existing projection and
freeze port. No new package/schema/grants/runtime/provider/cloud call.

### Author checks and requested root evidence

Author `git diff --check` exit0 (CRLF normalization warnings only), plus bounded
source/caller/metadata inspection. No build, test, snapshot capture, CI, provider
or cloud call. Code inspection and authored assertions are not runtime PASS.

Exact requested Integration filter:

FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheProvidersReturnLandsTheDraftKeepsBothDocumentsAndCompletesTheSession|FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheSameReturnDeliveredTwiceRecordsNothingASecondTime|FullyQualifiedName~CaseArtifactCustodyRecoveryTests.FailedWriteLeavesOnePendingIntentAndReplayUsesTheSameVersionIdentity|FullyQualifiedName~CaseArtifactCustodyRecoveryTests.AutomaticCustodyPreservesLiveCaseAuthority|FullyQualifiedName~Pegasus.IntegrationTests.Reports.CaseReportGenerationPersistenceTests

Root owns the needed build and focused Integration project test command with
--configuration Release --no-build and its uniquely named correction TRX.
Do not rerun unchanged157 just for this fix. Original proof's missed
default/conflict/unavailable captures remain root's separate final evidence
obligation using PEGASUS_TEST_UI_CAPTURE_DIR, not incorrect former variables.

Stop before commit/push/Review until root supplies actual results. Since PR683
is merged, the later approved handoff needs a NEW dev-targeting follow-up PR,
not an update claimed against a closed PR. Independent review and exact-follow-up
merged verification remain owed. Worktree/claim and original FAIL are retained.

## Post-merge correction attempt 1 and exact helper correction

Root job94384 finished exit1. The correction build passed in 51.75 s with zero
warnings/errors. Focused Integration reported 48 executed, 47 passed, one failed,
zero skipped, in 2m53s. Both original callback journeys reached Completed/import;
the sole failure was the new version-accounting assertion in
TheProvidersReturnLandsTheDraftKeepsBothDocumentsAndCompletesTheSession: expected
launchVersion + 1 = 2, actual 1 at line336. This failed attempt is retained.

Author read artifacts/verification/eng-041-custody-correction.trx directly:
48 total/executed, 47 passed, one failed, zero notExecuted/error/inconclusive.
TRX start 2026-09-08T02:00:55.9266207+01:00; finish
2026-09-08T02:03:51.6984278+01:00.
SHA-256 E32170BDAD1A4F74B29E11309C2F96041CCF933AA007378D3BDF9E2CD8994D81.
Root reports all three correct fresh capture tests passed, with inputs retained
in artifacts/eng-041-correction-capture. Snapshot verify and catalogue were not
attempted because the guarded command stopped on the failing test. No visual
pass, complete verification or deployment is claimed.

Diagnosis: the existing test helper CaseVersionAsync queried CaseEntity.Version
in Cases, whereas the real Glass authority and EfRepairSpecificationStore guard
use CaseWorkflows.Version. Successful estimate import increments that workflow
version and clears its lease through the existing Guard, not Cases.Version.
Both fixture versions begin at 1, masking the helper error until the new
post-import assertion. This is a wrong test observation, not evidence that
import intentionally avoids the real Case mutation.

After full run completion root explicitly approved changing ONLY that helper
query from context.Set<CaseEntity>() / item.Id to context.CaseWorkflows /
item.CaseId. Applied those two lines; Select(Version), SingleAsync, the +1
assertion and the exact replay/no-extra-version assertion are unchanged.
No production code changed after root's attempt. The correction remains seven
files, now +210/-71 over188d3e16. Author git diff --check passed exit0 with only
line-ending normalization warnings. Source is frozen again.

Changed helper has four method callers: the failed callback, duplicate callback,
exact-version/live-lease authority test, and two-row missing-vehicle-facts theory.
Proposed minimal root rerun is therefore five cases under this exact filter:

FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheProvidersReturnLandsTheDraftKeepsBothDocumentsAndCompletesTheSession|FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheSameReturnDeliveredTwiceRecordsNothingASecondTime|FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheRealCaseAuthorityRequiresTheExactVersionAndLiveLease|FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheRealCaseAuthorityRefusesIncompleteVehicleFacts

Root owns the necessary incremental build and test invocation. No build/test
was run by this author. Exact root command strings will accompany the final
reported rerun; earlier 47 passes are not erased or silently presented as a
fully passing cohort. Remain Implementing with claim/tree retained until root
supplies the focused correction result and authorizes the new follow-up PR.

### Root attempt 1 exact commands

Working directory: .worktrees/eng-041, PowerShell 7 on Windows.
Root supplied and author records the actual commands (not an author rerun):

```powershell
dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheProvidersReturnLandsTheDraftKeepsBothDocumentsAndCompletesTheSession|FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheSameReturnDeliveredTwiceRecordsNothingASecondTime|FullyQualifiedName~CaseArtifactCustodyRecoveryTests.FailedWriteLeavesOnePendingIntentAndReplayUsesTheSameVersionIdentity|FullyQualifiedName~CaseArtifactCustodyRecoveryTests.AutomaticCustodyPreservesLiveCaseAuthority|FullyQualifiedName~Pegasus.IntegrationTests.Reports.CaseReportGenerationPersistenceTests|FullyQualifiedName~CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName~TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor' --logger 'trx;LogFileName=eng-041-custody-correction.trx' --results-directory ./artifacts/verification
```

Build exit0/51.75s; test exit1 with the counters/retained hash above.
PEGASUS_TEST_UI_CAPTURE_DIR was the absolute resolved
.worktrees/eng-041/artifacts/eng-041-correction-capture;
PEGASUS_TEST_UI_SCOPE=case-details; PEGASUS_TEST_UI_MODE unset.
No snapshot verification/catalogue command ran after that guarded failure.

## Post-merge correction final source verification — PASS

Root supplied final job76428 exit0 after the exact helper correction. No
production changes were made after attempt1; all four helper caller methods
(five cases) passed. The original exact-merge FAIL and attempt1's 47 PASS /
1 FAIL remain above and in their retained TRXs. Final source acceptance reuses
the unchanged production/source-census/custody checks from those 47 passes;
it does not pretend that attempt1 was an entirely passing run.

Actual commands at .worktrees/eng-041:

```powershell
dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheProvidersReturnLandsTheDraftKeepsBothDocumentsAndCompletesTheSession|FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheSameReturnDeliveredTwiceRecordsNothingASecondTime|FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheRealCaseAuthorityRequiresTheExactVersionAndLiveLease|FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheRealCaseAuthorityRefusesIncompleteVehicleFacts' --logger 'trx;LogFileName=eng-041-workflow-version.trx' --results-directory ./artifacts/verification
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~TestUiSnapshotTests' --logger 'trx;LogFileName=eng-041-custody-snapshots.trx' --results-directory ./artifacts/verification
```

- Integration-project build: exit0, 17.14s, zero warnings/errors.
- Helper caller cohort: exit0, five executed/passed, zero failures/skips, 57s.
  TRX start2026-09-08T02:09:35.5228746+01:00;
  finish2026-09-08T02:10:35.3490419+01:00.
  artifacts/verification/eng-041-workflow-version.trx SHA-256
  C3AD73073CD8FC5EBA55F957624C0F08174D33D4A8A0B16EBE96F0711C691FA9.
- Snapshot verification: exit0, two executed/passed, zero failures/skips, 6s.
  Reused the already fresh three passing Case Details captures from attempt1;
  no recapture. PEGASUS_TEST_UI_CAPTURE_DIR resolves to
  artifacts/eng-041-correction-capture, PEGASUS_TEST_UI_SCOPE=case-details,
  PEGASUS_TEST_UI_MODE=verify. TRX start2026-09-08T02:10:37.7274401+01:00;
  finish2026-09-08T02:10:47.4070397+01:00.
  artifacts/verification/eng-041-custody-snapshots.trx SHA-256
  7F1CD86E6486623820372DB9A48A96049FA72A7686B8AEC8A72A359D0BD5F7C3.
- Root Test-UiCatalogue PASS: 60 routed pages, 67 prototypes, zero broken refs.

Author independently read both final TRX counters, instants and hashes and
verified the exact seven-file source census plus git diff --check exit0.
No author build/test, manual visual pass or cloud/provider operation.
Generated snapshots have no correction delta; source freshness is verified
against genuine retained captures, not an invented visual approval.

Root explicitly authorizes final report/checklist, scoped [skip ci] commit,
normal push and a NEW dev-targeting follow-up PR because683 is already merged.
Live read-back confirms PR683 MERGED atbaafa29e and no other open PR for this
branch before creation. The earlier author history remains reachable through
normal merge188d3e16; no reset, rebase or force-push. Root's skip-ci instruction
avoids duplicate full suites only; required checks are not bypassed, and final
converged CI/release verification still belong to the root controller.

Independent exact-new-head review is next. This author must not review or
merge this follow-up. A later verifier needs the exact follow-up merge SHA and
proportionate custody/report/Glass caller checks, comparing immutable source
inputs before reusing the unchanged47. No Done, deployment or cleanup claim.

## New follow-up PR handoff

PR691: https://github.com/collisionengineers/pegasus/pull/691
Exact pushed head: b253306f048dfb8e1dd635e9994835b32c0090d1.
GitHub and origin branch read-back agree; OPEN against dev, same retained
ENG-041-glass-recovery branch and .worktrees/eng-041, clean after push.
Seven files, +210/-71. The scoped root-authorized commit includes [skip ci].
PR683 stays recorded as merged historical work; PR691 is the new post-merge
correction and is also recorded in prs[]. No history rewriting or extra scope.

Root owns review/merge scheduling. Stop after fresh gates to Review and hand
this exact head to an independent reviewer. Original FAIL proof73d3327f6364834c
is unchanged; no self-review, merge, Done, deployment or cleanup by this author.
