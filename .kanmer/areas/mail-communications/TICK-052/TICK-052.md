---
id: TICK-052
type: ticket
title: 'MAIL-10 — Manual email/case association, unlink, relink, and correction'
status: done
area: mail-communications
order: 2230
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:45.021Z'
  review: '2026-08-20T20:53:13.536Z'
  verifying: '2026-08-20T22:05:21.309Z'
  done: '2026-08-21T15:08:54.873Z'
taken_at: '2026-08-20T20:31:22.084Z'
branch: task/tick-052-mail-10-manual-case-association
worktree: 'C:/Users/Alex/Documents/GitHub/pegasus-worktrees/tick-052'
labels:
  - capability
  - MAIL-10
  - next
  - post-alpha
  - requires-live-approval
groups:
  - EPIC-003
  - EPIC-006
links: []
blocks: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - d4c951f5
  - 6b7c62a4
  - 563bb2ec
prs:
  - '490'
deployment: production
archived: false
created: '2026-08-12T15:05:19.275Z'
updated: '2026-08-25T01:27:00.990Z'
---

## What

Plan and research **MAIL-10**: Manual email/case association, unlink, relink, and correction

## Why

This is allocated to **Next / 0.3.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — MAIL-10.
