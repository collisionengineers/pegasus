---
id: PLAT-054
type: ticket
title: >-
  Expose OperationsSnapshot.OfficeBoundaries (Europe/London day) for the Reports
  page — one conversion owner
status: implementing
area: platform-operations
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-28T21:27:18.167Z'
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
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-28T10:58:44.482Z'
updated: '2026-08-28T21:27:18.167Z'
---

## What

`GetOperationsSnapshot.OfficeBoundaries` (`Core/Operations/OperationsSnapshot.cs`) is the only place that turns "the office's day" (Europe/London, Monday-start week, UTC fallback when the zone is missing) into UTC instants, and it is private. The Administration Reports page ([[PLAT-051]]) must convert its From/To dates to the half-open UTC period `GetEngineerActivityReport` takes ([[PLAT-048]]); without a shared owner it will grow a second conversion. Lift the office-day conversion into one public Core owner (e.g. `OfficeCalendar` beside `LondonCalendar` if that is the right home — search first) and make `GetOperationsSnapshot` and the Reports page both call it. Behaviour-preserving for the dashboard.

## Owns

`src/Pegasus.Core/Operations/OperationsSnapshot.cs` (extract), the new owner file, Core tests for the boundary.

Raised in the [[PLAT-048]] review (2026-08-28); blocks the Reports page conversion in [[PLAT-051]].
