---
id: MAIL-010
type: ticket
title: Remove explanatory copy and a banned word from the Mail list
status: done
area: mail-communications
order: 1460
assignee: ''
profile: fix
stageEntered:
  review: '2026-08-21T22:07:04.376Z'
  verifying: '2026-08-22T03:44:33.897Z'
  done: '2026-08-22T03:44:41.137Z'
labels:
  - design
  - regression
  - release-17
links:
  - PR-053
refs:
  - docs/design/README.md
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
deployment: production
archived: false
created: '2026-08-21T21:42:40.523Z'
updated: '2026-09-01T14:44:33.040Z'
---

## Why

Found by the Release 17 design check over the Mail workspace that shipped in Release 16.
[[PR-053]] removed explanatory copy from the categorised mail selector; the list page
itself was not covered, and carries three defects against
`docs/design/README.md`.

**1. A banned word in operator-visible text** — `Mail/Index.cshtml:137`:

> No Deleted Items in the **bounded** approved scope matched "…".

`bounded` is on the closed banned-words list, and a change introducing one does not
merge. This one already did.

**2. A hint sentence for a field** — `Mail/Index.cshtml:123`:

> Enter a search term to read accepted Deleted Items within the selected approved
> mailbox scope.

"A field is a label and a control, nothing more. No hint sentence under a field." It also
narrates the scope, which the mailbox and folder navigation already shows.

**3. How-it-works copy** — `Mail/Index.cshtml:115`:

> Search includes retained messages in their current Outlook folders.

"A page never describes its own mechanics, workings, derivations." Not on the approved
necessary-copy list.

## Fix

Trim (1) to `No Deleted Items matched "…".` Delete (2) and (3). No new copy is written —
the approved necessary-copy list is closed, and every change here removes text.

## How to verify

The three strings are gone; the Deleted Items search still works with no results, with
results, and before a search; the banned-word scan over `Pages/**/*.cshtml` returns only
Razor comments.
