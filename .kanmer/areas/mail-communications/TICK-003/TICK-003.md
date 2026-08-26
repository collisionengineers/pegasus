---
id: TICK-003
type: ticket
title: >-
  EVAL-01 — Local development-only EML categorisation evaluator over a read-only
  local working copy, recording adjudications into t…
status: done
area: mail-communications
order: 580
assignee: ''
profile: custom
requires: {}
labels:
  - capability
  - EVAL-01
  - now
groups:
  - HZN-003
links: []
archived: false
created: '2026-08-12T15:03:52.819Z'
updated: '2026-08-26T14:34:43.570Z'
---

## What

Plan and research **EVAL-01**: Local development-only EML categorisation evaluator over a read-only local working copy, recording adjudications into the local `emailevallocal` tree ([ADR-0016](adr/0016-standalone-desktop-email-evaluator.md))

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — EVAL-01.
- Canonical owner: [QDOS-alpha evaluation boundary](docs/frd/frd-08-email-mailbox-and-background-processing.md#qdos-alpha-evaluation-boundary)
- Activation/boundary: Separately owned prerequisite; not QDOS delivery. The retained `Now` target records the evaluator allocation boundary above, not a QDOS implementation gate.
