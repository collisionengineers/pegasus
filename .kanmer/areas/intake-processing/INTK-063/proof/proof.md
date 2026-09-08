---
kind: proof-record
merged_sha: "a022fc4b2db87d4d2eeb14437b41f6d8344d63e6"
environment: ".worktrees/verify-intk-063-a022fc4b2db87d4d2eeb14437b41f6d8344d63e6; Windows / PowerShell 7; Release; existing SQL integration fixture"
verified_at: "2026-09-08T04:43:00.9598445Z"
result: PASS
attempts:
  - attempted_at: "2026-09-08T04:03:28.6642250Z"
    command: "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~ImageIntakeCasePairingTests|FullyQualifiedName~AutomaticImageIntakeTests|FullyQualifiedName~ImageIntakeLifecycleTests|FullyQualifiedName~ImmediateExternalPublicationTests\" --logger \"trx;LogFileName=intk-063-core-corrected.trx\""
    cwd: ".worktrees/intk-063"
    exit_code: 0
    result: PASS
    summary: "Root session 89133 author checkpoint: 75/75 passed, no skipped/errors; final Worker seed correction was not yet applied."
  - attempted_at: "2026-09-08T04:03:32.2343630Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~ImageIntakePersistenceTests|FullyQualifiedName~StagedArtifactReconciliationFunctionIntegrationTests|FullyQualifiedName~ImageCaseCustodyIntegrationTests.RegistrationStoresEveryGroupImageAndMergeFoldsThemIntoTheCase|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce\" --logger \"trx;LogFileName=intk-063-integration-corrected.trx\""
    cwd: ".worktrees/intk-063"
    exit_code: 1
    result: FAIL
    summary: "Root session 89133: 17/18 passed, one duplicate-QDOS fixture failure before restricted Worker caller. Failure preserved; no skip/error counters."
  - attempted_at: "2026-09-08T04:11:14.7290190Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce\" --logger \"trx;LogFileName=intk-063-worker-seed-corrected.trx\""
    cwd: ".worktrees/intk-063"
    exit_code: 0
    result: PASS
    summary: "Root session 83241: corrected seeded-identity fixture, exact restricted Worker method 1/1 PASS; all assertions retained."
  - attempted_at: "2026-09-08T04:41:01.9537182Z"
    command: "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~ImageIntakeCasePairingTests|FullyQualifiedName~AutomaticImageIntakeTests|FullyQualifiedName~ImageIntakeLifecycleTests|FullyQualifiedName~ImmediateExternalPublicationTests\" --logger \"trx;LogFileName=intk-063-a022fc4b2-core.trx\""
    cwd: ".worktrees/verify-intk-063-a022fc4b2db87d4d2eeb14437b41f6d8344d63e6"
    exit_code: 0
    result: PASS
    summary: "Root exact-merge session 33369: 75/75 PASS, no failed/skipped/errors; reported test duration 155ms."
  - attempted_at: "2026-09-08T04:41:05.7646617Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~ImageIntakePersistenceTests|FullyQualifiedName~StagedArtifactReconciliationFunctionIntegrationTests|FullyQualifiedName~ImageCaseCustodyIntegrationTests.RegistrationStoresEveryGroupImageAndMergeFoldsThemIntoTheCase|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce\" --logger \"trx;LogFileName=intk-063-a022fc4b2-integration.trx\""
    cwd: ".worktrees/verify-intk-063-a022fc4b2db87d4d2eeb14437b41f6d8344d63e6"
    exit_code: 0
    result: PASS
    summary: "Root exact-merge session 33369: 18/18 PASS, no failed/skipped/errors; reported test duration 1m51s."
---

# Exact merged verification — INTK-063

## Verdict and attribution

PASS for the bounded INTK-063 acceptance at GitHub PR696's exact squash merge
`a022fc4b2db87d4d2eeb14437b41f6d8344d63e6` into dev. Root alone ran every
restore/build/test described here. Recorder `pack_reconcile` independently
read the five actual TRXs, test names/counters/timestamps, source and retained
hashes, merge identity and clean detached-worktree facts. This recorder did
not rerun runtime commands or self-review its implementation.

The exact merged run is 75 Core plus 18 integration PASS, zero failures,
errors or skipped cases. Top-level PASS is the final merged acceptance, not
an erasure of the initial author compile failure or later author 17/18
fixture failure. Native build start instants were not recorded individually:
their actual results are in the complete chronological table below, without
invented timestamp anchors. Machine attempt entries use actual TRX starts.

No Done move, cleanup, claim renewal/release, source change, deployment or
live/provider action was performed while preparing this proof. Root must
read the whole proof before its stage decision.

## Binding inputs and Git facts

