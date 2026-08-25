---
id: TICK-059
type: ticket
title: Retired — API-02 provider processing-status lookup
status: backlog
area: automation-integrations
assignee: ''
profile: feature
labels:
  - capability
  - API-02
  - next
  - post-alpha
  - blocked
  - requires-live-approval
groups:
  - HZN-002
  - EPIC-009
links:
  - TICK-058
  - TICK-060
  - AUTO-008
archived: true
created: '2026-08-12T15:05:19.443Z'
updated: '2026-08-25T06:36:41.651Z'
---

## What

Retired **API-02** as a standalone provider processing-status capability.

## Why

The operator determined that ordinary processing is expected to finish in under five seconds and that a separately modelled provider-facing Processing experience is disproportionate. The durable submission receipt remains owned by [[TICK-058]], while terminal Case/PO or bounded-failure retrieval remains owned by [[TICK-060]].

## Disposition

- No transient provider-facing Processing state or dedicated status feature.
- Measure queue wait and processing cost in [[AUTO-008]] before changing architecture.
- Update FRD-09, ADR-0004 through a superseding ADR, and the capability registry during implementation of the surviving API tickets.

## Verification

- [x] Responsibilities are retained by API-01 and API-03 without a standalone status capability.
- [x] Performance investigation is separately tracked.

## Outcome

Archived by operator decision on 2026-08-21.
