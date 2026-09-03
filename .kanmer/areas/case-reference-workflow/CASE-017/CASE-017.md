---
id: CASE-017
type: ticket
title: 'Case History becomes Notes, and operators can add their own'
status: done
area: case-reference-workflow
order: 980
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-22T00:48:37.254Z'
  implementing: '2026-08-22T00:48:40.065Z'
  review: '2026-08-22T00:51:13.165Z'
  verifying: '2026-08-22T04:36:10.651Z'
  done: '2026-08-22T07:18:01.626Z'
labels:
  - qdos26009
  - operator-requested
  - ui
links: []
docs_todo: true
deployment: production
archived: false
created: '2026-08-21T23:30:28.020Z'
updated: '2026-09-03T09:06:47.253Z'
---

## Why — operator direction (2026-08-22)

> "History tab should be a 'Notes' tab. Users can also add notes. These go in alongside any 'System' messages such as the DVLA lookup (or any pegasus action that adds details to a case)"

## What this is

Today the History tab is a read-only record of system actions (`Cases/Details.cshtml:142`, `_CaseHistory.cshtml`). The operator wants one **Notes** timeline carrying both:

- **system** entries — what Pegasus did to the case (the DVLA/DVSA lookup, custody, allocation, lifecycle changes);
- **operator** entries — free text a staff member writes.

Each entry needs its author distinguishable at a glance, so a system entry is never mistaken for a colleague's note or the reverse.

## Care required

Case history is permanent action history and is **append-only** — an operator note must not become a way to edit or reinterpret the record. Adding a note is itself a material action and belongs in that record.

The existing history rows keep their meaning; this adds a second kind of entry to the same timeline rather than replacing it.

The word "Immutable" currently heading this tab is removed under its own ticket.

## How to verify

The tab reads **Notes**; system entries and operator notes appear in one timeline, ordered, each attributed; an operator can add a note; notes cannot be edited or deleted afterwards.
