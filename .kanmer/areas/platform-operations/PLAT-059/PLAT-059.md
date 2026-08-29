---
id: PLAT-059
type: ticket
title: Settle the "Create Case" entry point so one label has one destination
status: backlog
area: platform-operations
assignee: ''
profile: fix
labels:
  - ui
  - shell
groups:
  - EPIC-011
links:
  - CASE-026
  - PLAT-029
archived: false
created: '2026-08-29T08:06:53.293Z'
updated: '2026-08-29T08:06:53.293Z'
---

## What

`Create Case` is drawn in four places and resolves to two different
destinations, and two of them are dead links.

| Call site | Target | Result today |
| --- | --- | --- |
| `Pages/Shared/_ShellDialogs.cshtml:64` (Add dialog card) | `/Cases/Create` | 404 |
| `wwwroot/js/site.js:1364` (Ctrl N) | `/Cases/Create` | 404 |
| `Pages/Search/Index.cshtml:24` (header action, [[CASE-026]]) | `/Upload` | works |
| `Pages/Index.cshtml` Work Centre header (context.md §1.2) | not yet ported | — |

`Pages/Cases/Create.cshtml.cs:215-219` returns `NotFound()` for an empty
`receiptId`, and every working call site (`Pages/Intake/Details.cshtml:451`,
`Presentation/UploadOutcome.cs:322`) reaches it *with* a receipt: a case is
made from received material, so `/Cases/Create` is receipt-bound by design.
A page with no receipt in hand therefore has no receipt-less form to send the
operator to, which is why [[CASE-026]] pointed its contracted header action at
`/Upload` — the only place that produces the receipt.

## Why

EPIC-011 `context.md` §1.1 draws `Upload files` and `Create Case` as separate
entries in the Add dialog, so collapsing them is a product judgement no ticket
has taken; and one label with two destinations breaks one-list-per-concept for
the label→destination mapping. It also leaves two shipped controls that 404.

## Outcome

Settle it once and apply it to every call site — either retarget the two shell
call sites to `/Upload`, or give `/Cases/Create` a receipt-less entry point
that picks or receives material. Files are [[PLAT-029]]'s
(`Pages/Shared/_ShellDialogs.cshtml`, `wwwroot/js/site.js`) and E1's
(`Pages/Cases/Create.*`); [[CASE-026]] reported them rather than editing them.
