---
kind: proof-record
merged_sha: "cc441645b0a62a806e34367ad75e9eaff4df8b11"
environment: ".worktrees/verify-eng-041-cc441645b0a62a806e34367ad75e9eaff4df8b11; Windows x64; PowerShell 7"
verified_at: "2026-09-08T01:53:38Z"
result: PASS
attempts:
  - attempted_at: "2026-09-08T00:20:40Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    exit_code: 0
    result: PASS
    summary: "Original PR683 exact merge: locked restore."
  - attempted_at: "2026-09-08T00:20:40Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    exit_code: 0
    result: PASS
    summary: "Original PR683 exact merge:61.48s, zero warnings/errors."
  - attempted_at: "2026-09-08T00:21:54Z"
    command: "Original Core56 command retained below"
    exit_code: 0
    result: PASS
    summary: "Original exact merge:56 passed,0 failed/skipped,212ms."
  - attempted_at: "2026-09-08T00:21:58Z"
    command: "Original Integration159 command retained below"
    exit_code: 1
    result: FAIL
    summary: "Original exact merge:157 passed,2 failed callbacks AwaitingImport,0skips,2m42s. Implementation defect fixed by PR691; original artifacts retained."
  - attempted_at: "2026-09-08T01:41:00Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    exit_code: 0
    result: PASS
    summary: "Clean detached cc441645b exact merge, locked restore. Approximate observed start minute."
  - attempted_at: "2026-09-08T01:41:00Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    exit_code: 0
    result: PASS
    summary: "125.81s, zero warnings/errors; no other heavy verifier."
  - attempted_at: "2026-09-08T01:43:20Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheProvidersReturnLandsTheDraftKeepsBothDocumentsAndCompletesTheSession|FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheSameReturnDeliveredTwiceRecordsNothingASecondTime|FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheRealCaseAuthorityRequiresTheExactVersionAndLiveLease|FullyQualifiedName~GlassRepairEstimateCallbackWebTests.TheRealCaseAuthorityRefusesIncompleteVehicleFacts|FullyQualifiedName~CaseArtifactCustodyRecoveryTests.FailedWriteLeavesOnePendingIntentAndReplayUsesTheSameVersionIdentity|FullyQualifiedName~CaseArtifactCustodyRecoveryTests.AutomaticCustodyPreservesLiveCaseAuthority|FullyQualifiedName~Pegasus.IntegrationTests.Reports.CaseReportGenerationPersistenceTests|FullyQualifiedName~CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName~TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor' --logger 'trx;LogFileName=eng-041-merged-correction.trx' --results-directory ./artifacts/verification"
    exit_code: 0
    result: PASS
    summary: "51 passed,0failed/0skipped,3m9s. Actual callback, source-race, automatic custody authority and route captures."
  - attempted_at: "2026-09-08T01:46:33Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~TestUiSnapshotTests' --logger 'trx;LogFileName=eng-041-merged-snapshots.trx' --results-directory ./artifacts/verification"
    exit_code: 1
    result: FAIL
    summary: "1passed/1failed: default snapshot differed. Verification input defect: old capture selection supplied one-off Edit mode is active notice, not canonical default."
  - attempted_at: "2026-09-08T01:49:54Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~CaseDetailsWebTests.NativeHandoffDialogPostsWithoutEvaOrASeparateReviewAction' --logger 'trx;LogFileName=eng-041-canonical-handoff-capture.trx' --results-directory ./artifacts/verification"
    exit_code: 0
    result: PASS
    summary: "Canonical current default capture,1passed/0failed/0skipped,35s; no rebuild/source change."
  - attempted_at: "2026-09-08T01:50:34Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~TestUiSnapshotTests' --logger 'trx;LogFileName=eng-041-canonical-snapshots.trx' --results-directory ./artifacts/verification"
    exit_code: 1
    result: FAIL
    summary: "Default now matched;1passed/1failed conflict. Old completeness-refusal input is not current Lease gone canonical conflict. No expected-output update."
  - attempted_at: "2026-09-08T01:52:30Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~CaseDetailsWebTests.WorkflowPageBindsReviewReturnEngineerAssignmentFindingAndLinkedReplacement' --logger 'trx;LogFileName=eng-041-canonical-conflict-capture.trx' --results-directory ./artifacts/verification"
    exit_code: 0
    result: PASS
    summary: "Exact existing canonical conflict caller;1passed/0failed/0skipped,34s; no rebuild/source change."
  - attempted_at: "2026-09-08T01:53:09Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~TestUiSnapshotTests' --logger 'trx;LogFileName=eng-041-final-snapshots.trx' --results-directory ./artifacts/verification"
    exit_code: 0
    result: PASS
    summary: "2passed/0failed/0skipped,7s against unchanged committed output; fresh exact-merge captures."
  - attempted_at: "2026-09-08T01:53:00Z"
    command: "pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1"
    exit_code: 0
    result: PASS
    summary: "60routed sources,67prototypes,0broken local references. Approximate observed minute."
  - attempted_at: "2026-09-08T01:53:00Z"
    command: "git status --porcelain; git rev-parse HEAD; TRX counter/time/SHA256 census"
    exit_code: 0
    result: PASS
    summary: "Clean exact cc441645b; all six merged proof artifacts read and hashed. Approximate observed minute."
