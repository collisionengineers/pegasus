---
id: TICK-029
type: ticket
title: OPS-14 — Production cutover and previous-artifact rollback procedure
status: done
area: platform-operations
order: 1750
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-20T05:38:47.790Z'
  implementing: '2026-08-20T05:41:31.436Z'
  review: '2026-08-20T05:41:35.380Z'
  verifying: '2026-08-20T05:41:37.513Z'
  done: '2026-08-20T05:41:45.948Z'
labels:
  - capability
  - OPS-14
  - now
  - requires-live-approval
groups:
  - HZN-003
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
prs:
  - '475'
deployment: n/a
archived: false
created: '2026-08-12T15:03:53.369Z'
updated: '2026-08-26T14:34:45.831Z'
---

## What

Plan and research **OPS-14**: Production cutover and previous-artifact rollback procedure

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — OPS-14.
- Canonical owner: [Owning FRD](docs/frd/frd-12-operator-experience.md#operator-experience)
- Activation/boundary: 0.1.0-alpha.1 gate; implementation/recovery detail remains open.
