---
id: PLAT-049
type: ticket
title: 'Operations: AI Job List, Service health and Send Unidentified to AI'
status: review
area: platform-operations
assignee: claude-plat-049
profile: feature
stageEntered:
  implementing: '2026-08-29T09:31:38.080Z'
  review: '2026-08-29T09:51:51.642Z'
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
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
commits:
  - 3ddc77d51b37efd6253b1c7d9a140167d9069018
  - 41c1b4ea5a7e9345dfdb5266f632d68fa77484d5
  - d393ecd547aa79f4e5fe55b3434462ad8c4e2ffd
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/617'
archived: false
created: '2026-08-28T08:35:24.068Z'
updated: '2026-08-29T09:52:03.383Z'
---

## What

Wave 4 of [[EPIC-011]]. Second pass on `Pages/Operations/**` after [[PLAT-023]]: AI Job List panel (kind + detail, record, started by, created, state chip, actions Review estimate / Open query / Review / Complete job / Cancel), "Send Unidentified to AI" creating an UnidentifiedResolution job, Service health table with Retry/View, EVA handoffs panel from submission records.

## Owns

`src/Pegasus.Web/Pages/Operations/**`, `tests/Pegasus.IntegrationTests/OperationsWebTests.cs`.

## Blocked by

[[PLAT-023]], the AI job ledger ticket, the service health ticket.
