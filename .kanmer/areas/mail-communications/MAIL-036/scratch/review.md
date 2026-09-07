---
kind: review-attestation
pr: "678"
head_sha: "a0260a4ef6fca856d86b4c44c5b0c863d93276b8"
verdict: pass
reviewer: "principal_delivery_audit"
independent: true
plan_hash: "1b32dda114d7a815"
ticket_updated: "2026-09-07T20:44:41.973Z"
board_sha: "da83dbb775c6fb677b2295a1cc88f425b3f53dc9"
expected_reviewers:
  - "principal_delivery_audit"
threads_snapshot: []
findings: []
---

# Independent review — MAIL-036

Consolidated review of PR 678 at the exact head above. The author is the
root implementation lane; this independent agent authored none of this diff.
Root explicitly delegated merge authority under the current operator request.
Expected reviewer principal_delivery_audit has settled on this head in GitHub
review 5135173623 (COMMENTED, same repository credential but independent role).

## Acceptance and safety

The full 12-file diff matches the bounded plan, including the operator's
exact-notification clarification. The existing wipe invokes one parameterised
SQL reset inside its XACT_ABORT transaction with deletion. The exact Worker
must already report Stopped before any deletion. Blob/SQL authentication,
inventory and delete failures stop; maintenance excludes application writers
and failed wipes must stay in maintenance. No live wipe is authorised here.

Reset advances existing receive boundaries monotonically, clears stale
cursor/leases, and seeds activated-but-unpolled mailbox state without editing
approval, activation, subscription or reference sequences. Scope/generation
rebinding only raises the effective boundary to a later activation. The exact
reset body is exercised by the SQL tests; no copied test-only policy exists.

Graph refuses historical notification MIME before download, and the Core
receive-time filter remains authoritative. ExecuteNotificationAsync handles
only its named message, then completes its owned lease through the existing
serializable store helper, without changing Cursor, DueAtUtc, LastCompletedAtUtc
or LastFailureCode. Lease-loss remains an error. IntakeFunctions uses this
caller for Created wakes, while lifecycle wakes and the existing timer retain
the recovery cursor scan.

FRD-08, runbook, AGENTS and the wipe skill agree with the current operator's
reset and exact-message semantics. No new schema, package, worker or policy
owner is introduced.

## Evidence and live policy

Accepted root evidence, not rerun by this reviewer: locked restore and Release
builds exit 0 with zero warnings/errors; 72 initial SQL/Graph cases; 27 clarified
notification Core cases; 3 updated reset/lease SQL cases. PowerShell parse,
Markdown placement and diff checks passed. Reviewer static diff check exit 0.
This is pre-merge evidence, not production or post-merge proof.

Live dev protection endpoint returned 404 Branch not protected; applicable
branch rules returned []; PR required-check query reports no checks and
check-runs is empty. The deliberate skip-CI corrective PR bypasses no required
check. Final integrated v1 release verification remains owed to the controller.

At gather, PR is OPEN, CLEAN, same-repository head MAIL-036-wipe-boundary to
dev, exact head unchanged. Review-thread GraphQL returns zero threads with no
next page. The sole automated status-only issue comment
IC_kwDOThBrk88AAAABTFL_0Q has no finding and reports advisory review activity,
not a required or expected reviewer. There are no undispositioned findings.
Board tip above was pushed (ahead 0/behind 0).

## Handoff

Re-gather head, checks, threads and board sync immediately before authorised
squash merge. After confirmed merge move only Review to Verifying.
kanmer-verify/root owns exact merged-SHA proof and Done; this review writes none.
