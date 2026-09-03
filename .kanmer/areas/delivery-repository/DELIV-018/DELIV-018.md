---
id: DELIV-018
type: ticket
title: Correct the capability inventory summary arithmetic
status: backlog
area: delivery-repository
order: 200
assignee: ''
profile: chore
labels:
  - documentation
  - board-groom-follow-up
links: []
archived: false
created: '2026-08-25T06:35:41.656Z'
updated: '2026-09-03T15:15:27.306Z'
---

## What

Correct the allocation-summary counts in `docs/capabilities.md` so they equal the 231 capability rows.

## Why

The table currently derives to 133 Now, 29 Next, 40 Later, and 29 Not planned; release targets derive to 133 at `0.1.0-alpha.1` and 12 at `1.0.0`. The prose summary instead says 132/29/41/29 and 132 alpha/13 at 1.0.0. This is arithmetic drift, not a product or scheduling decision.

## Approach

- Recompute the table counts mechanically.
- Change only the summary numbers that disagree.
- Keep every capability row, allocation, owner, and note unchanged.

## Verification

- [ ] A mechanical recount equals every displayed horizon and target-release total and still totals 231 unique IDs.

## Outcome
