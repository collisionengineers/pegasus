---
id: TICK-016
type: ticket
title: >-
  CASE-30 — Track the QDOS-alpha inspection/report stage and EVA handoff without
  replacing EVA engineering work
status: implementing
area: engineering-assessment
order: 30
assignee: ''
profile: feature
labels:
  - capability
  - CASE-30
  - now
groups:
  - HZN-002
  - HZN-003
links: []
blocks: []
archived: true
created: '2026-08-12T15:03:53.085Z'
updated: '2026-08-17T12:51:32.671Z'
---

## What

Plan and research **CASE-30**: Track the QDOS-alpha inspection/report stage and EVA handoff without replacing EVA engineering work

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — CASE-30.
- Canonical owner: [Focused EVA manual handoff](docs/frd/frd-07-eva-and-external-engineering-handoff.md#focused-eva-manual-handoff)
- Activation/boundary: Review-stage generation, revision, download and proxy history are caller-proved locally; no EVA network call or replacement authority exists.
