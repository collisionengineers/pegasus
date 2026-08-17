---
id: TICK-020
type: ticket
title: >-
  EXT-01 — DVLA/DVSA make, model, manufacture year, engine capacity, fuel type,
  MOT chronology, mileage evidence, and operator-con…
status: backlog
area: integration-dvla-dvsa-whatsapp-guided-capture
assignee: ''
profile: feature
labels:
  - capability
  - EXT-01
  - now
  - requires-live-approval
links: []
archived: false
created: '2026-08-12T15:03:53.163Z'
updated: '2026-08-17T04:09:01.542Z'
---

## What

Plan and research **EXT-01**: DVLA/DVSA make, model, manufacture year, engine capacity, fuel type, MOT chronology, mileage evidence, and operator-confirmed reconciliation

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — EXT-01.
- Canonical owner: [Vehicle data and MOT enrichment](docs/frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment)
- Activation/boundary: Live adapter/provider contract remains unresolved; approved local replay returns explicit unavailable when evidence is absent.
