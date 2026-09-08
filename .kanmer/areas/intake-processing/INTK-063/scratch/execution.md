2026-09-08 execution acquired approved isolated branch INTK-063-image-link-recovery at .worktrees/intk-063, exact packet/base cdaa02584c38ecc27d3bd24784f59da189138bc1; plan 72f2ded4f46f9866/files 7ee5d63f63be1ef1. Fresh whole packet ready and leave-preparing gates passed; include-archived board/worktree census preserved all foreign claims. Resolved root/common Git/branch clean and exact; .worktrees ignored. No supplied reference files exist on this ticket. Root is sole heavy verifier; no author builds/tests/cloud. Existing candidate query now shares a context-bound current CaseMatchIndex read under automatic transaction. Additional actual group failure window raised to root: current first-member link triggers terminal merge before later sibling links; propose same pairing owner completes existing group members before one merge, reusing IIntakeReceiptQueries and ListImagesAsync, no new DI registration/schema/service.

## Code-ready checkpoint (not runtime proof)

2026-09-08 03:33 UTC approximately. Author work remains on exact cdaa02584c38ecc27d3bd24784f59da189138bc1, INTK-063-image-link-recovery in .worktrees/intk-063. Seventeen scoped files changed; no source commit/push/PR yet. Standalone git diff --check exited 0 (line-ending conversion warnings only). No author restore/build/test/cloud action was run.

Current code uses persisted ExpectedMemberCount (HasSiblingMembers is > 1), current CaseMatchIndex/principal, serializable link/merge rechecks, every durable group image regardless of mutable queue decision, current staff decision origin, oldest eligible-before-cap recovery, and existing acceptance/registered-replay/timer callers. The existing restricted Worker fixture is authored using the actual stores and EXECUTE AS USER; no permission gap or runtime PASS is claimed. Root approved existing custody and acceptance fixture additions; plan 7e7246481a9a46bd/files 9bc24e63a2b10a38 read back.

A final scope question is with root: a recorded staff override on only the group origin cannot authorize strict automatic links for remaining mismatching members; current code safely leaves it Awaiting/excluded-before-cap until those members are explicitly linked. Singles and groups whose current members are all staff-linked honor the override. No caller-supplied bypass or invented provenance was added. Do not start final runtime verification until root disposes this boundary.

Proposed focused Core filter: FullyQualifiedName~ImageIntakeCasePairingTests|FullyQualifiedName~AutomaticImageIntakeTests|FullyQualifiedName~ImageIntakeLifecycleTests|FullyQualifiedName~ImmediateExternalPublicationTests

Proposed focused Integration filter: FullyQualifiedName~ImageIntakePersistenceTests|FullyQualifiedName~StagedArtifactReconciliationFunctionIntegrationTests|FullyQualifiedName~ImageCaseCustodyIntegrationTests.RegistrationStoresEveryGroupImageAndMergeFoldsThemIntoTheCase|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce

No full GroupedImageIntakeConcurrencyTests stress class, full corpus, UI capture, live email or provider calls are requested. No routed Razor page changed.

## Final revised source freeze

2026-09-08 03:46 UTC approximately. Root approved the partial staff-group remedy; the earlier checkpoint's scope question is resolved, not deferred. Plan 10543d55d4090f66 and files a2712472fca53cc9 were whole-read and approved by root, then fresh resumed execution packet returned ready:true. Recorded worktree/root/branch/common Git all still match; HEAD/base remains cdaa02584c38ecc27d3bd24784f59da189138bc1. Nineteen scoped tracked files are dirty; no commit/push/PR. No further source edits until root returns verification results. Standalone git diff --check PASS exit 0, with line-ending conversion warnings only. No author build/test/cloud/provider action.

Partial manual groups now derive authority inside AutoLinkAsync from their CURRENT active reasoned staff origin, exact group/Case and observed association version. Untouched siblings complete under SystemWorker attribution; existing history JSON retains StaffGroupDecision (group, origin receipt, Case, origin version, staff actor/roles/reason/operation). Any prior sibling association history is refused. Final merge also rejects stale origin versions even when the Case ID is unchanged. The Core request carries a concurrency expectation, not a bypass flag. Ordinary automatic requests omit the null JSON property, preserving their existing durable request identity.

