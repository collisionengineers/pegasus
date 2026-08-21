---
id: INTK-029
type: ticket
title: Unlink must clear the case link and warn when it cancels the case
status: implementing
area: intake-processing
assignee: claude-code
profile: fix
taken_at: '2026-08-21T21:15:37.804Z'
branch: task/qdos26008-regressions
worktree: .
labels:
  - regression
  - qdos26008
links: []
deployment: not-deployed
archived: false
created: '2026-08-21T18:17:18.865Z'
updated: '2026-08-21T21:15:50.893Z'
---

## Why

The operator unlinked QDOS26008's spawning email, confirmed the dialog, and nothing
happened. Research found **two** defects behind that, not the one originally recorded.

**Defect 1 — a reversed association kept reporting its case.** The mail projection fell
back to allocation state (`linkedCase?.CaseId ?? allocationState?.CaseId`), and the
automatic allocation attempt still names the case it created, so the link never visibly
cleared. Fixed in `1a86f5db` via `IntakeAssociations.AllocationMayStandIn`.

**Defect 2 — the spawning email cannot be unlinked at all.** A receipt whose allocation
created the case has an accepted `CaseIntakeLink` and **no** manual association —
`AutoLinkAsync` explicitly refuses an already-accepted receipt. But
`IntakeReceipt.CurrentCaseId` resolves to the accepted case, so the UI renders Unlink,
takes a case edit lease, shows the dialog — and then `ReverseLinkAsync` finds no manual
association and throws. The UI offers an action the store refuses. That is what "unlink
did very little" actually was.

## Operator-directed behaviour

Unlinking the spawning email cancels the case. Warn before the mutation, naming the
reference. On confirmation the case closes as the new terminal outcome
**`SourceEmailUnlinked`**, shown as `Cancelled — email unlinked`.

This supersedes the original `CreatedInError` proposal: that outcome requires the atomic
corrected-principal replacement action and is refused by the generic close
(`CaseLifecycle.cs:466`). Operator chose a new terminal outcome.

The accepted origin link is never deleted — an inactive association row is written and
`CurrentCaseId`'s existing precedence rule clears the link, preserving both origins.

Unlinking a non-spawning receipt keeps today's behaviour and leaves the case open. No
new "next action" UI is needed: `Mail/Message.cshtml:444-459` already renders the case
search-and-link form once no case is associated.

## How to verify

Unlink the spawning email of a scratch case: the sentence appears, the case closes as
`Cancelled — email unlinked`, the inbox stops naming the case, and the search-and-link
surface returns.
