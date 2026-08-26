---
id: TICK-006
type: ticket
title: >-
  EVAL-04 — Copying the reviewed EML into the local emailevallocal tree and
  appending the JSONL adjudication log records the human…
status: done
area: mail-communications
order: 600
assignee: ''
profile: custom
requires: {}
labels:
  - capability
  - EVAL-04
  - now
groups:
  - HZN-003
links: []
archived: false
created: '2026-08-12T15:03:52.892Z'
updated: '2026-08-26T14:34:43.602Z'
---

## What

Plan and research **EVAL-04**: Copying the reviewed EML into the local `emailevallocal` tree and appending the JSONL adjudication log records the human result; source files are never moved or modified ([ADR-0016](adr/0016-standalone-desktop-email-evaluator.md))

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — EVAL-04.
- Canonical owner: [QDOS-alpha evaluation boundary](docs/frd/frd-08-email-mailbox-and-background-processing.md#qdos-alpha-evaluation-boundary)
- Activation/boundary: Separately owned prerequisite; not QDOS delivery. The retained `Now` target records the evaluator allocation boundary above, not a QDOS implementation gate.