- Plan `10543d55d4090f66`; files `a2712472fca53cc9`; author checklist
  `5ff6eab776e8775e`; whole report `67f2c76040cf0b1c`.
- Independent root review `4859da1b06b2cefe`, PASS on author head
  `e7db237e47322d2378ccf44749d97024db377aeb`; reviewer
  `codex-v1-remediation-root` is distinct from author pack_reconcile.
  Whole review, report and current plan/checklist were read.
- GitHub PR696 is MERGED at `2026-09-08T04:37:37Z`, mergeCommit exactly
  the frontmatter SHA, base dev. Ticket entered Verifying at 04:37:40.981Z.
- Existing exact verification root is `.worktrees/verify-intk-063-a022fc4b2db87d4d2eeb14437b41f6d8344d63e6`; HEAD equals merge,
  `symbolic-ref --short -q HEAD` is empty/exit 1 (expected detached),
  status is clean and common Git is the source repository, not the board,
  shared checkout or author root. Reused only this root-created exact
  worktree; no second worktree, checkout or mutable-branch update.
- Root's author-to-merge census and this recorder's independent Git diff agree:
  only 12 already-reviewed CASE-031/ENG-037 paths differ, +372/-34. They are
  FRD-07, AssessmentPolicy, CaseEvaApiMapping/EvaApiContracts/
  EvaSubmissionPolicy, EvaApiTransport/EvaSubmissionStore and their existing
  Assessment/Qdos/EVA/custody tests. All 19 INTK-063 scoped paths are
  byte-equivalent to the independently reviewed author head (Git diff exit 0).
  The merged runtime results consume the integrated state, not a relabelled
  author build.
- Squash ancestry is explicit: merge parent is
  `498144b0bb55b68fd53b9a31ffc89ef90622c73a`; author e7db237e4 is not an
  ancestor (exit 1), whereas merge a022fc4b2 is reachable from origin/dev
  (exit 0). Author SHA remains historical reviewed provenance; eventual
  closeout must record the integrated merge SHA in current commit/delivery
  traceability rather than claiming author ancestry.
- Dry `reconcile_ticket` returned EVIDENCE_INCONCLUSIVE with no
  recommendation: proof was absent, required-check discovery unavailable
  and recorded author commit unreachable under squash ancestry. No
  recommendation was applied. Direct GitHub/Git/actual runtime evidence here
  resolves the acceptance question; the inspector response is retained, not
  recast as PASS.

Governing behavior remains FRD-02 and current EPIC-014 brief. No historical
foreign ticket/claim was absorbed. Root's live lease revision 17,
running-command, expiry 05:38:53.019Z remains unchanged by this recorder.

## Complete chronological verification history

Exact individual restore/build start/finish times are unavailable unless
explicitly stated. The durations and exits below are root-observed facts;
not estimated times or evidence manufactured from TRX modification dates.
All author native commands ran in `.worktrees/intk-063`; all final native
commands ran in the detached root above. Each failed native command stopped
its enclosing root script.

