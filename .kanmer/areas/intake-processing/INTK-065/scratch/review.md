---
kind: review-attestation
pr: "706"
head_sha: "5e0aeb47b9cb87258e12f66967e1efee4743b3f3"
verdict: pass
reviewer: "/root/parallel_plan_review"
independent: true
plan_hash: "b7e0dfdbb97bcd01"
ticket_updated: "2026-09-08T14:45:48.793Z"
board_sha: "04e11b24a38f5e4032dbaca762b831e87b251f67"
expected_reviewers: ["/root/parallel_plan_review"]
threads_snapshot: []
findings:
  - id: F-001
    severity: note
    summary: "The optional unit lane exposes two unchanged stale architecture assertions."
    disposition: deferred-to-ticket
    ticket: DELIV-058
  - id: F-002
    severity: note
    summary: "The optional SQL lane exposes three unchanged historical vehicle-lookup fixture inserts that omit the current non-null column."
    disposition: deferred-to-ticket
    ticket: DELIV-057
  - id: F-003
    severity: note
    summary: "The optional SQL lanes expose unchanged intake fixtures that now route to NeedsSorting or do not create an allocation."
    disposition: deferred-to-ticket
    ticket: DELIV-056
  - id: F-004
    severity: note
    summary: "The optional browser and Test UI lanes reproduce an unchanged upload case-search timeout."
    disposition: rejected-with-reason
    reason: "PR 706 changes no upload, browser, Web, runtime, or integration-test path, and the identical test already failed on dev promotion run 34235294697 at head 6509746913eda16d2c4440add20e7f6793500f0b; root is investigating the baseline class separately."
  - id: F-005
    severity: note
    summary: "The optional SQL lane exposes an internally contradictory unchanged ENG-034 Send-to-Claude accessibility assertion."
    disposition: deferred-to-ticket
    ticket: ENG-034
---

# INTK-065 independent review

## Inputs and independence

The reviewer is a separately assigned reviewer role and did not author the change.
Reviewed PR 706 at exact head 5e0aeb47b9cb87258e12f66967e1efee4743b3f3
against plan b7e0dfdbb97bcd01 and ticket revision rev1:22467f0c3b14383c.
The PR is open, ready, mergeable, targets dev, and has nine logical changed paths
including two renames. The head, bounded diff, complete ticket packet, EPIC-014
context, FRD-09, implementation report, verifier record, checks, reviews,
comments and all review threads were gathered. Reviews and threads are empty
on this head; one informational status comment is dispositioned below. The
expected reviewer set is settled by this record.

## Changes reviewed

The existing generator and matching Core test now describe the five current
principal-policy snapshots. The tracked package changes only its purpose,
current source metadata/references and 26 approved evidence references. All 220
referenced ids resolve; source modes, paths, hashes and byte counts match the
current files. The seven historical evaluation/evidence/cohort/crosswalk/
criteria/support sections and immutable source snapshots remain unchanged.

The principal documents move to docs/principal-profiles. qdos.md is
byte-identical. README preserves the moved content and adds only the approved
five-line historical-v1/current-source clarification; docs/index changes only
the matching link. The existing placement allow-list and its regression admit
only that canonical destination, and AGENTS records the changed convention.
No src, runtime policy, original corpus, schema, deployment, dependency,
compatibility path, or historical review record changes.

## Acceptance evidence

The final generated package SHA-256 is
494e0a0f42ced164aab97cd50ebb497c1479c09eaf9f0a4db177949bbdc7c251.
The final helper materialization occurred once after the clarification; the
earlier materialization is retained as superseded. Independent host-serialized
evidence recorded: corrected structural/determinism harness exit 0 after
retaining its first harness-only exit 1, Python generator tests 2 passed,
documentation links 140 files passed, Markdown placement passed, locked Core
restore and Release build passed with zero warnings/errors, focused
PrincipalIdentificationCorpusTests 7 passed, and git diff --check passed.
Commit content matches the reviewed frozen diff.