---

# ENG-041 final exact integrated acceptance

PASS at confirmed dev follow-up merge cc441645b0a62a806e34367ad75e9eaff4df8b11
(PR691, mergedAt2026-09-08T01:39:46Z). Root read whole independent final
review6abcbc32911d7b8b/public5136361089 at authorb253306f048dfb8e1dd635e9994835b32c0090d1,
plan c096b7ddf32366de/report66303a1fa5639466, and refreshed unchanged head,
plan/ticket, complete-empty live review threads/checks/rules and synchronized
board before guarded normal merge. No forced merge or CI-green inference.
Review author and verifier roles are distinct; principal_delivery_audit wrote
the correction, pack_reconcile reviewed it, root verified this merge.

The full author-to-merge diff is the already accepted disjoint CASE-049 native
handoff. Exact detached root and common Git were checked before execution;
postflight remained clean and at the exact same SHA. No mutable author/shared
checkout was used as merged evidence. Root was the sole heavy verifier.

## Proven actual callers and invariants

Both original failed callback journeys now complete with one imported Draft
and retained XML/PDF sources; repeated delivery creates no additional import
or workflow mutation. Real current-version/live-lease and missing-vehicle
refusals remain. Immediate and recovered automatic custody preserve active
Case authority and replay identities while atomically marking report source
changes stale. Explicit staff custody remains a Case mutation.

The actual report projection/freeze owner detects complete source membership,
identity, logical version, hash, name, media, length, Box identity, currentness
and custody changes inside its serializable transaction before writes/reuse.
Every tested stale-capture race refuses without a new generation/artifact;
ordinary lease/version/signatory requirements and generated-output identity
exclusion remain. The 51-case merged cohort passed, with no skips. The two
additional canonical Case caller cases passed and supplied current default/
conflict captures. Final snapshot verification2/2 and catalogue60/67/0 passed.

No production or expected snapshot changed during verification. Earlier
Core56/Integration157 passing cases at baafa29e remain source-scoped evidence,
not a claim that the entire original159 cohort reran at cc441645b. The changed
custody/report/callback paths and newly integrated native UI callers were
exercised here. Final converged CI/release remains a separate controller task.

## Two preserved verification-input failures

The first snapshot attempt used an old Case capture selection from before
CASE-049. Its default capture was the first GET following edit-mode entry,
with the transient success notice, whereas accepted default is the native
handoff test's subsequent normal GET. Adding that one existing canonical test
(no rebuild) made default match; the conflict still differed. Read-only HTML
comparison then identified old completeness refusal versus accepted workflow
lost-lease refusal (reason Lease gone, review-evidence-1). The exact existing
WorkflowPageBindsReviewReturnEngineerAssignmentFindingAndLinkedReplacement
caller supplied that state. Both originate in CaseWorkflowWebTests.cs, whose
partial class is CaseDetailsWebTests. This is why file-name guesses are not
capture filters. After that one caller, final verification passed unchanged.

These are real failed attempts, retained above and in their TRXs; they are not
silently turned green, source regressions, nor justification to update the
expected page or weaken the predicate. The initial51 functional passes remain
valid. Catalogue was not attempted after either guarded snapshot failure.

All merged commands used .worktrees/verify-eng-041-cc441645b0a62a806e34367ad75e9eaff4df8b11. During capture,
PEGASUS_TEST_UI_CAPTURE_DIR was its absolute artifacts/eng-041-merged-capture,
PEGASUS_TEST_UI_SCOPE=case-details and MODE unset. Each snapshot check set
PEGASUS_TEST_UI_MODE=verify against that same retained directory. Exact UTC TRX
instants below convert their recorded +01:00 offsets; restore/build/catalogue
minutes are approximate observations, not fabricated precise timestamps.

## Retained merged artifacts

