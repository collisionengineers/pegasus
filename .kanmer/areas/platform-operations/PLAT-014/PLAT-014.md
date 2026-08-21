---
id: PLAT-014
type: ticket
title: Correct missing LocalDB detection in Offline lifecycle
status: done
area: platform-operations
order: 20
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-08-20T09:02:33.715Z'
  review: '2026-08-20T11:33:51.724Z'
  verifying: '2026-08-20T11:54:13.473Z'
  done: '2026-08-21T15:13:21.920Z'
taken_at: '2026-08-20T10:41:05.974Z'
branch: task/plat-014-localdb-detection
worktree: ../pegasus-worktrees/plat-014
labels:
  - local-development
  - offline
  - windows
links:
  - PLAT-005
blocks:
  - PLAT-005
commits:
  - 6cb9c59a761909a5e926452a2684af0438559cb9
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/471'
deployment: n/a
archived: false
created: '2026-08-20T08:56:51.802Z'
updated: '2026-08-21T15:13:21.920Z'
---

## Why

PLAT-005 cannot start a supported Windows Offline run. On Microsoft SQL Server LocalDB 2025, `sqllocaldb info PegasusDevelopment_<run-id>` prints that the instance does not exist but exits with code 0. The lifecycle currently interprets any zero exit code without a `State:` line as an existing/unknown database, then refuses creation as unowned.

## Scope

Normalize that known absence response to `Missing` while preserving fail-closed treatment for genuinely unknown or pre-existing instances. Add focused coverage for the detection contract and verify an owned Offline run can create and clean up its exact database without acting on unrelated instances.

## Verification

- A missing LocalDB instance is recognized as missing even when the command exits 0.
- A real unknown/pre-existing instance remains protected by the ownership guard.
- The supported Offline Start → Status → Smoke → Reset lifecycle succeeds for one exact run on Windows.
- [[PLAT-005]] can resume its visual capture using the supported lifecycle.

## Outcome