| Attempt / executor | Actual command or phase | Exit / result and preserved disposition |
| --- | --- | --- |
| Root 65450 | `dotnet restore ./Pegasus.slnx --locked-mode` | Exit 0 PASS, all seven projects, max 1.60s. Exact individual times unavailable. |
| Root 65450 | `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | Exit 1 FAIL, 69.39s, zero warnings, CS0103 missing CultureInfo namespace at EfImageIntakeStore.cs:1274. Script stopped; no tests/TRX. Corrected by adding System.Globalization in the mapped file after root released source. |
| Root source review before next run | Actual automatic-link fingerprint inspection | No runtime attempt. Explicit anonymous RequestHash projection omitted expected staff-origin version; record JsonIgnore did not affect that projection. Corrected actual fingerprint and bounded replay assertion; disposition below. |
| Root 89133 | Same solution Release build | Exit 0 PASS, 124.08s, zero warnings. Exact individual build times unavailable. |
| Root 89133 | Author Core command in machine attempts | Exit 0 PASS, 75/75, zero skipped/errors; reported test duration 128ms. Exact TRX interval retained below. |
| Root 89133 | Author integration command in machine attempts | Exit 1 FAIL, 17/18 passed, zero skipped/errors; reported 1m50s. Only Worker recovery fixture failed while inserting migration-seeded QDOS again, before restricted caller. Original failure TRX retained. |
| Root-authorized fixture correction | Existing seeded QDOS helper only | No runtime attempt. Reused SeededPrincipals.QdosAsync Id/SequenceLineageId; removed duplicate Organization/lineage/principal inserts. Runtime assertions unchanged. |
| Root 83241 | `dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore` | Exit 0 PASS, 21.64s, zero warnings. Exact individual build times unavailable. |
| Root 83241 | Exact corrected Worker method in machine attempts | Exit 0 PASS, 1/1, zero skipped/errors, 35.3962993s. Existing restricted role/stores/caller actually executed. Prior 75 Core/17 integration passing cases were not repeated. |
| Pre-merge review freshness guard | Bot comment changed from running to completed | Refused before merge/source write, as recorded in whole independent review. Root re-gathered completed comment, reviews and empty threads; no new finding. This is not a test failure; exact guard process exit was not supplied in the review. |
| Root 33369, exact merged lane | Locked solution restore | Exit 0 PASS, seven projects, max 1.73s. Combined script start is 2026-09-08T04:38:54.5546735Z before restore, not an individual build timestamp. |
| Root 33369, exact merged lane | Solution Release build, no restore | Exit 0 PASS, 120.85s, zero warnings/errors. Exact individual build start/finish unavailable. |
| Root 33369, exact merged lane | Merged Core command in machine attempts | Exit 0 PASS, 75/75, zero skipped/errors, reported 155ms. |
| Root 33369, exact merged lane | Merged integration command in machine attempts | Exit 0 PASS, 18/18, zero skipped/errors, reported 1m51s. |
| Root 33369, postcheck | Exact HEAD/detached/clean assertions | PASS; whole script exit 0 at 2026-09-08T04:43:00.9598445Z. |
| Recorder, after completion | Read/copy/hash/diff checks | Five source/copy hashes match, all source files retained, exact detached state remains clean and INTK scoped author/merge diff exit 0. No runtime rerun. |

The two author setup failures were implementation/fixture corrections, not
unproven transient classifications. A later PASS never rewrites their red
results. No other INTK-063 author or merged TRX was present in the explicit
source-directory census, and the initial compile failure produced none.

### Exact merged native command sequence

Root session 33369 ran this sequence in the exact detached worktree, stopping
on any nonzero native exit. The combined lane interval above covers the whole
sequence and postchecks; it does not assign fabricated timing to subcommands.

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImageIntakeCasePairingTests|FullyQualifiedName~AutomaticImageIntakeTests|FullyQualifiedName~ImageIntakeLifecycleTests|FullyQualifiedName~ImmediateExternalPublicationTests" --logger "trx;LogFileName=intk-063-a022fc4b2-core.trx"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImageIntakePersistenceTests|FullyQualifiedName~StagedArtifactReconciliationFunctionIntegrationTests|FullyQualifiedName~ImageCaseCustodyIntegrationTests.RegistrationStoresEveryGroupImageAndMergeFoldsThemIntoTheCase|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce" --logger "trx;LogFileName=intk-063-a022fc4b2-integration.trx"
```

Author test commands and all five exact test start times are machine-readable
in frontmatter. Author build/restore commands are retained in the chronological
table. Report `67f2c76040cf0b1c` preserves their original execution history.

## Actual accepted caller coverage

Merged Core test-name census: AutomaticImageIntakeTests 22,
ImageIntakeCasePairingTests 10, ImageIntakeLifecycleTests 40 and
ImmediateExternalPublicationTests 3 = 75 actual xUnit cases.

Merged integration test-name census is 18 actual cases, including four
`GroupRegistrationAndInterruptedPairingPreserveEveryMember` cases
(automatic/staff-origin × intact/reversed sibling), not a stress/cohort count.

- Actual acceptance duplicate and registered-image replay use the same
  pairing owner, without another acceptance custody publication or rescan.
  Existing first-arrival tests retain supported exact/truncated recognition.
- `TimerRecoversLinkedImageAfterOlderNonmatchWithoutAnotherAcceptance`
  exercises the real staged reconciliation function, recovering linked but
  unmerged state despite an older nonmatch, then replaying once without a
  second merge/outbox. The existing full timer-result test also passes.
- `AutomaticWriteRechecksCurrentIdentityAndPrincipalBeforeAssociation`
  and actual association tests cover current CaseMatchIndex identity,
  known-principal contradiction, new ambiguity, current Case version,
  active lease and post-report refusal.
- The four grouped cases cover all durable members before merge, partial
  reasoned staff group completion, same-target changed origin, final merge
  refusal, prior sibling history, timer replay and retained staff reason/
  identity/version/operation under SystemWorker completion attribution.
  Exact request replay and changing only expected origin version conflict
  without mutation are asserted in that existing bounded theory.
- `MergeRechecksCurrentStaffDestinationAndPreservesItsReasonedOverride`
  and `ReceiptLinkEnforcesEligibilityOnceAnImageIntakeExists` cover current
  recorded staff target, actual LinkIntake and stale-target custody refusal.
  `RegistrationStoresEveryGroupImageAndMergeFoldsThemIntoTheCase` retains
  its original custody assertions after reasoned member links.