Artifacts currently reside in this exact verification root's artifacts/verification.
Closeout must copy and hash-check them before removing any temporary workspace.

| TRX | SHA256 | UTC start–finish, 8 September |
| --- | --- | --- |
| eng-041-merged-correction.trx | 28718553E2FB3E07ADF7EE966ED15CF629972E15FF5E046A36E7FD6B7621CB55 | 01:43:20.1066928Z–01:46:31.7224463Z |
| eng-041-merged-snapshots.trx | 342FB3ED27F4FDB91935AC9393AE1944FB7ED8A772FEA0BE0E7682440F24F8B0 | 01:46:33.6509526Z–01:46:36.9127559Z |
| eng-041-canonical-handoff-capture.trx | D5578D04E34F3A493EE6BB7CE6420C436418CA0B007A69FA24F8F21F6B609BD9 | 01:49:54.9569479Z–01:50:32.3715555Z |
| eng-041-canonical-snapshots.trx | 95169B16CAE8CD4AC7F91B38B19CB7A3ECF849D2AF3C55EF4E5487222F90B052 | 01:50:34.6156228Z–01:50:37.6308566Z |
| eng-041-canonical-conflict-capture.trx | B8D1476C566C5D62A5BD14DA4B06622EB545E007170ED30C48DA5F243BE10C3F | 01:52:30.4075307Z–01:53:07.4195151Z |
| eng-041-final-snapshots.trx | 25779A7788529929AED38AEF7D71F19C723C4BAFC5DFC134A0C8E808CDBC8BC8 | 01:53:09.1704000Z–01:53:18.7569321Z |

The original two merged TRXs, plus three author-correction TRXs named in
report66303a1fa5639466/review6abcbc32911d7b8b, must also be preserved at closeout.
No live Glass, Azure, mailbox write, send, wipe or deployment occurred.
This is integrated functional acceptance, not production activation. The board
reports this existing legacy proof-record format as untyped under report mode;
that is not a typed-schema validation claim. Root read this whole record before
Done. Canonical full-solution CI/artifact/deployment acceptance remains separate.

## Original exact-merge failed proof preserved

The following body is retained verbatim from proof73d3327f6364834c. Its original
frontmatter attempts and FAIL outcome are retained in the attempt list above;
its pending diagnosis/CASE-049 statements are historical, superseded by the
confirmed correction and final acceptance above, not current blockers.


# ENG-041 exact integrated verification — attempt 1

FAIL at PR683's confirmed dev merge baafa29e0f7002b8235aa43bf333f5d9bb172828.
Clean detached worktree with exact HEAD and correct shared Git common directory,
not the mutable author, shared checkout or board worktree. Postflight Git status
remains clean. Independent review27fbd21c5f009f14 passed author
8bbceb4fd190ae80a8b656540fd0ae5973f49895; it does not replace this merged check.

## Commands and observed failures

Core:
`dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~Pegasus.Core.Tests.Assessment.EstimateTests' --logger 'trx;LogFileName=eng-041-merged-core.trx' --results-directory ./artifacts/verification`

Integration:
`dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~GlassRepairEstimateGatewayTests|FullyQualifiedName~GlassRepairEstimatePersistenceTests|FullyQualifiedName~GlassRepairEstimateCallbackWebTests|FullyQualifiedName~AssessmentEstimateImportWebTests|FullyQualifiedName~AssessmentPersistenceIntegrationTests.EarlierEstimateUpdateReplaysItsRecordedIdentityWithoutRevertingLaterEdits|FullyQualifiedName~CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName~TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor' --logger 'trx;LogFileName=eng-041-merged-integration.trx' --results-directory ./artifacts/verification`

Two failures, both in GlassRepairEstimateCallbackWebTests:

- TheSameReturnDeliveredTwiceRecordsNothingASecondTime, line389.
- TheProvidersReturnLandsTheDraftKeepsBothDocumentsAndCompletesTheSession,
  line325.

Both expected Completed, received AwaitingImport. Root has assigned read-only
diagnosis of the integrated custody/import interaction; the precise cause is
not yet confirmed. No assertion was weakened and no fresh PASS is claimed.

The shell guards stopped after the failed test command. Snapshot verification
and catalogue commands were NOT run. Root also observed that this command
mistakenly set unused PEGASUS_TEST_UI_CAPTURE/PEGASUS_TEST_UI_CAPTURE_ROOT
variables instead of the actual PEGASUS_TEST_UI_CAPTURE_DIR. Therefore the
passing route tests are functional evidence only, not fresh capture evidence.
The later correction must use the existing actual capture variable and selected
three route tests; do not rerun the whole passing cohort just to capture HTML.

