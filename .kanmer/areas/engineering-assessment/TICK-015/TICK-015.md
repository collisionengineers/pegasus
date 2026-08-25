---
id: TICK-015
type: ticket
title: >-
  CASE-21 — First successful manual EVA bundle generation records the
  once-per-case First sent to Engineer handoff proxy; EVA owns…
status: done
area: engineering-assessment
order: 40
assignee: ''
profile: feature
stageEntered:
  review: '2026-08-20T03:35:18.170Z'
  verifying: '2026-08-20T03:35:21.044Z'
  done: '2026-08-20T03:35:21.239Z'
labels:
  - capability
  - CASE-21
  - now
groups:
  - HZN-002
  - HZN-003
links: []
blocks: []
deployment: production
archived: false
created: '2026-08-12T15:03:53.066Z'
updated: '2026-08-25T01:26:59.636Z'
---

## What

Plan and research **CASE-21**: First successful manual EVA bundle generation records the once-per-case `First sent to Engineer` handoff proxy; EVA owns actual named-Engineer assignment and later generations are revisions

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — CASE-21.
- Canonical owner: [Focused EVA manual handoff](docs/frd/frd-07-eva-and-external-engineering-handoff.md#focused-eva-manual-handoff)
- Activation/boundary: Caller-proved locally with frozen revisions and replay-safe history; deployment, operator drag-and-drop acceptance, EVA receipt and named-Engineer assignment remain unproved.
