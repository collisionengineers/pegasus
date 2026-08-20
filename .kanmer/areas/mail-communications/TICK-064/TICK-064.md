---
id: TICK-064
type: ticket
title: >-
  MAIL-23 — Map the detailed taxonomy to operational queues and designated
  Outlook folders
status: implementing
area: mail-communications
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:49.947Z'
taken_at: '2026-08-20T09:54:49.769Z'
branch: task/tick-064-mail-23-folder-policy
worktree: ../pegasus-worktrees/tick-064
labels:
  - capability
  - MAIL-23
  - next
  - post-alpha
  - blocked
  - requires-live-approval
groups:
  - EPIC-003
  - EPIC-006
links:
  - TICK-044
blocks:
  - TICK-047
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-12T15:05:19.554Z'
updated: '2026-08-20T09:54:49.769Z'
---

## What

Plan and research **MAIL-23**: Map the detailed taxonomy to operational queues and designated Outlook folders

## Why

This is allocated to **Next / 0.3.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — MAIL-23.
- Blocked by: [[TICK-044]] — Operational queue and folder mapping follows the detailed classification mapping.
