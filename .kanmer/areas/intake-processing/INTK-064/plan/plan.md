# Plan — INTK-064

## Objective and starting state

Automatically link a uniquely identified Triage and formal Case in either
arrival order, preserving permanent Triage reference/findings and reasoned
manual control. Current execution baseline:
aefe4c32d078ad79c0368666b5666032e6865248 (fresh origin/dev).
Evidence: research 1233ce70e848f312; files f05f000a8b26f9be.
Prior plan 597e4162d28c4a76 remains historical; its transaction/replacement
guards are retained below.

TICK-035 is Done/released. INTK-063 is Done/closed/released at
a022fc4b2db87d4d2eeb14437b41f6d8344d63e6. TICK-085 is integrated at
aefe4c32d078ad79c0368666b5666032e6865248 and remains Verifying for genuine
OCR acceptance. Root's 2026-09-08 handoff in both execution scratch records
releases only the DI Triage registration and direct AcceptIntake constructor
fixtures to INTK-064; it does not release TICK-085's claim or OCR scope.
Root approved execution of this bounded plan. Fresh census preserves all
historical claims, including INTK-060. The frozen supplemental run and original
roster are unchanged; use INTK-064-triage-link-recovery at .worktrees/intk-064.

## Governing docs

Meets current operator brief, EPIC-014, FRD-02 source identity/reversal and
FRD-03 Triage-to-formal-Case association. Authorized doc clarification removes
obsolete pre-Case terminology for Triage without giving it a normal Case/PO
or promoting its findings. No image eligibility or manual link policy changes.

## Required changes and existing owners

There is no ITriageLifecycle registration to reuse. Add one narrow
ITriageCasePairing/TriageCasePairing automatic command in the existing
Triage contracts/lifecycle files, following the current image-pairing caller
convention, with one ordinary DI registration. Its three real entry points
serve Triage creation, formal acceptance and scheduled recovery. No parallel
matcher, service hierarchy, service locator or Worker-side construction.

Extend ITriageStore/EfTriageStore with only the narrow candidate/replay/link
operations this command needs. Do not use the full ITriageQueries.GetAsync
UI projection: linking needs no findings, and Worker has no findings SELECT.
Existing Triage/history/origin/draft/principal/Case/index permissions suffice
on source census; actual restricted-role proof remains mandatory.

Reuse EvaluateIntakeCaseMatch.ExecuteDeclaredAsync with current accepted
typed CaseMatchSourceData and the existing principal policies. No evaluator
contract or grammar change. Give both EfCaseMatchIndex.FindByAnyKeyAsync and
FindByCaseIdAsync a context-bound form in that SAME query owner so the complete
eliminator and replacement lookup run within EfTriageStore's existing
serializable transaction.

## Ordered steps

1. Add the automatic command/store inputs and context-bound existing matcher.
   Candidate input is the known persisted Triage principal, retained origin/
   evaluation identity, accepted Triage VRM and typed draft identity. Present
   contradictory draft VRM refuses; never invent a principal or substitute
   a candidate's principal. Extend the two existing ITriageStore test doubles.
2. Implement the SystemWorker-only automatic transaction. Preserve manual
   TriageCaseLinkRequest's Staff authorization, reason and real Case lease.
   Recheck current Triage state/version/principal/origin, complete candidate
   uniqueness, target state/version/archive and live Staff lease. Compare the
   final current Case.PrincipalId directly with known Triage.PrincipalId,
   including Created-in-error replacement redirects. Prior deliberate manual
   unlink/relink prevents automatic override. Reuse deterministic operation
   identity, hash/replay, Triage history and Case workflow-event transaction.
   Cancelled refuses; Completed may link without reopening or finding changes.
