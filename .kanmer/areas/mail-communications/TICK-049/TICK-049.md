---
id: TICK-049
type: ticket
title: MAIL-07 — Move the confirmed message to the designated Outlook folder
status: review
area: mail-communications
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:37.436Z'
  review: '2026-08-20T14:53:19.600Z'
taken_at: '2026-08-20T14:16:53.138Z'
branch: task/tick-049-mail-07-confirmed-folder-move
worktree: ../pegasus-worktrees/tick-049
labels:
  - capability
  - MAIL-07
  - next
  - post-alpha
  - blocked
  - requires-live-approval
groups:
  - EPIC-003
  - EPIC-006
links:
  - TICK-048
blocks:
  - TICK-050
  - TICK-054
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 8b1e6d74
  - f60248af4a078c1fa188a46143818d2cce2683c9
  - 5e8217a1d3f23caf7a137b24cdc79366175c35c8
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/477'
archived: false
created: '2026-08-12T15:05:19.217Z'
updated: '2026-08-20T14:55:15.351Z'
---

## What

Plan and research **MAIL-07**: Move the confirmed message to the designated Outlook folder

## Why

This is allocated to **Next / 0.3.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — MAIL-07.
- Blocked by: [[TICK-048]] — A folder move may occur only after staff confirmation.
