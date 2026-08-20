---
id: TICK-053
type: ticket
title: >-
  MAIL-11 — Browse, search, and view mailbox messages and conversation threads
  in the app, including read-only search of accepted D…
status: review
area: mail-communications
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:45.571Z'
  review: '2026-08-20T11:11:50.033Z'
taken_at: '2026-08-20T09:58:24.766Z'
branch: task/tick-053-mail-browse-search
worktree: ../pegasus-worktrees/tick-053
labels:
  - capability
  - MAIL-11
  - next
  - post-alpha
  - blocked
  - requires-live-approval
groups:
  - EPIC-003
  - EPIC-006
links: []
blocks:
  - TICK-056
  - TICK-057
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 2d7c1421fcc4ff864cd9e114c904a3181c3fb4b9
  - 72f55b8a551336ffd46b8536aafdc334c1854f26
  - 93c069579ca3437e7560336e6c5d53b59402790c
  - 347f5ce741e19e6973a31655cd433f5c452005b0
  - 8b300043182ab14e8716323f6fa6f800bc2ba782
  - c0fa9a9905f2808ec1e2eb03e42dbe29cfde7ae4
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/469'
archived: false
created: '2026-08-12T15:05:19.296Z'
updated: '2026-08-20T12:18:29.933Z'
---

## What

Implement **MAIL-11**: Browse, search, and view mailbox messages and conversation threads in the app, including read-only search of accepted Deleted Items.

## Why

This remains allocated to **Next / 0.3.0** in `docs/capabilities.md`. On 2026-08-20 the operator instructed “Implement the plan”, activating the planned local browse/search UI. That instruction does not authorize deployment, Graph permission changes, live mailbox writes, or claim live-mailbox/manual visual acceptance.

## Approach

- Reuse the existing retained-mail query/read model and canonical intake reader projection.
- Keep Deleted Items as an explicit bounded GET-only read of exact approved mailbox scopes, with no persistence, backfill, reconstruction, or mailbox mutation.
- Keep production composition, failure behavior, exact attachment identity, and acceptance evidence explicit.

## Verification

- [x] A task-level plan covers the capability's exact contract and tests.
- [x] The local implementation activation is recorded in canonical design/capability owners.
- [x] Review blockers [[PR-015]], [[PR-016]], [[PR-017]], [[PR-018]], [[PR-019]], [[PR-020]], [[PR-021]], and [[PR-022]] have implementations and PIRs in Review.

## Notes

- Source: `docs/capabilities.md` — MAIL-11.
- Deployment and live-mailbox evidence remain separately allocated.
