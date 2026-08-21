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
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-21T08:24:32.333Z'
updated: '2026-08-21T08:24:32.333Z'
---

## What

Rebuild `/Inbox/{id}` (`src/Pegasus.Web/Pages/Mail/Message.cshtml`) to the
design approved by the operator on 2026-08-21. The design is not a direction
to interpret — it is the exact target, saved at
`docs/design/references/mockups/inbox-message-page/` with its own README, and
viewable at
<https://claude.ai/code/artifact/60fbd406-0644-4754-9107-2669e5faa97e>.

Message body first in the main column; decision and both its actions in a
sticky right-hand card; no action bar; the machine evidence gone.

## Why

The page is the one screen where an operator decides what a received message
is, and it answers that question last. The body sits below three panels of
evidence, and the resolving action sits below that. It is also off the house
style: `docs/design/README.md:176` says a single-record screen is "one
container — header, action bar, tabs", `site.css:1542-1607` implements exactly
that as `.record`, and `Pages/Cases/Details.cshtml` uses it — this page stacks
flat `.panel`s under a `.back-link` instead.

Most of what is being removed is already forbidden. [[TICK-046]] (done,
deployed) built the evidence panel in good faith; the design authority has
since settled `No explanatory copy and page economy` (operator direction
2026-08-20, `README:420`), and the panel does not survive it. This ticket
supersedes that part of [[TICK-046]] and keeps the rest — the correction
history and the correction action both stay.

Body rendering is a view change, not a text change: `StaffForwardBodyCleaner`
already normalises the text, and `<pre class="mail-body">` with
`white-space: pre-wrap` is what produces the gapping. Suppressing the original
sender's trailing signature and disclaimer is separate work, tracked on its
own ticket.

## Approach

- Wrap the page in `.record` with `data-state`: dark band head carrying the
  subject and the status chip, 3px `.record__accent`, `nav.tabs`
  (Message / Attachments *n* / Thread), `.record__body`.
- `.record__body` is `.split-main` — body left, a new `.decision` card right.
- Move `Correct classification` and `Move to folder` into the `.decision`
  card. Both open `Shared/_ReasonDialog`. No `.record__bar` — a bar holding
  one button is chrome for its own sake, and the actions belong beside the
  rows they change. Record this as a deliberate departure from `README:176`.
- Delete `Open case`. When the message is filed, the `Filed to` row carries
  the case reference as the link to `/Cases/{id}`.
- Render the body as paragraphs: blank line is a paragraph break at 14px,
  consecutive lines stay tight at 4px, measure capped at 68ch. Split the
  retained `From:/Sent:/To:/Subject:` block out as a quoted header.
- Remove every row the README already bans — the full list with its citation
  is the table in the mockup README. Relabel the `Queue` figure; `Accepted` is
  a routing disposition, not a work list an operator visits.
- Reuse `.facts`, `.status-chip`, `.tabs`, `.btn`, `.prov`, `.split-main`,
  `_StatusChip`, `_ReasonDialog` as they are. Only `.decision` and the
  `.mail-*` letter rules are new to `site.css`.
- Provenance is the `.prov` icon with `data-word="Automatic"` beside the
  decided time — never a rendered sentence (`README:177`).
- Write no new operator-facing guidance copy. The approved list at
  `README:400` is closed and only the operator adds to it.

## Verification

- [ ] The page matches the approved artboards at 1280 wide — tokens, control
      heights (34px inputs, 42px bar, 32px rows) and type ramp taken from
      `site.css`, not eyeballed.
- [ ] Identity, state, available actions and the start of the message are all
      above the fold at 1280×800 (`README:176`).
- [ ] No policy key, version integer, predicate key, `Reason` sentence or
      provenance sentence renders. Grep the built markup.
- [ ] `Open case` is absent; a filed message links its case from `Filed to`.
- [ ] Correction and folder move still work end to end, including the
      concurrency check on the expected classification version.
- [ ] `dotnet build --configuration Release` and the Mail page tests pass.
