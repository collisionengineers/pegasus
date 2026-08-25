---
id: TICK-050
type: ticket
title: MAIL-08 — Suggested next actions for classified email
status: done
area: mail-communications
order: 2210
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:44.019Z'
  review: '2026-08-20T18:06:36.026Z'
  verifying: '2026-08-20T18:57:20.790Z'
  done: '2026-08-21T15:11:34.082Z'
taken_at: '2026-08-20T17:53:50.130Z'
branch: task/tick-050-mail-08-suggested-next-action
worktree: ../pegasus-worktrees/tick-050
labels:
  - capability
  - MAIL-08
  - next
  - post-alpha
  - blocked
  - requires-live-approval
groups:
  - EPIC-003
  - EPIC-006
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 75c9f3a0576b73c722c03b6e1a71b39205711602
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/480'
deployment: production
archived: false
created: '2026-08-12T15:05:19.237Z'
updated: '2026-08-25T01:27:00.978Z'
---

## What

Plan and research **MAIL-08**: Suggested next actions for classified email

## Why

This is allocated to **Next / 0.3.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — MAIL-08.