LinkIntake now calls PairRegisteredReceiptAsync and surfaces its returned failure summary through existing Activity telemetry. Its prior empty catch is removed. Direct-consumer census found only LinkIntake and pairing's own call for the old public SyncMergeAfterLinkAsync; the helper is now private, and both explicit interface fakes were adapted. Existing timer constructors both bind the new owner. No compatibility wrapper/default, DI change, migration/grant, new service or queue.

Focused Core filter remains:
FullyQualifiedName~ImageIntakeCasePairingTests|FullyQualifiedName~AutomaticImageIntakeTests|FullyQualifiedName~ImageIntakeLifecycleTests|FullyQualifiedName~ImmediateExternalPublicationTests

Focused Integration filter remains:
FullyQualifiedName~ImageIntakePersistenceTests|FullyQualifiedName~StagedArtifactReconciliationFunctionIntegrationTests|FullyQualifiedName~ImageCaseCustodyIntegrationTests.RegistrationStoresEveryGroupImageAndMergeFoldsThemIntoTheCase|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce

GroupRegistrationAndInterruptedPairingPreserveEveryMember is four bounded cases (automatic/staff-origin × intact/reversed sibling), not the old stress fixture. It now includes partial manual completion, origin unlink/relink to the SAME Case between read/write, stale-origin final merge refusal, prior sibling history refusal and actual timer replay; staff provenance assertions use retained JSON history. Other new persistence checks cover eligible-before-cap lost merge recovery, current VRM/principal/ambiguity/stale version/lease/post-report, and current staff destination override. Existing ReceiptLinkEnforcesEligibilityOnceAnImageIntakeExists proves the actual changed LinkIntake caller. Restricted Worker test uses real stores/SQL role, owner-seeded registered state (no registration execution or cloud claim). Root owns all runtime evidence. No UI snapshot requested: no Razor route or rendering changed.

## Transitions

- 2026-09-08T03:46:38.450Z lease-phase implementing → running-command (lease 96040972-2231-4565-8a48-d176ea4c6aec rev 10; expires 2026-09-08T04:16:38.438Z)

## Root verification attempt 1 and bounded compilation correction

Root sole heavy session 65450: locked restore all seven projects PASS (maximum 1.60 seconds); solution Release build FAILED exit 1 after 69.39 seconds, zero warnings, one CS0103 at EfImageIntakeStore.cs:1274, missing CultureInfo namespace. No tests/TRX executed. Root reported host 13.9 GiB total / 2.8 GiB free during the run and confirmed no other heavy session active when it stopped. This failure remains part of the record.

After root explicitly released the source, added only using System.Globalization to the existing mapped store file. No author build/test was run.

Read-only initial-principal hypothesis inspection: ImageIntakeOrigin has no PrincipalId (receipt/source/hash/evaluation only); its actual EF resolver exposes no principal. RegisterAsync's new entity initializer never assigns PrincipalId; SetPrincipalAsync is the sole production setter, after registration. Consequently an already-known principal at the initial origin boundary is not currently representable; no new inferred origin/provenance field was invented. Existing initial tests explicitly allow the accepted truncated-read completion. Already registered records instead take the early replay path and retain their known principal and immutable VRM. Root has this distinction for disposition before the combined rerun.

- 2026-09-08T03:52:15.274Z lease-phase running-command → implementing (lease 96040972-2231-4565-8a48-d176ea4c6aec rev 11; expires 2026-09-08T04:22:15.266Z)

## Combined frozen correction — actual automatic-link fingerprint

Root's final source review found that RequestHash(AutomaticIntakeLinkRequest) serializes an explicit anonymous projection, not the record. Therefore the earlier record JsonIgnore attribute did not bind the staff-origin expectation; that explanation was incorrect. Removed the ineffectual Core attribute. The existing fingerprint now conditionally appends the non-null staff-origin association version, using invariant formatting consistent with the merge fingerprint; null ordinary automatic requests preserve the exact prior serialized identity.

