---
id: PLAT-020
type: ticket
title: >-
  Grant production runtime roles their required vehicle-lookup and image-custody
  writes
status: verifying
area: platform-operations
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-08-21T09:58:24.209Z'
  review: '2026-08-21T10:25:49.981Z'
  verifying: '2026-08-21T14:18:35.242Z'
taken_at: '2026-08-21T10:00:08.531Z'
branch: task/plat-020-runtime-grants
worktree: ../pegasus-worktrees/plat-020
labels:
  - defect
  - production
  - azure-sql
  - least-privilege
  - worker
links:
  - CASE-008
  - INTK-008
  - INTK-014
refs:
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/adr/0007-direct-terminal-azure-deployment.md
commits:
  - 02a9a61b
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/496'
archived: false
created: '2026-08-21T09:57:53.143Z'
updated: '2026-08-21T14:18:35.242Z'
---

## Why

Production runtime roles lack permissions required by deployed callers: Worker cannot insert automatic vehicle-lookup requests, and Web/Worker cannot update image-intake lifecycle state. Due lookups and one custody item remain stranded.

## Acceptance

An append-only migration restores only the required grants, duplicate-key handling remains idempotent without hiding other persistence failures, role-backed tests cover the real callers, and approved production recovery reaches truthful terminal state with no recurring permission failures.

## Outcome
