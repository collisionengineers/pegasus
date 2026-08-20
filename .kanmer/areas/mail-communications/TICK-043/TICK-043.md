---
id: TICK-043
type: ticket
title: >-
  MAIL-01 — Identify every inbound mailbox item and its mailbox/thread/message
  identity
status: done
area: mail-communications
order: 10
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-18T15:21:16.581Z'
  review: '2026-08-19T09:25:07.280Z'
  verifying: '2026-08-19T10:34:21.101Z'
  done: '2026-08-20T01:29:40.942Z'
taken_at: '2026-08-19T09:04:22.276Z'
branch: task/tick-043-mailbox-identity
worktree: ../pegasus-worktrees/tick-043-mailbox-identity
labels:
  - capability
  - MAIL-01
  - next
  - post-alpha
  - blocked
  - requires-live-approval
groups:
  - EPIC-006
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - a795877bd6fcceaffa74c8f9de0959c18a792f1b
  - 195bbf62
  - 09dbeb6a
  - ee947826
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/414'
deployment: production
archived: false
created: '2026-08-12T15:03:53.649Z'
updated: '2026-08-20T01:29:40.942Z'
---

## What

Plan and research **MAIL-01**: Identify every inbound mailbox item and its mailbox/thread/message identity

## Why

The capability inventory allocates this outcome to **Next / 0.3.0**. This capability is **not designated until post-alpha** (Next / 0.3.0). It is blocked from implementation until the activation evidence and decisions below are accepted.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MAIL-01.
- Canonical owner: [Owning FRD](docs/frd/frd-08-email-mailbox-and-background-processing.md#email-mailbox-and-background-processing)
- Activation/boundary: Allocation only; owning evidence still required.
