---
id: TICK-020
type: ticket
title: >-
  EXT-01 — DVLA/DVSA make, model, manufacture year, engine capacity, fuel type,
  MOT chronology, mileage evidence, and operator-con…
status: done
area: engineering-assessment
order: 1750
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-20T04:20:31.015Z'
  review: '2026-08-20T04:25:34.367Z'
  verifying: '2026-08-20T04:41:52.854Z'
  done: '2026-08-20T12:47:25.773Z'
labels:
  - capability
  - EXT-01
  - now
  - requires-live-approval
groups:
  - HZN-002
  - HZN-003
  - EPIC-009
links: []
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
commits:
  - e2cc731b
prs:
  - '450'
deployment: production
archived: false
created: '2026-08-12T15:03:53.163Z'
updated: '2026-09-01T14:44:33.347Z'
---

## What

Plan and research **EXT-01**: DVLA/DVSA make, model, manufacture year, engine capacity, fuel type, MOT chronology, mileage evidence, and operator-confirmed reconciliation

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [x] A task-level plan records the exact feature contract, caller, failure behavior, and required tests. (Ticket plan, 2026-08-20: the workflow, adapters, Worker path, persistence and UI already exist; the plan completes the production Web availability composition and the observation's vehicle-detail display.)
- [x] The activation criteria have been satisfied or explicitly accepted before implementation begins. (Provider/API/credentials were already resolved and deployed — official DVLA VES v1.2 + DVSA MOT History v1, production Key Vault references live-verified per `docs/operations.md`; the remaining gate, live acceptance evidence from a real production lookup after release, is explicitly NOT claimed by this ticket and stays approval-gated.)

## Notes

- Source: `docs/capabilities.md` — EXT-01.
- Canonical owner: [Vehicle data and MOT enrichment](docs/frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment)
- Activation/boundary: Live adapter/provider contract remains unresolved; approved local replay returns explicit unavailable when evidence is absent.
- Shares the observation display block with [[TICK-021]] — branch stacked on `task/tick-021-ext-02-mot-chronology` (PR #448 merges first).
