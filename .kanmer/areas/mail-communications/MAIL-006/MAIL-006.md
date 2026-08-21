---
id: MAIL-006
type: ticket
title: Rebuild the Inbox message page on the record container
status: backlog
area: mail-communications
assignee: ''
profile: feature
labels:
  - ui
  - web
  - design-approved
links:
  - TICK-046
  - TICK-047
  - TICK-049
  - TICK-050
  - TICK-051
  - TICK-052
  - MAIL-008
  - PLAT-019
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-21T08:24:32.333Z'
updated: '2026-08-21T09:38:16.292Z'
---

## What

Rebuild `/Inbox/{id}` (`src/Pegasus.Web/Pages/Mail/Message.cshtml` and its
PageModel) to the approved design. The design is the exact target, not a
direction to interpret. It is stored in the repository at
`docs/design/references/mockups/inbox-message-page/` — open
`preview/Main.html` in a browser — and on the canvas at
<https://claude.ai/code/artifact/60fbd406-0644-4754-9107-2669e5faa97e>.

Message body first; decision and both its actions in a sticky right-hand card;
case association promoted to a fourth tab; no action bar; the machine evidence
gone.

**Base this on `origin/dev`, not `main`.** The deployed page is 385 lines; the
`dev` page is 575, with a Case association flow and a Folder recommendation
section that production does not have. An earlier revision of this design was
drawn against production and is superseded.

## Why

This is the one screen where an operator decides what a received message is,
and it answers that question last: the body sits below three panels of machine
evidence, and the resolving action below that again. It is also off the house
style — `docs/design/README.md:176` says a single-record screen is "one
container — header, action bar, tabs", `site.css:1542-1607` implements exactly
that as `.record`, and `Pages/Cases/Details.cshtml` uses it, while this page
stacks flat `.panel`s under a `.back-link`.

Most of what is removed is already forbidden. [[TICK-046]] built the evidence
panel in good faith; the design authority has since settled *No explanatory
copy and page economy* (operator direction 2026-08-20, `README:420`), and the
panel does not survive it. This supersedes that part of [[TICK-046]] and keeps
the rest — the correction history and the correction action both stay.

Everything the nine MAIL tickets in Verifying shipped is preserved and given a
home: [[TICK-047]]/[[TICK-049]]/[[TICK-050]] become the Folder row and its one
action, [[TICK-051]]/[[TICK-052]] become the Case tab, MAIL-11's thread keeps
its tab, MAIL-23's taxonomy is the Destination row. UI-10's quick preview is on
the list page and is untouched.

## Approach

The exact file-by-file, step-by-step integration method is this ticket's
**files** document. In outline:

- One `.record` container: dark band head (subject, wrapping, plus the state
  chip), `.record__accent`, four tabs, `.record__body`. **No `.record__bar`** —
  both actions live in the Decision card beside the rows they change. Record
  that as a deliberate departure from `README:176`.
- Message tab is `.split-main`: letter left, Decision card right, Corrections
  card beneath it when history is non-empty.
- Case tab is a single 680px column carrying the whole association flow.
- Delete `Open case`. `Filed to` becomes the case link once filed.
- The folder move becomes a **confirmation, not a form** — no typed reason. The
  designated folder is named in the dialog title and the recorded reason is
  shown as a value. There is no override:
  `frd-08:243` — "Staff may confirm only the designated folder… A different
  destination requires correction of that classification, not an arbitrary
  folder choice."
- Render the letter as paragraphs, split the quoted forward header out, cap the
  measure at 68ch. View-only: `StaffForwardBodyCleaner` already normalises the
  text.
- Write no new operator-facing guidance copy. The approved list at `README:400`
  is closed and only the operator adds to it.

Two dependencies, each its own ticket, because each reaches beyond this page:
[[MAIL-008]] (the classification and move-reason labels — the design shows
proposals, not settled terms) and [[PLAT-019]] (the shared reason-dialog copy).

## Verification

- [ ] Rendered page matches each artboard at 1280 wide — tokens, control
      heights (34px inputs, 32px rows) and type ramp taken from `site.css`,
      never eyeballed.
- [ ] Identity, state, available actions and the start of the message are all
      above the fold at 1280×800 (`README:176`).
- [ ] No policy key, version integer, predicate key, `Reason` sentence or
      provenance sentence renders — grep the response HTML.
- [ ] `Open case` absent; a filed message links its case from `Filed to`.
- [ ] All six POST handlers still work end to end, including every optimistic
      concurrency check and the uncertain-move replay.
- [ ] No inline `style` attribute — `AccessibilityTests` already asserts this
      for `/Inbox` and would fail.
- [ ] `dotnet build --configuration Release` clean; `MailWorkspaceWebTests` and
      the Browser lane green.
