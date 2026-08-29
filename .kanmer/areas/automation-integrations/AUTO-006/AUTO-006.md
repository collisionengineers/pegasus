---
id: AUTO-006
type: ticket
title: Redesign the Automation & AI administration area
status: preparing
area: automation-integrations
assignee: claude-auto-006
profile: feature
taken_at: '2026-08-29T09:28:43.736Z'
branch: task/auto-006-automation-admin
worktree: 'C:/Users/PC/Documents/GitHub/pegasus-worktrees/auto-006-automation-admin'
labels:
  - ui
  - automation
  - operator-requested
groups:
  - EPIC-008
  - EPIC-011
links:
  - AUTO-007
docs_todo: true
archived: false
created: '2026-08-21T13:19:14.422Z'
updated: '2026-08-29T09:28:43.736Z'
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
