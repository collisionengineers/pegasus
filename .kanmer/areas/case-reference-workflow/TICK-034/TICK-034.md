---
id: TICK-034
type: ticket
title: >-
  DATA-02 — Prepare inspection-address / repairer reference data from separately
  approved spreadsheets
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - capability
  - DATA-02
  - next
links: []
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-12T15:03:53.474Z'
updated: '2026-08-25T06:46:26.390Z'
---

## What

Plan and research **DATA-02**: Prepare inspection-address / repairer reference data from separately approved spreadsheets

## Why

The capability inventory allocates this outcome to **Next / 0.2.0**. This capability is **not designated until post-alpha** (Next / 0.2.0). It is blocked from implementation until the activation evidence and decisions below are accepted.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — DATA-02.
- Canonical owner: [Inspection address](docs/frd/frd-06-vehicle-and-engineering-evidence.md#inspection-address)
- Activation/boundary: Deferred pending accepted provider-location evidence, schema/package, migration, and caller proof; no domain-based address inference. The Principal inspection-mode setting (ADR-0018) selects a mode, never an address, and does not activate this pipeline.
