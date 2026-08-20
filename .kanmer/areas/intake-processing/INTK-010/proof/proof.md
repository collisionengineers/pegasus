# Proof — INTK-010

## Merge

PR #433, merge commit `ac1627c004025e829862d131e816425d27147d9c` on `dev`/`main`.

## Deployment

Shipped in **release 13** (`2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5`,
deployed 2026-08-20 ~01:10–01:20Z). `ac1627c0` is a verified ancestor of
`2325ed4a`. See [[DELIV-012]] proof (Appendix — Release 13) for the
deployment readbacks.

## Production evidence

A DOM text probe against the deployed Upload page returned
`hasReceiptCopy=false, hasIntake=false, panelIsDropTarget=true` — direct
confirmation that (1) the "Each file has its own receipt and remains
associated with this submission group" mechanics narration is gone, (2) no
"intake" wording remains operator-facing, and (3) the whole upload panel
(not just the small dashed rectangle) is the drop target, per the
operator's verbatim release-12 complaints.

## CASE-003 fixed within this PR

This ticket's diff includes CASE-003's exact specified fix: `GET
/Cases/Create` with no `receiptId` now returns 404 (guarded before
`LoadAsync` runs) instead of throwing — in scope because this ticket's own
"create a case" confirmation offer always carries a real `receiptId`, but
the guard is a cheap defensive backstop matching CASE-003's approach
exactly. See CASE-003's own proof document for its ticket-level disposition.

## Honest qualification

The ticket's own post-implementation report discloses two unproven claims
rather than overclaiming them:

- **No manual 1920px visual pass was run** — "no interactive browser/
  screenshot tool was available to this agent in this environment," stated
  honestly rather than checked off. The production DOM probe above verifies
  the specific copy/drop-target claims but is not a substitute for a full
  visual pass.
- **The "drop off the panel doesn't navigate the tab away" claim** could not
  be red/green proven via CDP simulation (Chromium's real default-navigate
  action isn't triggered by CDP-injected drops either way); the
  document-level `preventDefault()` safety net is sound defensive practice
  but only proven to swallow the drop, not proven against a real browser
  navigation.
