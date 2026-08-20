---
id: INTK-013
type: ticket
title: Make the Not ready tab count match its rows across both case origins
status: preparing
area: intake-processing
assignee: claude-code
profile: fix
taken_at: '2026-08-20T04:58:55.284Z'
branch: task/intk-013-notready-badge
worktree: ../pegasus-worktrees/intk-013
labels:
  - defect
  - queues
  - operator-reported
links:
  - INTK-009
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-20T03:16:37.482Z'
updated: '2026-08-20T04:58:55.284Z'
---

## What

Operator, 2026-08-20, verbatim: *"There are two cases under 'Not Ready' - 1 instruction initiated, and 1 image initiated, but the Tab only says 1 case."*

Fix the Not ready tab badge/count so it equals the number of rows the tab actually lists — across Instruction-initiated and Image-initiated origins.

## Why

[[INTK-009]] added the origin filters; the count query and the list query have diverged (likely the badge counts only one origin). A queue whose badge disagrees with its rows is untrustworthy.

## Verification

- [ ] With one instruction-initiated and one image-initiated Not ready case, the tab shows 2 and the list shows both.
- [ ] Filters still show correct sub-counts.
- [ ] Regression test covering count == rows for mixed origins.
