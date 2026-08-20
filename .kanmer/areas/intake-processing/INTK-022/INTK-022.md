---
id: INTK-022
type: ticket
title: 'Queues: one Not-ready table, dropdown filters, sortable newest-first columns'
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels:
  - ui
  - design
  - operator-reported
  - queues
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-03-triage.md
archived: false
created: '2026-08-20T18:44:30.327Z'
updated: '2026-08-20T18:44:30.327Z'
---

## Why

Operator, 2026-08-20 (feedback round 2): the Queues Not-ready view stacks two tables (Instruction-initiated + Image-initiated) under a pill row; filters should be dropdowns, there should be one table, and tables should sort newest-first with clickable column sorting.

## What

- Not ready renders **one** merged table (Reference · Registration · Claimant · Principal · Status · Received · Chase), image-initiated rows showing "—" where a field does not apply; each reference links to its own details page.
- The origin pills become a **dropdown** (All / Awaiting images / Awaiting instructions — the two INTK-008 origins in operator words) plus a **Principal dropdown**, auto-submitting on change with a no-script Apply button.
- Case tables default newest-first; column headers are sort links toggling direction (`?sort=`), carried by the search query for Review/Held and applied to the merged Not-ready rows.
- Triage and Unidentified tabs keep their existing orderings (already deliberate); badge counts untouched.

## How to verify

/Triage shows one Not-ready table mixing both origins newest-first; dropdown filters narrow it; header links flip direction; TriageQueuesWebTests stays green with the new surface.

## Outcome
