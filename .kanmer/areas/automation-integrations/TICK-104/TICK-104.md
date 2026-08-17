---
id: TICK-104
type: ticket
title: >-
  MCP-07 — Administration-configurable Send to AI channel connector setup: base
  URL, token entry/rotation, and timeout configured…
status: backlog
area: automation-integrations
assignee: ''
profile: feature
labels:
  - capability
  - MCP-07
  - later
  - post-alpha
  - blocked
  - requires-live-approval
groups:
  - EPIC-005
links:
  - TICK-102
archived: false
created: '2026-08-12T15:06:02.806Z'
updated: '2026-08-17T06:41:52.644Z'
---

## What

Plan and research **MCP-07**: Administration-configurable Send to AI channel connector setup: base URL, token entry/rotation, and timeout configured from Administration, with connector health/status display, replacing the current configuration/user-secrets-only setup

## Why

This is allocated to **Later / 1.3.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — MCP-07.
- Blocked by: [[TICK-102]] — Channel administration extends the existing Send-to-AI work-request boundary.
