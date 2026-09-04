---
id: PLAT-069
type: ticket
title: >-
  Move Service health off Operations; Operations shows a partial-data notice
  linking to Administration
status: verifying
area: platform-operations
assignee: wf-build/plat-069
profile: fix
stageEntered:
  preparing: '2026-09-02T22:22:00.643Z'
  review: '2026-09-04T10:47:44.885Z'
  verifying: '2026-09-04T11:14:11.060Z'
taken_at: '2026-09-04T10:04:55.966Z'
branch: task/plat-069-operations-notice
worktree: .worktrees/plat-069
claim_expires_at: '2026-09-04T10:34:55.966Z'
claim_controller: wf-build/plat-069
lease_id: 992226f2-4279-4d02-9a63-60f27f263653
lease_revision: 1
lease_workspace: 'worktree:c:\users\pc\documents\github\pegasus\.worktrees\plat-069'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-04T10:04:55.966Z'
labels:
  - operations
  - ui
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - UIIMP-014
refs:
  - docs/frd/frd-12-operator-experience.md
prs:
  - '657'
archived: false
created: '2026-09-02T20:31:38.879Z'
updated: '2026-09-04T11:14:11.060Z'
---

## What

Remove the Service health table from `/Operations`; when any query is not current, administrators see a one-line notice with a link to Administration → Service health.

## Why

D37. Mockup source: `Pegasus_UI_v2_src/src/16-operations.js`.

## Approach

- Delete the panel from `Pages/Operations`; keep PLAT-051's admin area.

## Verification

- [ ] Snapshot states updated; no dead link.

## Outcome
