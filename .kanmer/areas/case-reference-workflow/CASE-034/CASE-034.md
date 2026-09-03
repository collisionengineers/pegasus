---
id: CASE-034
type: ticket
title: 'Cases queues: the Principal filter must apply to every queue (FRD-12)'
status: backlog
area: case-reference-workflow
order: 140
assignee: ''
profile: fix
labels:
  - ui
  - cases
  - wave-4
groups:
  - EPIC-011
links:
  - CASE-025
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-29T14:01:10.973Z'
updated: '2026-09-03T15:15:27.186Z'
---

## What

`docs/frd/frd-12-operator-experience.md:149` states:

> `?tab=` selects the queue. Filters are Principal (every queue) and, on Not
> ready, Missing.

On merged `dev` at `b92cb9a7`, `src/Pegasus.Web/Pages/Cases/Index.cshtml:37` carries
the comment *"Principal select exists only where Principal rows are listed"* and
gates the select accordingly. So the Principal filter is absent on the queues whose
rows are not Case rows — Triage, Unidentified and the image-initiated queue.

## Why

FRD-12 says *every* queue. An operator narrowing to one Principal loses that
narrowing when they switch tab, which is exactly the kind of inconsistency the
workspace redesign exists to remove.

There is a real question underneath, which is why this is a ticket rather than a
one-line change: a Triage or Unidentified row may have no Principal yet — that is
the point of those queues. So "filter by Principal" needs a defined meaning on a
queue whose rows can be unallocated.

Found during the [[CASE-025]] strict rule-14 audit, 2026-08-29.

## Approach

- Settle the semantics first and record it: on a queue whose rows may have no
  Principal, does the filter exclude unallocated rows, or does it offer an explicit
  "unallocated" choice? Check `operator-notes.md` and FRD-12 before deciding; if
  neither settles it, raise it as an open question rather than guessing.
- Then render the select on every queue with the settled behaviour, reusing the
  existing filtered-options query — do not add a second Principal lookup.
- Preserve the filter across `?tab=` changes.
- No explanatory copy on the control.

## Verification

- [ ] The Principal filter renders on every `?tab=` queue.
- [ ] Its meaning on rows without a Principal is defined, documented in the ticket,
      and matches the shipped behaviour.
- [ ] Switching tab preserves the selected Principal.
