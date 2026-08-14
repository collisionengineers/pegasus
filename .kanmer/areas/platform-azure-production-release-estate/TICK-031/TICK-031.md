---
id: TICK-031
type: ticket
title: OPS-23 — Operator acceptance against the real end-to-end workflow
status: todo
area: platform-azure-production-release-estate
priority: medium
assignee: ''
labels:
  - capability
  - OPS-23
  - now
  - requires-live-approval
links:
  - TICK-001
archived: true
created: '2026-08-12T15:03:53.413Z'
updated: '2026-08-13T14:39:29.696Z'
---

## What

Plan and research **OPS-23**: Operator acceptance against the real end-to-end workflow

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — OPS-23.
- Canonical owner: [Requirements](requirements.md#operator-experience)
- Activation/boundary: Required before 0.1.0-alpha.1 acceptance.

## Migrated validation

This standalone proof/validation ticket was consolidated into [[TICK-001]]. Its pending checks and approval boundaries now live in that work ticket's `checklist.md`; actual results belong in the owner's `proof.md`. Archived rather than deleted to preserve history.
