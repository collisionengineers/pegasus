# Post-implementation report — INTK-064

## Summary and current stop

Author source-complete with root's focused pre-merge runtime PASS; not yet
integrated or deployed.
The isolated branch is INTK-064-triage-link-recovery at .worktrees/intk-064,
still based on aefe4c32d078ad79c0368666b5666032e6865248. No source commit or
PR yet. Root remains the sole heavy verification owner.

Plan 87b4d11fd251180e and file map f05f000a8b26f9be govern this diff.
Root approved the two necessary final direct-constructor fixture amendments
before edits: ConcurrencyTokenPersistenceTests and the existing Worker timer
fixture. No other file-map expansion, source handoff or historical claim
transfer occurred.

## Changes and production callers

- TriageContracts/TriageLifecycle add one required automatic pairing owner.
  CreateTriageFromIntake calls it after both first creation and exact replay,
  retaining creation's original historical return. AcceptIntake calls it
  after acceptance/replay without republishing already-committed custody.
  Existing image pairing is unchanged.
- EfTriageStore uses narrow Triage/origin/evaluation/draft/principal/index/
  workflow reads, not the full detail/findings projection. Known principal,
  original source identity/hash/evaluation and accepted VRM must agree with
  retained typed identity. A newer evaluation, unknown/inactive principal or
  contradictory typed registration refuses. Dynamic current matches, not
  merely oldest unlinked rows, fill the bounded result set.
- Existing EvaluateIntakeCaseMatch owns key normalization, eliminators,
  uniqueness and Created-in-error redirects. Both EfCaseMatchIndex methods
  have a context-bound form in the same query owner. Final link re-evaluates
  the whole match and replacement within its serializable transaction.
  The final Case's actual PrincipalId must equal the Triage's known one.
- The automatic store entry requires ExecuteSystemWork. It rechecks Triage
  and Case versions, cancellation, target archive/terminal state and live
  Staff lease. Existing manual commands and their staff authority/real lease
  are untouched. Existing triage_case_linked events are reused with
  SystemWorker attribution and match policy/version, avoiding a new UI event
  vocabulary. Prior manual link/unlink history prevents recovery overriding
  deliberate intent. One operation/hash replay records no duplicate link.
- StagedArtifactReconciliationFunction invokes that registered owner on its
  existing timer and records Candidates/Linked/Failures/FirstFailure. There
  is no new timer, queue, schema, migration, grant, findings permission, parser
  or production optional no-op. Recoverable failures remain unlinked and
  available for replay, expose bounded failure type/count, and do not skip
  other candidate writes.
- All six acceptance-constructor fixture files supply the required dependency;
  the direct store and timer fixtures preserve their existing assertions.
  The two existing ITriageStore doubles implement its narrow new members.
- FRD-03 records Triage as a Case with a distinct reference/workflow, automatic
  linkage and manual reversal. FRD-02 links that owner; current-architecture
  names actual creation, acceptance and timer callers without deployment
  claims. Generated UI, manual Web/MCP controls, corpus and other claims are
  untouched.

## Focused acceptance authored

The same supplied QDOS original remains hash-bound:
qdosmapping/(EREF10) RTA on 14_08_2026  Mr Paul Larcombe (Our Ref AMA_47857_1,
Vehicle PG18 BTY).eml — SHA256
3063ff9ecb31878f582fb439047d999a41a7c6fe5b978cfbee5c7e7f277553b4.

Its previous no-auto-link expectation is replaced with both arrival orders
through real intake/formal acceptance and the existing Triage creation
boundary. Reference/state/findings/no-extra-Case assertions remain. The
pre-existing Triage arrangement remains explicitly an arrangement, not a
claim that the formal instruction itself was classified as Triage.

Existing structural persisted-state fixtures are authored to exercise changed current index,
new competitor, cross-principal replacement reached through FindByCaseId,
stale Case version, live lease, cancellation, manual unlink and replay.
A later matching Triage is selected after older unknown/contradictory rows;
those rows are not permanently excluded. Existing completed-Triage replay
keeps all finding/response/history assertions and adds linkage without reopen.
The restricted Worker test uses the existing EXECUTE AS/ConnectedContextFactory
harness and actual TriageCasePairing command. Core probes retain original
creation replay, report failed writes and retry while continuing another
candidate. No new inbound domain message or source fixture is fabricated.

## Verification attempts

2026-09-08 UTC, author lightweight checks only:
git diff --check PASS (exit 0); exact branch/base/worktree readback and
direct-constructor/interface-implementation census completed. Static review
corrected an undefined clock in the direct Provider API fixture before freeze.

No restore, build, test, provider call, capture or CI has been run by the
author. The following actual root-owned attempts are retained; later PASS
does not erase the first compile failure. This is not merged proof.

