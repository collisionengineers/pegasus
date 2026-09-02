---
id: PLAT-049
type: ticket
title: 'Operations: AI Job List, Service health and Send Unidentified to AI'
status: verifying
area: platform-operations
order: 310
assignee: claude-plat-049
profile: feature
stageEntered:
  implementing: '2026-08-29T09:31:38.080Z'
  review: '2026-08-29T09:51:51.642Z'
  verifying: '2026-08-29T18:03:26.282Z'
taken_at: '2026-08-29T09:27:42.833Z'
branch: task/plat-049-operations-features
worktree: 'C:/Users/PC/Documents/GitHub/pegasus-worktrees/plat-049-operations-features'
labels:
  - ui
  - wave-4
  - operations
groups:
  - EPIC-011
links: []
blocks: []
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
commits:
  - 3ddc77d51b37efd6253b1c7d9a140167d9069018
  - 41c1b4ea5a7e9345dfdb5266f632d68fa77484d5
  - d393ecd547aa79f4e5fe55b3434462ad8c4e2ffd
  - 7df757982d2a3cd324d5c9d89f602ce3c25b3cef
  - 3d5cdbb98a11a947bff6eae109353237fd850039
  - aed0aa0a
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/617'
deployment: production
archived: false
created: '2026-08-28T08:35:24.068Z'
updated: '2026-09-01T14:46:06.793Z'
---

## What

Wave 4 of [[EPIC-011]]. Second pass on `Pages/Operations/**` after [[PLAT-023]]: AI Job List panel (kind + detail, record, started by, created, state chip, actions Review estimate / Open query / Review / Complete job / Cancel), "Send Unidentified to AI" creating an UnidentifiedResolution job, Service health table with Retry/View, EVA handoffs panel from submission records.

## Owns

`src/Pegasus.Web/Pages/Operations/**`, `tests/Pegasus.IntegrationTests/OperationsWebTests.cs`.

## Blocked by

[[PLAT-023]], the AI job ledger ticket, the service health ticket.
