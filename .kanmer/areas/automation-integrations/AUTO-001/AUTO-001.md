---
id: AUTO-001
type: ticket
title: Activate the Pegasus Automation MCP gate
status: backlog
area: automation-integrations
assignee: ''
profile: feature
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
updated: '2026-08-18T08:49:54.851Z'
---

## Why

Activate the existing Pegasus Web Automation Actor MCP composition gate only after recording the exact target, approval, credential custody, transport, scope, rate-limit, rollback, and external-caller evidence required for live use.

## Verification

- The approved target and activation authority are recorded before configuration changes.
- The named Automation Actor obtains a scoped token and reaches the protected `/mcp` endpoint.
- The approved 15-tool inventory is exercised with success, denial, and permanent-history evidence.
- The Administrator kill switch and rollback return the ingress to its closed state.

## Outcome
