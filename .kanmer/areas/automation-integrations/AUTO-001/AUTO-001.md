---
id: AUTO-001
type: ticket
title: Activate the Pegasus Automation MCP gate
status: done
area: automation-integrations
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-18T09:45:52.837Z'
  review: '2026-08-18T11:17:41.883Z'
  verifying: '2026-08-18T11:34:59.459Z'
  done: '2026-08-18T12:22:59.396Z'
taken_at: '2026-08-18T11:12:31.011Z'
branch: task/auto-001-activate-mcp-gate
worktree: ../pegasus-worktrees/auto-001-activate-mcp-gate
labels:
  - now
  - requires-live-approval
  - MCP
links:
  - TICK-027
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/adr/0021-automation-actor-direct-write-assessment-contract.md
archived: false
created: '2026-08-18T08:49:54.851Z'
updated: '2026-08-18T12:22:59.396Z'
---

## Why

Activate the existing Pegasus Web Automation Actor MCP composition gate only after recording the exact target, approval, credential custody, transport, scope, rate-limit, rollback, and external-caller evidence required for live use.

## Verification

- The approved target and activation authority are recorded before configuration changes.
- The named Automation Actor obtains a scoped token and reaches the protected `/mcp` endpoint.
- The approved 15-tool inventory is exercised with success, denial, and permanent-history evidence.
- The Administrator kill switch and rollback return the ingress to its closed state.

## Outcome
