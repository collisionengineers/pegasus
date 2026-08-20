# Proof — CASE-003

## Merge

Fixed inline within **[[INTK-010]]'s PR #433**, merge commit
`ac1627c004025e829862d131e816425d27147d9c` on `dev`/`main`. This ticket has
no PR or branch of its own — its exact specified fix (`GET /Cases/Create`
with no `receiptId` returns 404 before `LoadAsync` runs, instead of
throwing) was implemented as an in-scope defensive backstop inside INTK-010,
per INTK-010's post-implementation report: "in scope because the
confirmation step's own 'Create a case' offer always carries a real
`receiptId`, but the guard is cheap, correct, and exactly CASE-003's own
specified approach."

## Deployment

Shipped in **release 13** (`2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5`,
deployed 2026-08-20 ~01:10–01:20Z), as part of INTK-010's diff. `ac1627c0`
is a verified ancestor of `2325ed4a`. See [[DELIV-012]] proof (Appendix —
Release 13) for the deployment readbacks.

## Production evidence

No CASE-003-specific production browser check is separately recorded;
evidence is the deployed release-13 image containing INTK-010's guard, plus
INTK-010's own green Web test suite (`CaseCreateWebTests.cs` extended for
this path per its post-implementation report).

## Qualification

This ticket never went through its own preparing/implementing/review
pipeline — it has no `files`/`plan`/`post-implementation-report` documents
of its own (still in `backlog` at time of writing). This proof documents
that the fix is real and deployed even though the ticket's own document
pipeline was never walked; it cannot advance past `preparing` under its
`fix` profile gates (`files`, `plan` required to leave Preparing) without
those documents being written, which is outside this proof-writing pass's
scope.
