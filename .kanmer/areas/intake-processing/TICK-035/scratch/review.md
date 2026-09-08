---
kind: review-attestation
pr: "692"
head_sha: "cca5521a6315420129320061759209273bb64c67"
verdict: pass
reviewer: "pack_reconcile"
independent: true
plan_hash: "7a28b8ab58ed1ca2"
ticket_updated: "2026-09-08T02:06:42.835Z"
board_sha: "8b09791e5d8e0406f01b38b0fbc8ce387a013b75"
expected_reviewers: ["pack_reconcile"]
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Prior preflight F-P01: retained receipt candidates lost Locator and RawValue."
    disposition: fixed
  - id: F-002
    severity: major
    summary: "Prior preflight F-P02: equal table numbers from separate physical documents collided."
    disposition: fixed
  - id: F-003
    severity: major
    summary: "Non-QDOS automatic email matching extracted and consumed keys without an agreeing selected instruction profile."
    disposition: fixed
---

# TICK-035 independent delta review

PASS at exact PR692 head cca5521a6315420129320061759209273bb64c67.
Root assigned pack_reconcile as sole expected reviewer. The exact-head
[public review5136590986](https://github.com/collisionengineers/pegasus/pull/692#pullrequestreview-5136590986)
settled that identity at 2026-09-08T02:16:19Z before this attestation.
Author intake_audit and reviewer are distinct agent roles despite sharing
the repository GitHub account. Reviewer ran no build or test, changed no
source or author lease, and did not merge.

This whole-file record supersedes needs-changes29da3f33c888cb13 as the current
decision. The original public review5136452778 and source
preflight7074751553d263d8 remain historical evidence, not retrospectively PASS.
The ticket is Review, review_round1. This is the bounded delta review of
F-003, changed lines, direct callers/contracts and relevant assertions;
unchanged source is not reopened as an unrestricted second audit.

## Current packet and scope

Read the whole current plan7a28b8ab58ed1ca2,
filesa37303dd2d9f8f1f, report70312df54319f367,
checklist54a984eff71f67c8, executionbc4b6d6de3c978f5 and previous whole
review29da3f33c888cb13. The prior complete research
e916e4ac7aa58fd6 remains unchanged from the consolidated review.
Fresh live gates pass Review and resolved questions; no open-questions
document exists. Proof is correctly absent before merge and does not
substitute for this review. EPIC-014 and the current FRD-02/09 principal
selection/matching clauses plus FRD-01 association paragraph bind the delta.

The exact diff from006b556ff3e995c5a2aac0cdb2a4d508ada5be23 is only two
files, +70/-2: ProcessIntake.cs and QdosAllocationRecoveryTests.cs.
Local clean HEAD, pushed PR head and stated correction agree. The prior
normal accepted-dev merge, final document paragraph and 58-path consolidated
review remain historical whole-PR evidence. The report's 52-path equality
claim belongs to its explicitly earlier006b556 checkpoint; the current two
paths are instead covered by this correction's build/tests. CASE-049 native
handoff remains unchanged. No UI/snapshot/schema/grant/package or deployment
file changed. Generated index authority was expressly handed to UIIMP-017;
no repeated capture is owed for this source-only delta.

## F-003 — fixed by cca5521a6315420129320061759209273bb64c67

ProcessIntake.cs:794-796 already derives extractionPolicy from accepted
principal context, conflicting/ambiguous-profile refusal and the selected
existing profile. Only QDOS has the settled no-competing-profile fallback.
Line808 now passes the mail route into EvaluateIntakeCaseMatch only when
that same extractionPolicy is present, instead of checking merely
conflictingProfile. There is no copied selector predicate or new rule owner.

EvaluateIntakeCaseMatch.cs:32-35 returns null for the absent route before
selecting a provider, invoking ExtractMatchKeys or querying candidates.
A non-QDOS NotApplicable profile can therefore no longer invoke the typed
extractor through the automatic email match path. The existing later
NeedsSorting/OcrRequired refusal keeps its route evidence but persists no
CaseMatchDecision. DurableIntake.cs:985-988 then has no UniqueMatch to consume,
so its existing association owner cannot bypass that refusal.

QDOS's non-conflicting body/correspondence fallback remains populated and
still reaches its existing matcher; conflicting selected principals remain
refused. ProcessIntake's ProviderApi branch at713-767 is unchanged and returns
before the mail selector/guard. It continues to use ExecuteDeclaredAsync
on the authenticated submission's declared fields, rejecting an existing
Case rather than treating it as email or requiring a document profile.
No universal correspondence grammar or new matcher/orchestrator was added.

### Meaningful durable negative and unchanged positives

The existing genuine ALS theory first creates one real SQL-backed Case,
then uniquely associates the next occurrence and replays without duplicate
allocation. Exact original hashes, accepted principal, claimant/reference/
VRM/make/model, source origin, image/readiness and persisted locator
assertions remain. The original table-role and physical-document conflict
probes were not weakened.

The added probe changes only decoded content derived from that same retained
original, removing the required Vehicle Model: signature signal. Its explicit
ReaderKey is structural-profile-signal-probe; it is not claimed as another
genuine envelope or freshly read immutable source. The selector must return
NotApplicable. Independently calling the existing matcher without the new
guard must still return UniqueMatch to that actual prior Case with claim
160754 and registration K40NLY. This makes the refused destination meaningful:
there is a known eligible unique target, not an empty fake candidate list.

A four-line test source-reader port supplies that decoded structural result.
The existing queue fixture takes ProcessIntake explicitly; its three existing
call sites pass their same registered instance. Real ReceiveIntake, durable
work/evaluation, ProcessIntake and SQL association/allocation owners process
and replay the probe. Assertions require Accepted ALS/NeedsSorting, no
InstructionDraft, match decision, CurrentCaseId, allocation, manual-association
or CaseIntakeLink row, and exactly the original one Case. Original source
bytes and hash assertions remain untouched. No test host/framework or
fabricated domain email was introduced.

## F-001 and F-002 — fixed history retained

Both remain fixed by7fcd4c662c5457024d1d20c5fe0c02c842e98a63, verified in the
prior consolidated review and unchanged in this two-file delta. Git equality
for both production owners passed exit0.

F-001 retains existing Locator and RawValue through both directions of the
same EfIntakeReceiptStore candidate JSON record. Located/unlocated roundtrips
and exact printed SourceValue remain, as does the real persisted ALS column2
assertion. No schema, migration, alternate serializer or compatibility layer.

F-002 keys SourceStructure by existing DocumentIdentity plus Table, rather
than global table number. The ALS two-physical-source probe retains identical
table/row/column coordinates but distinct source labels and contradictory
supplied registrations; both candidates survive, HasConflict is true and
typed registration is absent. Missing/duplicate client cells and missing
paired header still refuse values instead of borrowing owner/third-party
columns. No table renumbering, glyph decoder or new parsing framework.

## Focused runtime evidence read independently

No reviewer execution. Root job80003 performed the frozen correction build
and focused checks before the commit; author confirms no subsequent source
change. Root reports whole incremental Release build exit0,111.42s,
zero warnings/errors. Both actual TRXs were read for names, counters and
timestamps and recomputed SHA-256 values match report70312df54319f367:

- artifacts/verification/tick-035-profile-guard-core.trx:
  2 total/executed/passed,0 failed/error/skipped/inconclusive.
  SHA25683934F6E1BD4482DA1D477F48C46717F4CF7915E3637B18221C6C37E697AC2EE.
  Start2026-09-08T03:01:13.2975607+01:00;
  finish2026-09-08T03:01:15.1708901+01:00.
  AConflictingSelectedPrincipalCannotExtractClassifyOrAssociate and
  AmbiguousCaseMatchForcesNeedsSortingOnAnOtherwiseCaseCreatedMessage
  passed. The latter demonstrates the QDOS no-selected-profile fallback
  still reaches matching and records the competing candidates; it is a
  Core policy fixture, not a newly executed genuine QDOS email journey.
- artifacts/verification/tick-035-profile-guard-destinations.trx:
  2 total/executed/passed,0 failed/error/skipped/inconclusive.
  SHA2565EDA51724AB4818C34CF69FEB70E114D4E5DC13A0536D583362DF9C5925E0026.
  Start2026-09-08T03:01:16.5555366+01:00;
  finish2026-09-08T03:02:01.3615320+01:00.
  GenuinePrincipalEmailsAllocateOnceAndAssociateRepeatedInstructions(ALS)
  and ASubmissionMatchingAnExistingCaseIsRejectedWithoutMutationOrDuplicateAllocation
  passed,42.8566268s and42.1882173s in the same invocation, not sequential
  durations. The latter uses the actual authenticated API, retained work and
  create-only existing-Case rejection.

The full report retains exact commands. Tests are in the retained
.worktrees/tick-035; root remains sole heavy verifier. The fresh result does
not erase earlier failed runs or claim a new whole suite, hosted-CI PASS,
exact-merge verification or deployment.

## Consolidated evidence and residual limits preserved

The prior review covered all58 paths and actual callers: one immutable
fifteen-principal Core identity catalog, profile/current-physical-document
selection, explicit work-type classification, principal-scoped normalization
and existing eliminator, canonical case-field vocabulary moved rather than
copied, original provenance and installed PdfPig scan geometry. The narrow
Settings label/metadata change had root's scoped capture/verify/catalogue
evidence, not a new UI framework or manual visual claim. None is changed
by this correction.

Earlier reviewed counters remain: classifier53/53 PASS; combined
Integration12/14 with2 FAIL; YML/hash/Settings4/5 with1 FAIL; genuine mail3/4
with1 ALS locator FAIL; final structural Core10/10 PASS; final provenance
Integration exactly3/3 PASS, one genuine ALS and two JSON roundtrips, not5.
Earlier inventory/version, provenance, MP geometry, YML signature and
hash-casing failures remain in the report. Prior final hashes remain
B7F1EDEBC41DA2DBA0D48DE10A0561A18BEFA8959C3B0BC390CF53517AF117FA
and20DF014FB223C516DFA34CB2F80F14DEC08F41BB967E717981B836CBF926508A.
The original genuine-mail failure remains
75482A9ACDCDEAB73B744027E237497744C22AA35F2244487CE08F90E90F432D.

ALS/FW/SBL genuine mail proves its recorded Case outcomes and replay.
YML HD4021 is later report correspondence and remains accepted mailbox with
no current draft/type/Case and zero allocation; two-deep quoted history
must not create an instruction. Fifteen standalone genuine profile/type/key
documents are separate evidence from an initial YML envelope, which is
not available and whose automatic allocation is not claimed. MP OCR text
is supplied hash-bound Astra evidence, not a new Azure operation.
No live email, Glass or cloud write occurred in this ticket review.

## Current GitHub/board evidence and handoff

After this exact-head public review settled, fresh PR readback is
OPEN/MERGEABLE/CLEAN, same collisionengineers/pegasus repository,
TICK-035-principal-routes to dev, exactcca5521a. Full GraphQL
reviewThreads(first:100) returns nodes[] and hasNextPage:false; no actual
thread is omitted or left unresolved.

Bot summary IC_kwDOThBrk88AAAABTHZo_g completed
2026-09-08T01:59:15.576330Z on the prior006b556 head with no actual
findings and mergeGateEnabled:false. Disposition: acknowledged historical
non-gating bot evidence, not a current-head security PASS or expected reviewer.
The original needs-changes public review is retained; this newer exact-head
review provides the disposition. Later actual comments/threads require a
fresh whole-file record, not silent acceptance.

Live dev is cc441645b0a62a806e34367ad75e9eaff4df8b11, unprotected with
active rules[], required contexts[]/checks[], head check_runs[] and statuses[].
Aggregate pending with zero contexts is not a required pending check and
not a CI-green claim. Root-authorized skip-ci avoids duplicate speculative
rails only; final converged CI/release and exact-merge proof remain separate.
Fresh board tip is synchronized ahead0/behind0 and equal local/remote SHA,
bound above. Plan/report/checklist/ticket timestamp remain unchanged after
reviewer settlement. No lease heartbeat or board stage move by reviewer.

No open finding remains. Root must read this whole attestation and refresh
head, plan/ticket/checks/threads and board push before its own merge decision.
The author claim/worktree remain retained. Stop before merge, proof, cleanup,
next implementation or any external delivery action.