### Root attempt 1 — retained FAIL

2026-09-08 UTC, root session 72700: locked restore of all seven projects
PASS. Solution Release build FAIL, exit 1 after 15.92 seconds, zero warnings
and one CS8602 at src/Pegasus.Core/Triage/TriageLifecycle.cs:106. No tests ran.
ExecuteDeclaredAsync deliberately returns a nullable result for an absent
policy. The new caller now uses a non-null UniqueMatch property pattern,
returning null for a missing/non-unique decision rather than dereferencing or
suppressing nullability. Only that source expression changed; no assertion,
matcher contract, policy or file scope was altered. Author diff --check PASS
exit 0 after the correction. Fresh source freeze; root owns the retry.

### Root attempt 2 — focused PASS

2026-09-08 UTC, root session 39568: corrected-source Release build with
--no-restore PASS, exit 0, 91.22 seconds, zero warnings/errors.
Root session 62911 ran the exact two filters below using Release --no-build:
Core 32/32 PASS (90 ms), integration 14/14 PASS (59 seconds), zero skips.
Author independently read both complete TRX counters, times and hashes.

- artifacts/verification/intk-064-core.trx:
  SHA256 6AC9B8A24C302230F659FEF561656BA13FB393880562E96E5F877784CBBED7FC;
  start 2026-09-08T05:55:54.3281705Z;
  finish 2026-09-08T05:55:55.8948561Z.
- artifacts/verification/intk-064-integration.trx:
  SHA256 F47E0DA8540ECB09C8636A1B925A95DAD937B2BC51497BB63751AADC8C767AA1;
  start 2026-09-08T05:55:57.3119103Z;
  finish 2026-09-08T05:56:58.2264624Z.

TRX timestamps are stored with +01:00; the times above are their exact UTC
conversion. All outcomes executed, none skipped or notRunnable. Integration
includes both genuine arrival orders, actual restricted Worker, pending
selection, completed replay, manual/identity/version/lease guards, concurrency
and the actual timer fixture. No external mailbox/provider call is claimed.
Root DocumentationLinks check: 127 PASS, exit 0 at 05:57 UTC.
No capture/generated UI delta or CI rerun. Root authorizes one [skip ci] PR
to dev under the remediation run's one-final-CI policy; exact merged proof
remains outstanding after independent review/integration.

## Root verification handoff

Use this exact author worktree, Windows/PowerShell 7. Keep the existing genuine
reference pack environment available; do not exclude Category=Corpus from
the explicitly selected genuine QDOS test.

1. Existing locked solution restore and Release build, once on frozen source.
2. Core filter:
   FullyQualifiedName~TriageReplayTests|FullyQualifiedName~AddTriageNoteTests|FullyQualifiedName~ImmediateExternalPublicationTests
3. Integration filter:
   FullyQualifiedName~QdosTriageIntegrationTests.AutomaticPairingRechecksCurrentIdentityLeaseVersionAndManualIntent|FullyQualifiedName~QdosTriageIntegrationTests.GenuineFormalInstructionLinksTriageInEitherArrivalOrderWithoutChangingItsWorkflow|FullyQualifiedName~QdosTriageIntegrationTests.CaseAssociationUsesCanonicalWorkflowVersionAndActiveCaseLease|FullyQualifiedName~QdosTriageIntegrationTests.NamedMutationRetriesReturnHistoricalResultsAndRetainConflictAndStateGates|FullyQualifiedName~TriageFromIntakeIntegrationTests|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WorkerRuntimeAutomaticallyLinksTriageAndReplaysWithoutFindingsPermission|FullyQualifiedName~ConcurrencyTokenPersistenceTests.FreshLocalDbCaseAcceptanceAndTriageInsertUpdateGenerateTokensAndRejectStaleWrites|FullyQualifiedName~StagedArtifactReconciliationFunctionIntegrationTests.TimerCallsTheBoundedReconcilerAndLogsEveryResultField
4. Existing scripts/Test-DocumentationLinks.ps1. No UI capture or broad corpus,
   stress, independent duplicate build or unrelated SQL cohort.

Use Release --no-build for each focused test project, unique TRXs under
artifacts/verification/intk-064-{core,integration}.trx, retain all outcomes.
Do not infer a test class from a filename: the QDOS association and replay
files are partial members of QdosTriageIntegrationTests.

## Risks, boundaries and next step

The required focused SQL/Worker checks passed under root without a grant.
Any later newly evidenced permission/schema need still requires root review. Read-only context and original provider grammar are unchanged.
This adds no live deployment or external provider acceptance claim.
Root supplied actual PASS and authorized publication after the independent
static preflight. Publish once for independent exact-head review; do not
self-review, merge, clean up or start another ticket.
