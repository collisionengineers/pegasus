---
id: TICK-005
type: ticket
title: >-
  EVAL-03 — Other category lets the reviewer enter a new category name and
  reasoning
status: done
area: mail-communications
order: 680
assignee: ''
profile: custom
requires: {}
labels:
  - capability
  - EVAL-03
  - now
groups:
  - HZN-003
links: []
archived: false
created: '2026-08-12T15:03:52.870Z'
updated: '2026-09-03T09:06:45.675Z'
---

## What

Plan and research **EVAL-03**: `Other` category lets the reviewer enter a new category name and reasoning

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — EVAL-03.
- Canonical owner: [QDOS-alpha evaluation boundary](docs/frd/frd-08-email-mailbox-and-background-processing.md#qdos-alpha-evaluation-boundary)
- Activation/boundary: Separately owned prerequisite; not QDOS delivery. The retained `Now` target records the evaluator allocation boundary above, not a QDOS implementation gate.
