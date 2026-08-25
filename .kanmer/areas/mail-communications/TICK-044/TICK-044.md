---
id: TICK-044
type: ticket
title: >-
  MAIL-02 — Map every detailed email classification to its operational
  destination or Needs sorting
status: done
area: mail-communications
order: 2200
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-18T15:21:29.366Z'
  review: '2026-08-19T08:36:43.063Z'
  verifying: '2026-08-19T09:03:23.751Z'
  done: '2026-08-20T01:29:41.489Z'
labels:
  - capability
  - MAIL-02
  - next
  - requires-live-approval
groups:
  - EPIC-003
  - EPIC-006
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 7af3f834
  - 702148f2
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/411'
deployment: production
archived: false
created: '2026-08-12T15:03:53.669Z'
updated: '2026-08-25T06:46:18.738Z'
---

## What

Plan and research **MAIL-02**: Map detailed email classifications to Receiving work, Query, Other, Needs sorting, or the separate Triage workflow

## Why

The capability inventory allocates this outcome to **Next / 0.3.0**. This capability is **not designated until post-alpha** (Next / 0.3.0). It is blocked from implementation until the activation evidence and decisions below are accepted.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MAIL-02.
- Canonical owner: [Owning FRD](docs/frd/frd-08-email-mailbox-and-background-processing.md#email-mailbox-and-background-processing)
- Activation/boundary: Allocation only; owning evidence still required.
