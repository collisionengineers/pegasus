---
kind: review-attestation
pr: "683"
head_sha: "8bbceb4fd190ae80a8b656540fd0ae5973f49895"
verdict: pass
reviewer: "/root/principal_delivery_audit"
independent: true
plan_hash: "f387a6a84dfa1f21"
ticket_updated: "2026-09-07T23:10:50.097Z"
board_sha: "1255dba16125a3f62b9b06f57dbfdd997827be05"
expected_reviewers: ["/root/principal_delivery_audit"]
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Resumed fresh provider writes bypassed the existing Case edit-authority guard."
    disposition: fixed
  - id: F-002
    severity: major
    summary: "Bounded provider response failure lost unknown-write semantics and released the external account."
    disposition: fixed
---

# ENG-041 independent delta review

PASS at8bbceb4fd190ae80a8b656540fd0ae5973f49895, same PR683/dev target.
Author pack_reconcile and root's verification are distinct from this reviewer;
reviewer authored no source. Round1 is limited to the two original findings,
the three-file correction (+174/-10), direct callers/contracts and relevant
tests. No unrestricted second audit of unchanged code was performed.

The sole assigned reviewer settled at this exact head through
[public review5135725065](https://github.com/collisionengineers/pegasus/pull/683#pullrequestreview-5135725065).
Planf387a6a84dfa1f21, files9eb56be81f06f9a0,
checklist50cf90d094bb2c3c, reportf437cfd8a750979b and resolved questions
were read with current ticket/gates, EPIC-014 and FRD-06. Clean author
worktree HEAD and GitHub head match. Reviewcc5b9a51b2e4e5d3 and public
review5135577047 remain historical needs-changes evidence at
1ac8bc428e0432b510b745342fdadd849d726878.

## F-001 — fixed

Fixed by8bbceb4fd190ae80a8b656540fd0ae5973f49895.
GlassRepairEstimateGateway.cs369-381 now rejects missing supplied version/
lease and invokes the existing IGlassRepairEstimateCaseAuthority with the
current Engineer actor before Prepared/known-vehicle fresh provider work.
The Ef authority still delegates to CaseMutationGuard; Web does not own a
second policy. Regained version/token are written into protected provider
state and reach ContinueLaunchAsync's versioned checkpoint before provider
work, then the later import. Existing owner and session-version guards remain
ahead of the new authority check. Known-ID-only reopening/reconciliation is
unchanged.

The two new SQL cases use the actual EfGlassRepairEstimateCaseAuthority and
CaseMutationGuard: missing fields, stale version, wrong token, foreign holder
and expiry refuse without an additional provider request or session change.
Valid regained authority then resumes once and reaches the recorded import
with that same version/token. Existing interrupted success callers now supply
their required authority; no original assertion was removed.

## F-002 — fixed

Fixed by8bbceb4fd190ae80a8b656540fd0ae5973f49895.
GlassMvaClient.cs697,757-770 propagates the existing outcomeUnknown argument
through the bounded ReadAsync exception. Create/start callers already mark
their write uncertain; overflow now reaches the unchanged gateway settlement
as Unknown, and the unchanged SQL occupying-account predicate retains the
reservation. Download/read-only calls explicitly retain false and existing
maximum byte limits are unchanged.

Two new SQL create/start overflow cases reconstruct the gateway, advance past
expiry, retain Unknown and ActiveAccountKey, refuse a second launch and assert
unchanged provider call/session counts. The read-only overflow negative and
existing export-size case show definite refusal still works. The private
signature reordering addresses CA1068 only; no extra exception taxonomy,
recovery wrapper or provider API was added.

## Evidence and retained attempts

Root's correction first build FAILED CA1068, exit1,21.70s. The parameter-order
fix preserved behavior; corrected integration-project build PASS49.39s.
The exact correction cohort then passed10/10, zero failed/skipped/aborted/
inconclusive, reported53s. Reviewer read actual case names/counters and
SHA256 from tests/Pegasus.IntegrationTests/TestResults/
eng-041-review-correction.trx:
AC6BBEA97CFF7854B40D48515ABC0D900DCE79C3B5D92C48E97CE2F5046510B2.
TRX UTC instants2026-09-07T23:01:36.5841269Z through23:02:33.7666946Z.
Its runner envelope is not the reported per-test duration.

Earlier compiler, timestamp-double and missing-capture failures and corrected
passes remain in reportf437cfd8a750979b and original review. No reviewer
build/test, live provider/cloud call or source edit ran. Scoped diff --check
exit0. No Razor delta exists, so no repeated snapshot capture was requested;
the original routed snapshot evidence remains bounded by the first review.
A guessed authority file path was corrected using rg discovery; this
read-only lookup failure was not a test attempt.

## Current checks and handoff

No new finding arises from the correction. Original findings are fixed, not
deleted. GitHub has no inline review threads (complete empty page); the old
automated summary is informational and contains no actionable finding.
Live dev protection returned404 Not protected, branch rules[], check runs[]
and status contexts[]; aggregate pending without a context is not a claimed
required-green CI. Board SHA above was pushed with ahead0/local=remote.
Current exact head remained OPEN/MERGEABLE/CLEAN during gather.

Final integrated CI, exact merged verification and deployment remain separate
root-owned obligations. No manual viewport/zoom or live Glass correctness
claim is made. Kanmer-review kept this round to the original findings and
their correction; no self-review or merge occurred. Root whole-verdict read
and separately authorized fresh merge checks are next.