3. Call pairing from CreateTriageFromIntake after store creation/replay,
   preserving its historical return contract; DurableIntake already calls
   this command and requires no edit. Call after AcceptIntake's actual
   acceptance, including duplicate replay, preserving INTK-063 image pairing.
   Register once and call from StagedArtifactReconciliationFunction.
   Select oldest eligible unlinked work before the result cap; unknown
   principals, manual overrides and dynamic current nonmatches must not starve
   newer matches. Nonmatch never becomes a permanent exclusion: later Case/
   correction remains retryable. Surface recoverable failures and continue
   unrelated work without swallowing them or completing a nonexistent link.
4. Update only the mapped canonical docs and existing focused fixtures.
   Replace the obsolete no-auto-link assertion in
   QdosTriageCaseAssociationIntegrationTests using its same genuine QDOS
   original/hash; retain reference/state/findings/no-extra-allocation checks.
   Supply the required dependency at the six mapped direct-construction
   fixture files; no optional production no-op added solely for tests.
5. Freeze for root's focused build/runtime verification and independent review.

## Expected files and do not modify

Exact paths/responsibilities are files f05f000a8b26f9be: seven production files,
three canonical docs, six behavior-test files, six acceptance-constructor fixture files,
the approved direct-store constructor fixture and existing Worker timer fixture. Compared with the old map, DurableIntake.cs and
EvaluateIntakeCaseMatch.cs are read-only context; AddTriageNoteTests and the
six direct-construction fixtures are explicit necessary internal consumers.

No Web/MCP manual authorization change, provider parser, new queue/runtime,
schema/migration, broad grant, corpus mutation, external provider call,
generated UI artifact, image behavior, or unrelated lifecycle changes.
Historical INTK-060 and linked claims remain untouched.

## Acceptance checks and commands

Root is the sole build/test owner. Use existing locked solution restore and
Release build once on the frozen source, then focused existing
TriageReplayTests, QdosTriageCaseAssociationIntegrationTests,
TriageFromIntakeIntegrationTests and QdosTriageReplayIntegrationTests; include
the new restricted-role method in AzureSqlRuntimeRoleMigrationTests and
ConcurrencyTokenPersistenceTests.FreshLocalDbCaseAcceptanceAndTriageInsertUpdateGenerateTokensAndRejectStaleWrites.
The latter is the one necessary direct EfTriageStore constructor fixture
approved by root: supply its existing real QDOS policy pattern, preserving
all concurrency assertions. Also run exactly
StagedArtifactReconciliationFunctionIntegrationTests.TimerCallsTheBoundedReconcilerAndLogsEveryResultField
to prove the required timer caller and bounded failure logging, preserving
all prior fields. Root approved this exact direct-constructor fixture
amendment after the final caller census.
Retain AddTriageNoteTests and ImmediateExternalPublicationTests as the small
affected Core regressions. Constructor-only unrelated SQL classes do not
justify rerunning their whole cohorts.

Prove both arrival orders, duplicate replay, timer-only failure recovery,
current corrected identity/new competitor, cross-principal Created-in-error
replacement refusal, contradictory/unknown principal, manual unlink/relink,
live lease/stale version, Cancelled refusal and Completed linkage. Assert one
link/history, unchanged references/findings and no extra Case/PO. Preserve all
manual Staff/lease rejection assertions and acceptance publication behavior.
Run existing documentation-links check for the three changed docs; no capture,
full corpus, soak or new test host. Every actual attempt/exit remains recorded.

## Failure and deviation rules

Pre-reading only a target version is insufficient: candidate and redirect
currentness must be checked in the link transaction. Stop on an unfit existing
query boundary or newly evidenced permission/schema requirement; root must
review any such expansion. Do not add TriageFindings access to support a broad
UI read. No author tests/builds; root owns focused verification.

## Stop condition

Execution authorized by root after the recorded narrow predecessor handoffs.
Stop source-frozen for root's named verification; no commit/push/PR before
root supplies actual PASS evidence. Then stop at independent Review.
No self-review, merge, cleanup, foreign claim change or next-ticket execution.
