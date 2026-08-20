---
id: TICK-027
type: ticket
title: >-
  MCP-06 — Automation Actor assessment actions: direct writes with logging
  parity (assessment get/update, case-detail update, EVA…
status: review
area: automation-integrations
order: 20
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-17T13:24:20.304Z'
  implementing: '2026-08-20T04:06:27.660Z'
  review: '2026-08-20T04:06:29.725Z'
taken_at: '2026-08-20T03:55:11.805Z'
branch: task/tick-027-mcp06-update-details-tests
worktree: ../pegasus-worktrees/tick-027
labels:
  - capability
  - MCP-06
  - now
  - requires-live-approval
groups:
  - EPIC-005
  - HZN-003
links: []
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
archived: false
created: '2026-08-12T15:03:53.326Z'
updated: '2026-08-20T04:06:29.725Z'
---

## What

Plan and research **MCP-06**: Automation Actor assessment actions: direct writes with logging parity (assessment get/update, case-detail update, EVA bundle generate and status) through the same Core use cases and guards as the staff app

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MCP-06.
- Canonical owner: [Targeted sending and reviewed AI proposals](docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md#targeted-sending-and-reviewed-ai-proposals)
- Activation/boundary: Implemented behind the shared composition gate (ADR-0021): automation values land unconfirmed for review at manual engineer assignment, finding confirmation stays staff-Engineer-only, estimate derivation waits for EXT-09 formula authority, and no confirmation, report-approval, or outward-dispatch tool exists.
