---
id: TICK-062
type: ticket
title: MCP-05 — Automation Actor actions for the broader classified-email workspace
status: done
area: automation-integrations
order: 1900
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:49.381Z'
  review: '2026-08-20T03:53:31.538Z'
  verifying: '2026-08-20T04:20:34.740Z'
  done: '2026-08-20T12:48:16.044Z'
labels:
  - capability
  - MCP-05
  - next
  - requires-live-approval
groups:
  - EPIC-005
  - EPIC-006
links:
  - TICK-056
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
prs:
  - '441'
deployment: production
archived: false
created: '2026-08-12T15:05:19.507Z'
updated: '2026-09-01T14:44:33.500Z'
---

## What

Plan and research **MCP-05**: Automation Actor actions for the broader classified-email workspace

## Why

This is allocated to **Next / 0.3.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — MCP-05.
- Blocked by: [[TICK-056]] — The broader classified-email actor work follows the email-management workspace.
