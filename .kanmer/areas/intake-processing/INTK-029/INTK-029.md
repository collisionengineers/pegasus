---
id: INTK-029
type: ticket
title: Unlink must clear the case link and warn when it cancels the case
status: done
area: intake-processing
order: 1600
assignee: claude-code
profile: fix
stageEntered:
  review: '2026-08-21T22:06:44.740Z'
  verifying: '2026-08-22T04:35:59.322Z'
  done: '2026-08-22T08:32:41.582Z'
labels:
  - regression
  - qdos26008
links: []
deployment: production
archived: false
created: '2026-08-21T18:17:18.865Z'
updated: '2026-08-25T01:27:00.608Z'
---

## Why

The operator unlinked QDOS26008's spawning email and the inbox went on showing the
case linked, with no further action — a dead end.

**Root cause.** The mail projection resolved the case as
`linkedCase?.CaseId ?? allocationState?.CaseId`. The automatic allocation attempt still
names the case it created, so once the association was reversed the fallback put the
link the operator had just removed straight back on screen. The unlink worked; it never
looked like it had. Fixed in `1a86f5db` via `IntakeAssociations.AllocationMayStandIn`.

(A second defect was diagnosed and then disproved — see research. `EfCaseAcceptanceStore`
writes an active manual association alongside the accepted `CaseIntakeLink`, so the
spawning receipt was always unlinkable. A pre-existing test caught the error.)

## Operator-directed behaviour

Unlinking the email that created a case now cancels that case. The dialog warns first,
naming the reference. The new terminal outcome is `SourceEmailUnlinked`, shown as
**`Cancelled — email unlinked`**.

This supersedes the original `CreatedInError` proposal: that outcome requires the atomic
corrected-principal replacement action and is refused by the generic close. Operator
chose a new terminal outcome.

The rule is decided once, on the receipt — true only while the receipt's current link is
the case its own acceptance created. A receipt since relinked elsewhere is not that
case's source and unlinking it leaves that case alone. The accepted origin row is never
deleted; both origins stay on the record.

**This removes a capability, deliberately:** the origin can no longer be unlinked and
freely relinked. An unlink cannot both cancel the case and leave it relinkable. Recovery
is a deliberate reopen with a reason — `SourceEmailUnlinked` is not on the reopen bar.

No new "next action" UI is needed: `Mail/Message.cshtml:444-459` already renders the case
search-and-link form once no case is associated.

## How to verify

Unlink the spawning email of a scratch case: the sentence appears, the case closes as
`Cancelled — email unlinked`, the inbox stops naming the case, and the search-and-link
surface returns.
