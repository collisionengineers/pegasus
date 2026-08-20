# Proof — INTK-011

## Merge

PR #434, merge commit `2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5` on `dev`/`main`
— this PR's merge **is** the release-13 head itself (the last PR integrated
before promotion).

## Deployment

Shipped in **release 13** (`2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5`,
deployed 2026-08-20 ~01:10–01:20Z). See [[DELIV-012]] proof (Appendix —
Release 13) for the deployment readbacks: the sole release-13 migration,
`20260819234014_GrantWorkerIntakeSubmissionGroupRead`, is this ticket's own
grant-only migration.

## Production evidence

- **Worker grant read back**: `sys.database_permissions` readback confirms
  `pegasus_worker_runtime_role` SELECT on `IntakeSubmissionGroups` and
  `IntakeSubmissionGroupMembers` — the gap this ticket's root-cause analysis
  proved (the tables' original migration wrongly claimed "the Worker never
  references either table").
- **Straggler recovery**: the production JPEG receipt `5b4c8cbd-c40a-43a0-b5c0-73c1c447ada2`
  — stranded by the release-12 concurrency race this ticket fixes — was
  recovered as `U6` by the product's own reconciliation mechanism after
  release 13 deployed (readback: `OriginId = 5b4c8cbd…`).

## Honest qualification

The recovery took the **>2h escalation branch** of this ticket's
reconciliation sweep (direct Unidentified registration), **not** absorption
into the same `G6KDL-01` Image-initiated Case, because the straggler
predated the fix's deployment by more than the 2-hour `EscapeAfter` bound.
This matches the ticket's own design note: the ImageIntake aggregate is
one-receipt-per-row by design, so even a same-VRM recovery inside the bound
would register as a second row (e.g. `G6KDL-02`), not literally merge into
the `G6KDL-01` row — "recovered ... into G6KDL-01" is read as "into the
G6KDL evidence set," not the same row. Future race victims within the
2-hour bound are re-driven into their group's outcome via the ordinary
replay branch, per the ticket's `ConcurrentGroupMembersNeverSplitAcrossRepeatedRuns`
test (36/36 concurrency trials green). A separate, pre-existing gap the
ticket surfaced and left out of scope: an **ordinal-0** group member cannot
be recognised as a group member by the reconciliation sweep either (a token-
encoding ambiguity) — flagged for a follow-up ticket, not fixed here.
