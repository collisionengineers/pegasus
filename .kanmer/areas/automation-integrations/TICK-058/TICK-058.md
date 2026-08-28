---
id: TICK-058
type: ticket
title: API-01 — Principal-scoped provider submission API
status: review
area: automation-integrations
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-21T14:20:03.598Z'
  review: '2026-08-28T11:31:30.687Z'
  implementing: '2026-08-28T16:58:50.515Z'
taken_at: '2026-08-28T11:08:34.214Z'
branch: task/tick-058-provider-submission-api
worktree: ../pegasus-worktrees/tick-058-provider-submission-api
labels:
  - capability
  - API-01
  - now
  - requires-live-approval
  - wave-3
groups:
  - HZN-002
  - EPIC-009
  - EPIC-011
links:
  - TICK-061
  - DELIV-032
blocks:
  - TICK-060
refs:
  - docs/frd/frd-09-provider-and-intermediary-routes.md
commits:
  - e56bb469
  - a5af5fd9
  - b5b6e096
  - 2804ebb6
  - 387f5e26
  - f021095e
  - ae35c34d
  - c5011932
  - df978b43
  - afc0dc10
prs:
  - '594'
archived: false
created: '2026-08-12T15:05:19.421Z'
updated: '2026-08-28T21:58:56.442Z'
---

## What

Plan and research **API-01**: Principal-scoped provider submission API

## Why

This is allocated to **Next / 0.4.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — API-01.
