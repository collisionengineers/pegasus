---
id: AUTO-006
type: ticket
title: Redesign the Automation & AI administration area
status: done
area: automation-integrations
order: 2210
assignee: claude-auto-006
profile: feature
stageEntered:
  implementing: '2026-08-29T09:31:06.707Z'
  review: '2026-08-29T09:56:08.347Z'
  verifying: '2026-08-29T17:19:13.635Z'
  done: '2026-08-29T17:19:25.745Z'
labels:
  - ui
  - automation
  - operator-requested
groups:
  - EPIC-008
  - EPIC-011
links:
  - AUTO-007
  - AUTO-010
  - PLAT-051
docs_todo: true
commits:
  - 62c9e2ace7598bf9de2385e7b2e5705cfd4a8288
  - ef905e6af364bddc8caa34416f9f54281b3e0b12
  - eb41188b0c48aa9a64547412bb833f389c11bb2b
  - b4d0f88a21656a839246d1a12eb1290f4c794562
  - 5dd27a278df9a4336d7b5b66a231094ce3a54240
  - 7e5cf00c4bbb699540eddbc8e0f6e146f83c0b31
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/618'
archived: false
created: '2026-08-21T13:19:14.422Z'
updated: '2026-09-01T14:44:33.819Z'
---

## What

Redesign the Automation workspace.

## Why

Automation needs a clear operator-facing experience in the requested redesign programme.

## Approach

- Research existing automation functions and design constraints.
- Create linked follow-ups for backend features or process changes required by the redesign.

## Verification

- [ ] The approved redesign supports the required automation workflows.

## Outcome

## Inherited scope from [[PLAT-015]]

The Automation redesign also owns the existing Activity-page copy defect:

- Resolve a raw `AggregateId` to the available Case or PO reference, or omit it when no supported business reference exists.
- Remove the “you can filter by” narration.

Verification for this inherited scope: the Activity table contains business references rather than raw aggregate identifiers and carries no filter instructions.
