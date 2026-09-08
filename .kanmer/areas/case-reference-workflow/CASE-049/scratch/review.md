---
kind: review-attestation
pr: "690"
head_sha: "24eb2f77276fd7eb847f8c1746e6909113866b58"
verdict: pass
reviewer: "codex-v1-remediation-root"
independent: true
plan_hash: "a5e40190fbaa6886"
ticket_updated: "2026-09-08T01:12:38.095Z"
board_sha: "af315c79f0e9f07f3b3552d9bea44618027618e1"
expected_reviewers: ["codex-v1-remediation-root"]
threads_snapshot: []
findings:
  - id: F-001
    severity: note
    summary: "Unchanged FRD-01 correspondence paragraph still names only QDOS-direct association."
    disposition: deferred-to-ticket
    ticket: TICK-035
---

# CASE-049 independent review

## Inputs and independence

Root did not author this PR; pack_reconcile authored the implementation.
Reviewed all 28 changed paths at 24eb2f77276fd7eb847f8c1746e6909113866b58,
based on accepted dev19e6f523bf6760cab39104b4dca3674b0ac8a512.
Read complete research3c0b558c3e2f9cb5, files4689efb462748ddf,
plan a5e40190fbaa6886, report9f6dc85004bf5c07, checklist4fdb7ce5a5daaa18,
execution scratch and EPIC-014 context. Governing FRD01/11/12 and design
changes agree with the current operator-directed native handoff. Protected
operator notes are untouched.

## Actual caller and correctness

Case Details renders the existing eligible Engineer options outside optional
EVA. The actual antiforgery-protected Workflow AssignEngineer POST supplies
the authenticated actor, submitted version, lease, operation identity and a
server-chosen reason. Core selects ReportPreparation; the existing SQL
serializable mutation records assignment, sign-off, state and the existing
state_ReportPreparation event in one operation. Persisted readiness and
lease/version guards remain, replay never reapplies, and the exact state
history preserves report-Sent timing. No exported-EVA fact is manufactured.

One Core lifecycle policy replaces the duplicate native/report gates and
obsolete export/history tuple. Both redundant SQL subqueries are removed.
Retained workspace data remains readable; native edits require With Engineer,
role and real edit authority. Actual optional EVA actions remain available in
Review/With Engineer and are not represented as native engineering completion.
The duplicate StartWork UI/POST is removed; its legitimate headless Core
transition remains. No schema, provider, package or new architectural layer.

## Evidence and limits

Root observed locked restore and Release build PASS65.07s/zero warnings,
56 Core PASS and32 actual SQL/Web PASS with no skips. Exact evidence hashes:
EEE5754A1286B5E5367B0AF4F03EDE596D7A9BDCC9CD8B960480F8B1D4E9CFC2 and
2CFA92669142BA7DB422E95D55F8617D55D2772A6436BF1E9C67CF1D49E42336.
The three actual route captures produced a scoped update2PASS, verify2PASS
and catalogue60routes/67prototypes/zero broken links. Read both committed
snapshot diffs; native dialog replaces the second lifecycle action.
No manual visual, hosted CI, live Glass/OCR/mail or deployment PASS is claimed.
No tests were rerun solely for this review.

Live GitHub gather: exact head/base/branch match; dev rules[]; branch
protected:false with required checks[]; status rollup[]; reviews[] and
reviewThreads[]/hasNextPage:false. The automatic security-review status
comment5577565673 reports running, has no finding, and is not an expected
reviewer or configured gate. Its disposition is informational pending status,
not completed security evidence. Root's expected review is settled here.
Required-check absence is explicitly configuration, not a fabricated green CI.
The final converged integration/release verification remains owed.

## Finding disposition

F-001 is outside this native-handoff diff: the unchanged incoming-cancellation
paragraph still limits automatic association to QDOS-direct. TICK-035 owns
generalized current routing/matching and root has assigned that one paragraph
after this PR integrates, preserving the actual cancellation/manual-state
guard. Do not expand this handoff PR into intake behavior. No blocker/major
or undispositioned finding remains.

## Decision

PASS for bounded reviewed implementation. Operator-authorized root may merge
only after a fresh unchanged head/check/thread/board gather. Exact merged-SHA
verification and Done remain kanmer-verify's separate obligation.