- `WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce`
  passes under the actual existing SQL Worker role with real pairing,
  candidate, receipt/image/mutation stores and two reconciliation calls.
  It proves single association/merged lifecycle/destination/outbox/history
  and the unchanged original draft versus accepted current index. This role
  fixture STARTS from already-registered state: it does not prove initial
  registration, live image/email receipt, mailbox/provider interaction,
  deployment or cloud activation. No new grant was needed or added.

No routed Razor/UI changed. No new capture, snapshot update, catalogue run or
manual visual result is claimed. No full corpus, stress/soak/capacity suite,
new test infrastructure, live mail/provider or Azure action ran for this proof.

## Review findings preserved

Independent review F-001 (major) is fixed: actual
`RequestHash(AutomaticIntakeLinkRequest)` conditionally appends a non-null
expected staff-origin version using invariant formatting, consistent with
the merge fingerprint. Null ordinary automatic identity remains unchanged.
The ineffectual record JSON attribute was removed. Both final author and
merged grouped-store assertions cover exact replay and version-only conflict
without any state/history mutation.

F-002 (note) is rejected with source-backed reason, not silently ignored:
the initial ImageIntakeOrigin/resolver expose no principal, registration does
not set one, and the existing later SetPrincipalAsync is the setter. A known
principal at that initial boundary was not established. No inferred origin
provenance or speculative initial-registration policy was introduced.
Registered records retain immutable VRM and the existing known-principal guard.

Independent review's completed non-gating bot comment contained no findings;
empty checks/threads were never called green CI. Exact local acceptance here
does not discharge EPIC-014's final converged solution/release/live gate.

## Retained artifacts and non-destructive copy manifest

All five TRXs were copied from their distinct author/merged source paths into
the previously absent ignored `pegasus_pack/current/proofs/intk-063/`.
Every destination was checked absent before Copy-Item; each source hash was
verified before copy and destination hash after copy. All five source and
destination hashes were rechecked against the manifest after it was written
with apply_patch. No existing evidence was overwritten or removed.

Manifest:
`pegasus_pack/current/proofs/intk-063/manifest.json`
SHA256 `FCC14EF2269BD14D1F17890D549C344B88DA685E8F75AF2A4B11A51724AE784F`.
It records original paths, retained paths, phases, sizes, exact UTC TRX
intervals, counters, failed test name and hashes. Total five TRX bytes:
759449. Archive remains ignored; no corpus/source or cloud copies.

| Retained path beneath that root | Actual counters | SHA256 |
| --- | --- | --- |
| author/intk-063-core-corrected.trx | 75 PASS | C464DEFFFB614EC5CD9B60D2CE38541BA24E23659A000FEC95995D05833D1834 |
| author/intk-063-integration-corrected.trx | 17 PASS / 1 FAIL | C3FA11D7CC51BF79FF967BF9D0C662F97332EB2E3642D68B2E86DCEB0A1F7C73 |
| author/intk-063-worker-seed-corrected.trx | 1 PASS | D7D37F72E5BC7FC742CBA75E286AC536C05B061F22415297C4A46115CF7FAFC0 |
| merged-a022fc4b2/intk-063-a022fc4b2-core.trx | 75 PASS | 506B03A683E69DE39A95F2A0457AB5E9BD459E1E8DACE12575E08EF525CCA774 |
| merged-a022fc4b2/intk-063-a022fc4b2-integration.trx | 18 PASS | F96B3F5E918D111F0B314BA79F3FA5D71FD0CAA5A54FDF402DAB3173E5FA3C23 |

TRX intervals in UTC (not identical to reported test durations):

- Author Core: 04:03:28.6642250Z–04:03:30.6238919Z.
- Author integration FAIL: 04:03:32.2343630Z–04:05:25.2657326Z.
- Corrected Worker: 04:11:14.7290190Z–04:11:52.3918048Z.
- Merged Core: 04:41:01.9537182Z–04:41:03.9947274Z.
- Merged integration: 04:41:05.7646617Z–04:43:00.8178101Z.

All intervals are 2026-09-08. No prior artifact was recovered from invented or
overwritten data; all five originals were readable and retained in place.

## Decision remains with root

This whole proof is ready for root readback and the fresh Verifying → Done
gate decision. Both exact author and verification worktrees, their branches,
the shared checkout's unrelated edits, all other claims and root's lease are
unchanged. No cleanup or release yet. If accepted, closeout may update current
traceability to the reachable squash merge and remove only explicitly
authorized clean owned worktrees after rehashing this retained archive.