Extended the existing grouped-store test: exact request/key replay succeeds, changing ONLY ExpectedStaffOriginAssociationVersion conflicts with IntakeOperationConflictException, and receipt version, association version, current destination, Case workflow version and single mutation-history row remain unchanged. Both the staff-origin and ordinary null-origin branch exercise this in the existing bounded theory. No new harness or extra test filter.

The missing System.Globalization import correction remains. Root's initial-principal hypothesis is explicitly disposed as not established at this boundary; no origin provenance was invented, and the registered PrincipalId guard remains. Build attempt 1 (69.39 seconds, CS0103, no tests) remains recorded above.

Source is frozen again for one root rerun. Current plan/files are unchanged (10543d55d4090f66/a2712472fca53cc9), as this is their existing replay contract. Readback confirmed the exact anonymous projection now carries the conditional component. git diff --check PASS exit 0. No author build/test. Fresh resumed packet showed current lease revision 10; successful CAS renewal is revision 11, implementing, expiry 04:22:15.266Z. Same worktree/branch/base.

- 2026-09-08T04:00:54.723Z lease-phase implementing → running-command (lease 96040972-2231-4565-8a48-d176ea4c6aec rev 12; expires 2026-09-08T04:30:54.717Z)

## Root verification attempt 2 and minimal fixture correction — 2026-09-08

Root sole-verifier session 89133 completed exit 1 before author source was
released. Full Release solution build PASS, 124.08s, 0 warnings; Core focused
cohort 75 PASS / 0 failures / 0 skipped, reported test duration 128ms;
integration focused cohort 17 PASS / 1 FAIL / 0 skipped, reported duration
1m50s. Initial attempt 65450 compile failure and its correction remain above.

Author independently read the retained TRX counters, precise timestamps and
hashes (no tests or builds executed by author):

- `tests/Pegasus.Core.Tests/TestResults/intk-063-core-corrected.trx`:
  75 executed, 75 passed; start 2026-09-08T04:03:28.6642250Z, finish
  2026-09-08T04:03:30.6238919Z. SHA256
  `C464DEFFFB614EC5CD9B60D2CE38541BA24E23659A000FEC95995D05833D1834`.
- `tests/Pegasus.IntegrationTests/TestResults/intk-063-integration-corrected.trx`:
  18 executed, 17 passed, 1 failed; start 2026-09-08T04:03:32.2343630Z,
  finish 2026-09-08T04:05:25.2657326Z. SHA256
  `C3FA11D7CC51BF79FF967BF9D0C662F97332EB2E3642D68B2E86DCEB0A1F7C73`.

Only failure:
`AzureSqlRuntimeRoleMigrationTests.WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce`.
Its owner-side seed duplicated the migration-seeded QDOS Organization name and
Principal code, causing uniqueness and cascading FK failures before the
restricted Worker caller ran. No role permission success was established by
this failed case.

After the root-confirmed process completion and exact authorization, corrected
only that method in the existing mapped test file: reuse
`SeededPrincipals.QdosAsync(context)` and its Id/SequenceLineageId; remove the
three duplicate Organization/PrincipalSequenceLineage/Principal inserts.
No fabricated customer, production change, new grant, or weakened assertion.
Existing migrated estate helper is the source of IDs. `git diff --check`
PASS exit 0 after correction (line-ending notices only).

Source frozen again: recorded `.worktrees/intk-063`, branch
`INTK-063-image-link-recovery`, HEAD/base
`cdaa02584c38ecc27d3bd24784f59da189138bc1`; 19 mapped tracked paths only.
Root will run incremental integration build and ONLY
`FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce`.
No rerun of 75 passing Core or 17 passing integration cases requested. No PR,
push, review, merge, cloud operation or author heavy command performed.
Lease renewed by fresh CAS to revision 13, running-command, at
2026-09-08T04:10:20.319Z; expires 04:40:20.319Z.
