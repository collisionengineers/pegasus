# Post-implementation report — INTK-063

## Outcome and exact scope

Implemented the existing image-to-Case recovery path at author commit
`e7db237e47322d2378ccf44749d97024db377aeb`, based on accepted dev
`cdaa02584c38ecc27d3bd24784f59da189138bc1`. Branch
`INTK-063-image-link-recovery`, recorded worktree `.worktrees/intk-063`.
Nineteen mapped files changed, +1222/-290; clean author tree after commit.
PR: [#696](https://github.com/collisionengineers/pegasus/pull/696), OPEN to dev;
GitHub head read back as the exact author SHA above.

Plan `10543d55d4090f66` and files `a2712472fca53cc9` were approved by root
before the corresponding changes. FRD-02 and EPIC-014 are the governing
behavior/context. This remains supplemental INTK-063; original 218 tickets and
foreign historical claims were not transferred or rewritten. The publication
packet resolves current dev to `498144b0bb55b68fd53b9a31ffc89ef90622c73a`;
that is not misreported as this implementation's starting or tested base.

No author build/test, self-review, merge, deployment, mailbox/provider call or
cloud write. Root alone executed the focused verification below. Root explicitly
approved the commit's `[skip ci]` under the single converged release CI policy;
this is not a CI PASS or a waiver of independent review/exact-merge verification.

## Existing production callers and behavior

- `ImageIntakeAutomation.TryRegisterAndAssociateAsync/TryRegisterGroupAsync`
  now route first-arrival and already-registered replay into the same existing
  `IImageIntakeCasePairing`. Replay does not rescan/re-register or change the
  registered VRM.
- `AcceptIntake` wakes that owner on duplicate acceptance as well as first
  acceptance, without republishing acceptance custody.
- `LinkIntake` routes advisory completion through
  `PairRegisteredReceiptAsync`; the swallowed recovery catch and old public
  sync surface are removed. The merge helper remains private to the existing
  owner; known interface consumers are adapted, not compatibility-wrapped.
- `StagedArtifactReconciliationFunction` invokes
  `ReconcileAsync(50)` on that same already-registered owner, reporting
  candidate/merged/failure counts and first failure. No new timer, DI
  registration, queue, schema, service or test host.
- Current `CaseMatchIndex` VRM and Case principal govern automatic registered
  pairing. The shared context-bound candidate query and Core selector run
  again in the automatic-write transaction; stale pre-query uniqueness is not
  authority. Eligibility/reversal/lease exclusions happen before the returned
  oldest-first cap. No persistent no-match exclusion prevents later recovery.
- Every durable group image member must be currently associated before one
  final merge, even if its mutable queue decision changed. A failed member
  leaves Awaiting instruction state for replay/timer. Single images and
  one-member groups retain exact-match precedence; persisted
  `ExpectedMemberCount > 1` retains the existing multiple-member rule.
- Current reasoned staff-group intent may deliberately differ from automatic
  VRM/principal matching. Serializable AutoLink derives that authority from the
  persisted active group origin/target/reason; its observed association version
  is only a concurrency expectation. Only siblings with no association history
  complete automatically. SystemWorker attribution and originating staff
  identity/reason/version/operation remain in existing JSON history.
- Final merge rechecks current origin version, every current member target,
  pre-report/nonarchived eligibility and applicable lease inside its own
  transaction. A staff unlink/relink cannot be hidden by unchanged image
  lifecycle version or by relinking to the same target. Existing deterministic
  custody outbox and transition identities remain the owners.

## Changed files

All paths are repository-relative. Optional mapped CaseMatchEntities and the
read-only grouped stress fixture were not changed.

| Path | Purpose |
| --- | --- |
| src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs | Existing query/result/context contracts for pending recovery, current principal and persisted group count. |
| src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs | One pairing/recovery owner, current target selection, all-member completion, observable outcomes. |
| src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs | First arrival and registered replay use that owner; remove redundant direct association dependencies/path. |
| src/Pegasus.Core/Intake/IntakeContracts.cs | Staff-origin association-version concurrency expectation on existing automatic request. |
| src/Pegasus.Core/Intake/DurableIntake.cs | Actual reasoned staff-link completion uses observable recovery owner. |
| src/Pegasus.Core/Intake/AcceptIntake.cs | Duplicate acceptance wakes pairing without duplicate acceptance custody. |
| src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs | Current index candidate adapter, eligible pending selection, current members and transactional merge/origin guards. |
| src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs | Serializable current candidate/staff-origin checks, existing JSON provenance and actual request fingerprint. |
| src/Pegasus.Worker/IntakeFunctions.cs | Existing scheduled reconciliation caller/result logging. |
| docs/frd/frd-02-intake-and-source-identity.md | Recovery, current identity, recorded manual authority, all-member and lease behavior. |
| docs/current-architecture.md | Actual owner/caller wiring, without deployment claim. |
| tests/Pegasus.Core.Tests/Cases/ImmediateExternalPublicationTests.cs | Duplicate acceptance retry with single acceptance custody publication. |
| tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs | Registered replay and existing single/group first-arrival consumer assertions. |
| tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs | Current principal/group exact selection, recovery results, adapted receipt/state owner. |
| tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeLifecycleTests.cs | Existing store-interface consumer adaptation; lifecycle assertions retained. |
| tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs | Actual automatic/staff-origin grouped recovery, replay, current identity/lease/eligibility/reversal and timer guards. |
| tests/Pegasus.IntegrationTests/StagedArtifactReconciliationFunctionIntegrationTests.cs | Existing actual scheduled function plus complete old/new result assertions. |
| tests/Pegasus.IntegrationTests/ImageCaseCustodyIntegrationTests.cs | Existing custody fixture first establishes required persisted member associations; original custody assertions retained. |
| tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs | Actual restricted Worker recovery/association/merge, twice, using migrated QDOS identity and existing SQL fixture. |

## Verification attempts and dispositions

All commands ran on Windows/PowerShell in the recorded author worktree, by
root as sole heavy verifier. A failed native command stopped its script.
Author independently read the three retained TRXs' actual names, counters,
timestamps and SHA256. No failure was overwritten or called PASS.

| Attempt | Result | Disposition |
| --- | --- | --- |
| Root 65450 | Locked restore all seven projects PASS (max 1.60s). Solution Release build FAIL exit 1, 69.39s, 0 warnings: CS0103 CultureInfo absent at EfImageIntakeStore.cs:1274. No tests/TRX. | Added only missing System.Globalization import in mapped store after root released source. |
| Root 89133 | Corrected solution build PASS 124.08s, 0 warnings. Core 75/75 PASS (reported test duration 128ms). Integration 17/18 PASS, 1 FAIL, 0 skipped (reported 1m50s); script exit 1. | Only new Worker fixture failed while duplicating migration-seeded QDOS identity, before restricted runtime caller. Reused existing SeededPrincipals.QdosAsync Id/SequenceLineageId and removed three duplicate identity inserts; all role/custody/history assertions retained. |
| Root 83241 | Incremental Integration project build PASS 21.64s, 0 warnings; only corrected Worker method 1/1 PASS, 0 skipped, 35.3962993s; script exit 0. | Actual restricted Worker caller now proved. The prior 75 Core/17 integration passes were not needlessly repeated. |
| Author file check/commit | git diff --check and staged diff check PASS exit 0; exact 19-file census; clean after commit. | No runtime or CI claim from Git checks. |

### Exact commands

The initial restore and build, then corrected solution build used:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
```

The two focused cohorts in attempt 89133:

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImageIntakeCasePairingTests|FullyQualifiedName~AutomaticImageIntakeTests|FullyQualifiedName~ImageIntakeLifecycleTests|FullyQualifiedName~ImmediateExternalPublicationTests" --logger "trx;LogFileName=intk-063-core-corrected.trx"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImageIntakePersistenceTests|FullyQualifiedName~StagedArtifactReconciliationFunctionIntegrationTests|FullyQualifiedName~ImageCaseCustodyIntegrationTests.RegistrationStoresEveryGroupImageAndMergeFoldsThemIntoTheCase|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce" --logger "trx;LogFileName=intk-063-integration-corrected.trx"
```

Attempt 83241 repeated only the corrected fixture:

```powershell
dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce" --logger "trx;LogFileName=intk-063-worker-seed-corrected.trx"
```

### Retained artifact identities

- `tests/Pegasus.Core.Tests/TestResults/intk-063-core-corrected.trx`:
  75 executed/PASS; UTC start `2026-09-08T04:03:28.6642250Z`, finish
  `2026-09-08T04:03:30.6238919Z`; SHA256
  `C464DEFFFB614EC5CD9B60D2CE38541BA24E23659A000FEC95995D05833D1834`.
- `tests/Pegasus.IntegrationTests/TestResults/intk-063-integration-corrected.trx`:
  18 executed, 17 PASS/1 FAIL; UTC start `2026-09-08T04:03:32.2343630Z`,
  finish `2026-09-08T04:05:25.2657326Z`; SHA256
  `C3FA11D7CC51BF79FF967BF9D0C662F97332EB2E3642D68B2E86DCEB0A1F7C73`.
- `tests/Pegasus.IntegrationTests/TestResults/intk-063-worker-seed-corrected.trx`:
  1 executed/PASS; UTC start `2026-09-08T04:11:14.7290190Z`, finish
  `2026-09-08T04:11:52.3918048Z`; SHA256
  `D7D37F72E5BC7FC742CBA75E286AC536C05B061F22415297C4A46115CF7FAFC0`.

These are distinct files. Preserve all three before eventual worktree cleanup.
TRX run start/finish is not the same measurement as reported test duration.
Exact restore/build instants were not recorded; their observed duration and
exit evidence are attributed to root sessions, not invented timestamps.

## Specific failure-mode evidence and source-review dispositions

`GroupRegistrationAndInterruptedPairingPreserveEveryMember` is four bounded
automatic/staff-origin × intact/reversed cases, not a stress run. It proves
partial reasoned group completion, stale origin after same-target relink,
final-merge origin refusal, deliberate sibling-history refusal, retained staff
provenance and timer replay. Existing current-identity tests cover corrected
VRM, principal contradiction, newly ambiguous candidate, stale Case version,
active lease and post-report refusals. Timer recovery puts an older nonmatch
before actionable linked-but-unmerged state; two real function invocations
produce one merge/outbox. Existing actual LinkIntake, registered automation and
acceptance callers are in the focused cohorts.

`WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce` owner-seeds
already-registered source/Case state, then executes the real pairing owner,
candidate adapter, receipt/image/mutation stores under the existing Worker SQL
role twice. It asserts first/replay outcomes, one active association, one
merged lifecycle/destination, one outbox and history, and unchanged original
draft versus current CaseMatchIndex identity. This is actual SQL-role recovery
evidence, not initial registration execution, a new customer, a live image/mail
receipt or deployed service proof. No missing grant was found or added.

Root's initial-principal hypothesis was investigated and explicitly disposed
as not established at that boundary: ImageIntakeOrigin and its actual resolver
carry receipt/source/hash/evaluation, not PrincipalId; RegisterAsync does not
assign it, and SetPrincipalAsync is the existing later setter. Do not invent
origin provenance or claim an observed wrong-principal initial registration.
Initial accepted truncated-read completion remains unchanged; already-registered
replay uses immutable VRM and its existing known-principal guard.

Root also found the actual RequestHash anonymous projection omitted the new
expected origin version: the earlier record JsonIgnore explanation was wrong.
Removed that ineffectual attribute. The existing hash now conditionally appends
non-null staff-origin version with invariant formatting; ordinary null automatic
request identity remains unchanged. The existing grouped-store theory asserts
exact key/request replay and conflict when ONLY expected origin version changes,
without changing receipt/association/Case versions, target or history. This is
the planned replay contract, not new scope. Earlier scratch failure/history
remains preserved.

## Boundaries and next owner

No Razor route/rendering change: no snapshot/capture, manual visual pass or UI
claim. No full corpus, stress/soak or live provider test. No new dependency,
DI registration, schema/grants/bootstrap change or runtime unit. The result
uses the existing source/history/custody mechanisms. Initial recognition policy
is unchanged; recovery respects current persisted automatic or staff origin.

Independent reviewer must inspect this exact commit/PR and current packet,
including transactional freshness, originating-version replay, actual timer
and restricted-role evidence. Root then owns exact merged dev verification
using these bounded Core/Integration cohorts (fresh unique TRX names) plus
normal locked restore/build; preserve both author failures. Current dev has
advanced since the accepted starting base, so reconcile actual merge delta and
caller evidence rather than relabelling author results as merged acceptance.
No integration, Done, live or deployment claim yet. Author stops after Review
handoff and retains the recorded branch/worktree/lease.
