---
id: TICK-014
type: ticket
title: MAIL-16 — Automatically match the exact report Sent item to its case
status: done
area: mail-communications
order: 2090
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-20T05:43:44.941Z'
  implementing: '2026-08-20T05:44:57.109Z'
  review: '2026-08-20T05:45:12.375Z'
  verifying: '2026-08-20T05:45:31.530Z'
  done: '2026-08-20T05:46:26.659Z'
labels:
  - capability
  - MAIL-16
  - now
  - requires-live-approval
groups:
  - HZN-003
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
deployment: production
archived: false
created: '2026-08-12T15:03:53.047Z'
updated: '2026-08-25T01:27:00.910Z'
---

## What

Plan and research **MAIL-16**: Automatically match the exact report Sent item to its case

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MAIL-16.
- Canonical owner: [Outbound correspondence evidence](docs/frd/frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence)
- Activation/boundary: Allocated but non-blocking for `0.1.0-alpha.1` acceptance; post-report tracking starts manual via MAIL-15.
