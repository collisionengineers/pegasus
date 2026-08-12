---
id: TICK-001
type: ticket
title: OPS-10 — Production environment deployed directly from an authorised terminal
status: todo
area: platform-azure-production-release-estate
priority: medium
assignee: ''
labels:
  - capability
  - OPS-10
  - now
  - requires-live-approval
links: []
archived: false
created: '2026-08-12T15:03:52.764Z'
updated: '2026-08-12T15:03:52.764Z'
---

## What

Plan and research **OPS-10**: Production environment deployed directly from an authorised terminal

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — OPS-10.
- Canonical owner: [ADR-0014](adr/0014-local-to-production-deployment.md)
- Activation/boundary: Executed for releases 1–3 ([operations — production environment](operations.md#production-environment) owns the evidence); operator acceptance outstanding.