Acceptance check 2's summary says both moved documents are byte-identical, while
the same plan's objective, required changes, expected-files table and Step 1
expressly require the README clarification. The coherent planned result is
therefore the observed one: qdos.md byte-identical, README changed only by the
approved clarification, and one index target replacement. No unplanned
documentation rewrite is accepted.

Full original-input regeneration is unavailable on this host and is not claimed
as PASS. The approved existing-helper evidence boundary is the only
materialization claim.

## Required checks and broader CI

Immediately before this record, GitHub reported: no required checks reported on
the INTK-065-principal-evidence-inventory branch. Under the review policy, only
a failed or missing required check blocks this bounded review; absence is
configuration evidence, not a fabricated green full suite.

The broader exact-head repository-check run 34240260482 completed FAILURE and
remains explicit non-PASS evidence. changes, documentation,
local-development-scripts, reference-data and sql-integration-coverage passed;
infrastructure was path-skipped. unit, browser, test-ui and all three SQL shards
failed. None of those failures is caused by or repaired in this diff:

- F-001: unit passed 1,907 Core tests with 14 skipped, then failed 2 of 116
  architecture tests. DELIV-058 owns the two current-contract assertion fixes
  in its frozen two-file scope.
- F-002: SQL shard 1 passed 658, skipped 1 and failed 4; three failures insert
  NULL InstructionConfirmedByStaff into the historical vehicle-lookup fixture.
  DELIV-057 owns that exact fixture correction.
- F-003: SQL shard 2 passed 612, skipped 3 and failed 29; SQL shard 3 passed 620
  and failed 15. The dominant class expects accepted allocation but current
  evidence routes NeedsSorting or produces no IntakeAllocationState. DELIV-056
  owns the affected evidence-fixture class and its remaining failures are still
  under investigation. The shard-1 held-lease setup failure is the same
  CaseCreated-versus-NeedsSorting shape.
- F-004: browser and Test UI each passed 134 of 135 and failed the same
  UploadCaseSearchBrowserTests case after a 30-second wait for
  details.upload-attach > summary. The identical unchanged test also failed on
  earlier dev promotion run 34235294697, proving it is not introduced by PR
  706. Its separate root-cause investigation remains open.
- F-005: SQL shard 3 also failed
  InaccessibleCaseCannotPostSendToClaude. Commit
  6a2c3af779201144def500c964524902fc560d79 introduced it for ENG-034. Its XML
  says the inaccessible control is absent, but its regex asserts a
  gated/disabled control. The current partial deliberately omits all mutation
  controls when AssessmentIsReadOnly, while the POST CanOpen guard still fails
  closed. ENG-034, currently Verifying and blocked, owns this stale assertion.

The ready event triggered Codex status comment IC_kwDOThBrk88AAAABTQo_Iw on this exact head. Its security review completed at 2026-09-08T15:23:17.056799Z with no suggestion, review or thread; its metadata says mergeGateEnabled false. The comment is informational and has no finding to disposition.

These notes are not waivers and do not convert the broader run to green. They
keep each baseline failure with its owning corrective work so independently
reviewed corrections can integrate without requiring every sibling fix to have
already integrated. The corrective programme still owes one converged full
repository gate before promotion.

## Finding disposition and decision

F-001, F-002, F-003 and F-005 are terminally deferred to their named owning
tickets. F-004 is rejected as a PR-706 finding because exact baseline evidence
shows it is unchanged and non-causal; its investigation remains recorded
rather than silently treated as PASS. No open finding, security risk, data-loss
risk, destructive risk, unmet scoped acceptance check, stale review input, or
failed/missing required check remains.

PASS for the bounded, non-runtime INTK-065 implementation. This decision does
not claim the broader repository suite is green and does not accept release or
deployment. Root has supplied explicit authority for ordinary integration to
dev only. The merge decision still requires a fresh unchanged
head/check/thread/ticket/plan/board gather; it permits no force, bypass, waiver,
main promotion, proof, source edit or cleanup. Exact merged-SHA verification
and Done remain separate obligations.