## Artifacts and chronology

All artifacts currently remain in this verification worktree's
artifacts/verification. UTC TRX start/end instants below are converted from
recorded +01:00 values; approximate restore/build boundary times are marked
as such by this statement.

- eng-041-merged-core.trx:
  SHA256 426FB59B3880AC1DDC1967D5047E8CE9C37FECA68591DD8E79BEBE23FC985D54;
  00:21:54.7287689Z–00:21:56.5038977Z.
- eng-041-merged-integration.trx:
  SHA256 0C07D7F78B1928A9CE2FCD324223298612F7F34FCE7788F0A8C440BF735D2ED5;
  00:21:58.1139049Z–00:24:42.1965869Z.

Earlier author compiler failures, clock-double mismatch, missing-default
capture, review F-001/F-002 and CA1068 correction remain in the full original
reportf437cfd8a750979b and independent review history. This merged failure is
additional evidence and is not erased by those earlier passes.

## Disposition

Remain not Done, not deployed. No live Glass call, cloud mutation, source edit
or claim cleanup was performed by this verification. Preserve both worktrees,
both TRXs and the recorded author claim. After confirming the cause, route the
bounded correction through the existing ticket with a new dev-targeting PR,
independent review and focused exact-follow-up proof. CASE-049 remains untaken
until this shared-file owner is released.


## Closeout evidence retention — 2026-09-08

The final PASS above remains bound to exact merge
cc441645b0a62a806e34367ad75e9eaff4df8b11. This is record-keeping, not a
new test run or a replacement of any failed attempt. The original proof,
whole report66303a1fa5639466 and review6abcbc32911d7b8b were read in full.

