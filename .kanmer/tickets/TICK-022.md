---
id: TICK-022
type: ticket
title: >-
  EXT-03 — Operator-approved deterministic UTF-8 EVA handoff with the exact
  ordered 13-key JSON, every eligible custody-confirmed…
status: todo
area: engineering-eva-export-handoff
priority: medium
assignee: ''
labels:
  - capability
  - EXT-03
  - now
  - requires-live-approval
links: []
archived: false
created: '2026-08-12T15:03:53.205Z'
updated: '2026-08-12T15:03:53.205Z'
---

## What

Plan and research **EXT-03**: Operator-approved deterministic UTF-8 EVA handoff with the exact ordered 13-key JSON, every eligible custody-confirmed Case-vehicle image, and a SHA-256 manifest; no EVA network call or Pegasus-owned image ordering

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — EXT-03.
- Canonical owner: [Focused EVA manual handoff](requirements.md#focused-eva-manual-handoff)
- Activation/boundary: Core policy plus authenticated Case and composition-gated Automation callers are proved locally. ZIP/drag-drop container acceptance, deployment and operator acceptance remain pending; a future EVA API remains a separate contract.
