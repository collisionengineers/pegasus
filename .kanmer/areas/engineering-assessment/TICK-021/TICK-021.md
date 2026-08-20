---
id: TICK-021
type: ticket
title: >-
  EXT-02 — MOT chronology and mileage evidence with
  supplied-versus-external-versus-estimated classification
status: implementing
area: engineering-assessment
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-20T04:05:18.234Z'
taken_at: '2026-08-20T04:04:01.021Z'
branch: task/tick-021-ext-02-mot-chronology
worktree: ../pegasus-worktrees/tick-021
labels:
  - capability
  - EXT-02
  - now
  - requires-live-approval
groups:
  - HZN-002
  - HZN-003
links: []
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-12T15:03:53.185Z'
updated: '2026-08-20T04:05:18.234Z'
---

## What

Plan and research **EXT-02**: MOT chronology and mileage evidence with supplied-versus-external-versus-estimated classification

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — EXT-02.
- Canonical owner: [Vehicle data and MOT enrichment](docs/frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment)
- Activation/boundary: Never invent mileage; live adapter/provider contract remains unresolved.
