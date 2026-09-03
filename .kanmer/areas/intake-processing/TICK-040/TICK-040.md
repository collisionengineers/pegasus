---
id: TICK-040
type: ticket
title: INT-15 — Automated MSG extraction
status: done
area: intake-processing
order: 1880
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-20T04:15:21.968Z'
  implementing: '2026-08-20T04:19:45.446Z'
  review: '2026-08-20T04:20:11.459Z'
  verifying: '2026-08-20T04:46:40.157Z'
  done: '2026-08-20T12:48:12.245Z'
labels:
  - capability
  - INT-15
  - next
links:
  - SIMPLI-013
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
prs:
  - '449'
deployment: production
archived: false
created: '2026-08-12T15:03:53.590Z'
updated: '2026-09-03T09:06:52.774Z'
---

## What

Plan and research **INT-15**: Automated MSG extraction

## Why

The capability inventory allocates this outcome to **Next / 0.2.0**. This capability is **not designated until post-alpha** (Next / 0.2.0). It is blocked from implementation until the activation evidence and decisions below are accepted.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — INT-15.
- Canonical owner: [Owning FRD](docs/frd/frd-02-intake-and-source-identity.md#intake-and-source-identity)
- Activation/boundary: Allocation only; owning evidence still required.
