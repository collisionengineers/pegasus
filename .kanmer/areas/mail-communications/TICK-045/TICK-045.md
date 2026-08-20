---
id: TICK-045
type: ticket
title: MAIL-03 — One shared classification policy across all supported mailboxes
status: done
area: mail-communications
order: 40
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-18T15:21:22.108Z'
  review: '2026-08-19T11:34:21.837Z'
  verifying: '2026-08-19T21:45:27.373Z'
  done: '2026-08-20T01:29:42.035Z'
taken_at: '2026-08-19T13:10:13.120Z'
branch: task/tick-045-shared-classification-policy
worktree: ../pegasus-worktrees/tick-045-shared-classification-policy
labels:
  - capability
  - MAIL-03
  - next
  - post-alpha
  - blocked
  - requires-live-approval
groups:
  - EPIC-006
links:
  - TICK-035
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 139a4571c00cb7ee3a0ac1d39d8d9d2d41129a7e
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/422'
deployment: production
archived: false
created: '2026-08-12T15:03:53.689Z'
updated: '2026-08-20T01:29:42.035Z'
---

## What

Plan and research **MAIL-03**: One shared classification policy across all supported mailboxes

## Why

The capability inventory allocates this outcome to **Next / 0.3.0**. This capability is **not designated until post-alpha** (Next / 0.3.0). It is blocked from implementation until the activation evidence and decisions below are accepted.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MAIL-03.
- Canonical owner: [Owning FRD](docs/frd/frd-08-email-mailbox-and-background-processing.md#email-mailbox-and-background-processing)
- Activation/boundary: Allocation only; owning evidence still required.
