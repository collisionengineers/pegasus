---
id: PLAT-021
type: ticket
title: >-
  Deduplicate application exceptions and page only for failed or persistent
  operations
status: review
area: platform-operations
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-08-21T09:58:24.302Z'
  review: '2026-08-21T10:25:50.023Z'
taken_at: '2026-08-21T10:00:08.703Z'
branch: task/plat-021-exception-alert
worktree: ../pegasus-worktrees/plat-021
labels:
  - production
  - monitoring
  - OPS-08
  - alert-quality
links:
  - MAIL-003
  - PLAT-013
  - CASE-005
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/adr/0002-dotnet-modular-monolith-on-azure.md
commits:
  - 32e6d932
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/497'
archived: false
created: '2026-08-21T09:57:55.118Z'
updated: '2026-08-21T10:25:50.023Z'
---

## Why

The current Sev1 rule pages for every AppExceptions row. Successful operations and duplicated telemetry create noise and obscure actionable failures.

## Acceptance

The infrastructure-owned rule deduplicates by operation and normalized signature, alerts for a failed recent request or persistence across distinct operations/minute buckets, preserves the existing action group, and historical replay distinguishes the permission incidents from a recovered deadlock.

## Outcome
