---
id: TICK-047
type: ticket
title: MAIL-05 — Recommend the designated Outlook folder for a classified message
status: implementing
area: mail-communications
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:36.697Z'
taken_at: '2026-08-20T11:44:11.993Z'
branch: task/tick-047-mail-05-folder-recommendation
worktree: ../pegasus-worktrees/tick-047
labels:
  - capability
  - MAIL-05
  - next
  - post-alpha
  - blocked
  - requires-live-approval
groups:
  - EPIC-003
  - EPIC-006
links: []
blocks:
  - TICK-050
  - TICK-049
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-12T15:05:19.177Z'
updated: '2026-08-20T11:44:11.993Z'
---

## What

Plan and research **MAIL-05**: Recommend the designated Outlook folder for a classified message

## Why

This is allocated to **Next / 0.3.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — MAIL-05.