Fresh GitHub read confirms [PR683](https://github.com/collisionengineers/pegasus/pull/683)
merged to dev at 2026-09-07T23:20:55Z, SHA
baafa29e0f7002b8235aa43bf333f5d9bb172828, and
[PR691](https://github.com/collisionengineers/pegasus/pull/691) merged to dev at
2026-09-08T01:39:46Z, SHA cc441645b0a62a806e34367ad75e9eaff4df8b11.
After fresh origin/dev fetch, both full merged SHAs pass
git merge-base --is-ancestor (exit0); origin/dev is the latter exact SHA.
Ticket commit traceability uses these integrated merges. Original author
SHAs remain preserved in the report/review as historical author evidence,
not mislabelled integration ancestry.

Before any Git cleanup, all 14 TRXs found across the three authorized
worktrees were copied to ignored pegasus_pack/current/proofs/ENG-041/
with the following relative paths. Every source/copy SHA-256 matched.
The twelve pre-recorded hashes also match the report/proof exactly; the
initial focused/correction hashes are newly measured retention evidence,
not invented historical attestations. Counters were read from each TRX.

| Retained path under ENG-041 | Passed / failed / skipped | SHA-256 |
| --- | --- | --- |
| author/tests/Pegasus.IntegrationTests/TestResults/eng-041-review-correction.trx | 10 / 0 / 0 | AC6BBEA97CFF7854B40D48515ABC0D900DCE79C3B5D92C48E97CE2F5046510B2 |
| author/tests/Pegasus.IntegrationTests/TestResults/eng-041-focused.trx | 153 / 1 / 0 | CB23C3CE09E3344F522280A9624084F3A12D50F74408228367E47DC13557B716 |
| author/tests/Pegasus.IntegrationTests/TestResults/eng-041-correction.trx | 2 / 0 / 0 | F1220B976D5679AA979E4F5F2DFEF5C9EDEFAAF84E0A98F749BA4D124B613A1D |
| author/artifacts/verification/eng-041-workflow-version.trx | 5 / 0 / 0 | C3AD73073CD8FC5EBA55F957624C0F08174D33D4A8A0B16EBE96F0711C691FA9 |
| author/artifacts/verification/eng-041-custody-snapshots.trx | 2 / 0 / 0 | 7F1CD86E6486623820372DB9A48A96049FA72A7686B8AEC8A72A359D0BD5F7C3 |
| author/artifacts/verification/eng-041-custody-correction.trx | 47 / 1 / 0 | E32170BDAD1A4F74B29E11309C2F96041CCF933AA007378D3BDF9E2CD8994D81 |
| merged-baafa29e/artifacts/verification/eng-041-merged-integration.trx | 157 / 2 / 0 | 0C07D7F78B1928A9CE2FCD324223298612F7F34FCE7788F0A8C440BF735D2ED5 |
| merged-baafa29e/artifacts/verification/eng-041-merged-core.trx | 56 / 0 / 0 | 426FB59B3880AC1DDC1967D5047E8CE9C37FECA68591DD8E79BEBE23FC985D54 |
| merged-cc441645/artifacts/verification/eng-041-merged-snapshots.trx | 1 / 1 / 0 | 342FB3ED27F4FDB91935AC9393AE1944FB7ED8A772FEA0BE0E7682440F24F8B0 |
| merged-cc441645/artifacts/verification/eng-041-merged-correction.trx | 51 / 0 / 0 | 28718553E2FB3E07ADF7EE966ED15CF629972E15FF5E046A36E7FD6B7621CB55 |
| merged-cc441645/artifacts/verification/eng-041-final-snapshots.trx | 2 / 0 / 0 | 25779A7788529929AED38AEF7D71F19C723C4BAFC5DFC134A0C8E808CDBC8BC8 |
| merged-cc441645/artifacts/verification/eng-041-canonical-snapshots.trx | 1 / 1 / 0 | 95169B16CAE8CD4AC7F91B38B19CB7A3ECF849D2AF3C55EF4E5487222F90B052 |
| merged-cc441645/artifacts/verification/eng-041-canonical-handoff-capture.trx | 1 / 0 / 0 | D5578D04E34F3A493EE6BB7CE6420C436418CA0B007A69FA24F8F21F6B609BD9 |
| merged-cc441645/artifacts/verification/eng-041-canonical-conflict-capture.trx | 1 / 0 / 0 | B8D1476C566C5D62A5BD14DA4B06622EB545E007170ED30C48DA5F243BE10C3F |

The two proof-named correction capture directories are also retained:
author/artifacts/eng-041-correction-capture and
merged-cc441645/artifacts/eng-041-merged-capture. All 104 individual capture
files were copied and source/destination hashes verified. Full source-path
mapping, counters and per-file hashes are in
pegasus_pack/current/proofs/ENG-041/manifest.json.
Committed normalized snapshots remain in the accepted Git tree; no manual
visual PASS is inferred from archival.

The original callback failures, wrong-version-helper failure, initial clock
failure and both exact-merge snapshot-input failures remain FAIL evidence.
Later focused corrections did not erase or relabel them. Historical absolute
worktree paths above identify where commands actually ran; the retention
paths here are their durable local artifact locations after cleanup.

Fresh complete include-archived board census finds only ENG-041 occupying
its branch/worktree, with no batch. All three authorized roots resolve to
their explicit repository-contained paths, share the source .git directory,
have the exact expected author/merged HEADs and clean tracked/untracked
Git status. First read-only guard attempt exited1 because detached branch
output was null in PowerShell; normalizing the empty branch string fixed
only that inspection and its rerun passed. No Git removal was attempted
on that refusal. Lease renewed by exact CAS to revision19, closeout phase.

No build, test, application edit, cloud/provider/mail action or deployment
occurred during closeout. Outcome is integrated and accepted on dev, not
deployed. Git cleanup and claim release are tracked separately below.


## Authorized Git cleanup completed

All three explicit clean roots were removed with normal git worktree remove:
.worktrees/eng-041,
.worktrees/verify-eng-041-baafa29e0f7002b8235aa43bf333f5d9bb172828 and
.worktrees/verify-eng-041-cc441645b0a62a806e34367ad75e9eaff4df8b11.
Normal git branch -d ENG-041-glass-recovery passed, then the exact remote
branch deletion passed. Git noted the author tip was merged to its upstream
but not the stale shared-checkout HEAD; no force flag, reset, rebase or source
checkout was used. Integrated merge reachability was already verified above.

Fetch --prune origin passed. Worktree-prune dry run had no candidates, and
normal prune passed. Exact path/ref/server checks confirm all three roots
and local/tracking/remote ENG-041 branch are absent. Before/after registered
worktree paths agree after subtracting precisely those three roots; all
foreign worktrees and claims, including the board and shared source checkout,
remain untouched. The stored proof readback differed only in CRLF normalization;
a complete LF-normalized comparison matched before cleanup.

All 14 TRXs and 104 capture files were rehashed from their archive manifest
immediately before removal. Accepted source remains recoverable from the
two merged PRs and dev; failed and passing evidence remains in the ignored
proof archive. Claim release is the final ownership operation, after this
record and completed Git checks; no work remains in flight for ENG-041.
