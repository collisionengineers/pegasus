---
id: DOCS-012
type: ticket
title: 'Show case evidence on the Evidence tab, not the document custody ledger'
status: backlog
area: documents-reports
assignee: ''
profile: fix
labels:
  - found-during-qa
  - ui
  - design
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-23T15:19:38.272Z'
updated: '2026-08-24T09:21:16.137Z'
---

## What the operator saw

> *"**Issue 2** — Document custody box in evidence is unneeded detail. We only
> need to see the files that are considered case evidence. If they show here,
> they should be on box."*

## Answered by the operator, 2026-08-24 — no open questions remain

> *"1. Include button for box folder. Per file 'custody state' is dev speak
> leaking into UI. Just show whats been stored. Keep it simple.*
>
> *2. Changes go in notes (created by system same as other automatic notes)*
>
> *3. The controls need rework:*
> - *Theres an export control that isnt needed.*
> - *Removal can just be a delete/trash icon next to each file/image*
> - *"retain document" as a control makes zero sense. Its already stored so how
>   can it be retained?*
> - *Semantic role shouldn't be user configurable"*

That settles every question this ticket was parked on, and it settles them more
sharply than the options I offered.

## What this means, control by control

| Today | Becomes |
| --- | --- |
| Box folder link | **Keep.** A button. |
| Per-version `Custody` column | **Gone.** "Custody state" is internal vocabulary. The tab shows what is stored; that a file is listed *is* the statement that it is stored. |
| `Revision state` (`Current` / `Historical` / `— logically removed`) | **Gone** from the tab as a column. Superseded by the notes rule below. |
| `EVA eligibility` cell, *"Eligible unless staff confirms third-party vehicle evidence"* | **Gone.** A how-it-works sentence in a table cell, already banned by the design authority. |
| Selective export by version (tickboxes + submit) | **Gone.** The whole-case Export action on the case header stays; that is the one the operator uses. |
| `Remove occurrence` form | **A delete control per row**, on the file or image itself. |
| `Retain document` upload form | **Gone.** The operator's reasoning is exact: the file is already stored, so "retain" describes nothing a person does. |
| Confirm third-party vehicle evidence | **Gone as a control.** Semantic role is not operator-configurable. |

## The notes rule

Document changes are recorded as **case notes, created by the system, in the
same shape as the other automatic notes** — not as a revision column on the
Evidence tab. History stops being a thing the operator reads out of a ledger and
becomes a thing that appears in the record's own timeline, beside every other
automatic note.

This is what replaces the `Revision state` column and the historical-revision
question: nothing becomes unreachable, because every change writes a note.

**To establish during planning:** the existing automatic-note mechanism and its
actor — the new notes must be written by the same path and look the same, not a
second note-writing implementation.

## The design authority must be amended in the same change

`docs/design/README.md` carries a binding row requiring
*"Original/source/version/logical removal/closed lock; Box/external state"* on
the evidence/document panel. The operator's instruction removes three of those
from the panel and moves the history to notes. Operator truth outranks the
design authority, but the row has to be rewritten in this ticket or the change
contradicts a governing document.

## Sequencing

Lands before [[DOCS-011]] — this decides which surface survives, and DOCS-011's
preview trigger sits on a row inside it. [[CASE-022]] owns the upload-request
section of the same file and is separately blocked on whether INT-31 is
delivered at all.
