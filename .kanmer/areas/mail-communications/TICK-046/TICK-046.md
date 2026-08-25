---
id: TICK-046
type: ticket
title: >-
  MAIL-04 — Explainable classification evidence, policy version, and correction
  history
status: done
area: mail-communications
order: 150
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-18T15:21:25.839Z'
  review: '2026-08-19T10:52:46.993Z'
  verifying: '2026-08-19T11:24:00.818Z'
  done: '2026-08-20T01:29:42.695Z'
labels:
  - capability
  - MAIL-04
  - next
  - requires-live-approval
groups:
  - EPIC-006
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - db9e35fe
  - fe66e4bd
  - 581fee7f
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/418'
deployment: production
archived: false
created: '2026-08-12T15:03:53.708Z'
updated: '2026-08-25T06:46:04.589Z'
---

## What

Plan and research **MAIL-04**: Explainable classification evidence, policy version, and correction history

## Why

The capability inventory allocates this outcome to **Next / 0.3.0**. This capability is **not designated until post-alpha** (Next / 0.3.0). It is blocked from implementation until the activation evidence and decisions below are accepted.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MAIL-04.
- Canonical owner: [Owning FRD](docs/frd/frd-08-email-mailbox-and-background-processing.md#email-mailbox-and-background-processing)
- Activation/boundary: Allocation only; owning evidence still required.
