---
id: PLAT-054
type: ticket
title: >-
  Expose OperationsSnapshot.OfficeBoundaries (Europe/London day) for the Reports
  page — one conversion owner
status: review
area: platform-operations
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-28T21:27:18.167Z'
  review: '2026-08-28T21:28:07.500Z'
taken_at: '2026-08-28T21:26:52.093Z'
branch: task/plat-054-office-boundaries
worktree: 'C:/Users/PC/Documents/GitHub/pegasus-worktrees/plat-054-office-boundaries'
labels:
  - backend
  - reports
groups:
  - EPIC-011
links:
  - PLAT-048
  - PLAT-060
refs:
  - docs/frd/frd-12-operator-experience.md
commits:
  - c2bef9df25acca4c5ec7224ae6bb637e57f089da
  - 3e16e506c753324598ba75de1022fb6ccb3f2817
  - 44bcb8c0f622e5f169bc0eb43d7271f1632b7d8d
  - 1e15e8325cc74ccfb5d8f059b1a5a17c20e98aad
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/611'
archived: false
created: '2026-08-28T10:58:44.482Z'
updated: '2026-08-29T13:12:40.147Z'
---

## What

`GetOperationsSnapshot.OfficeBoundaries` (`Core/Operations/OperationsSnapshot.cs`) is the only place that turns "the office's day" (Europe/London, Monday-start week, UTC fallback when the zone is missing) into UTC instants, and it is private. The Administration Reports page ([[PLAT-051]]) must convert its From/To dates to the half-open UTC period `GetEngineerActivityReport` takes ([[PLAT-048]]); without a shared owner it will grow a second conversion. Lift the office-day conversion into one public Core owner (e.g. `OfficeCalendar` beside `LondonCalendar` if that is the right home — search first) and make `GetOperationsSnapshot` and the Reports page both call it. Behaviour-preserving for the dashboard.

## Owns

`src/Pegasus.Core/Operations/OperationsSnapshot.cs` (extract), the new owner file, Core tests for the boundary.

Raised in the [[PLAT-048]] review (2026-08-28); blocks the Reports page conversion in [[PLAT-051]].
