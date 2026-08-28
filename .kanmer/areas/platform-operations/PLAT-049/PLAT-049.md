---
id: PLAT-049
type: ticket
title: 'Operations: AI Job List, Service health and Send Unidentified to AI'
status: backlog
area: platform-operations
assignee: ''
profile: feature
labels:
  - ui
  - wave-4
  - operations
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
archived: false
created: '2026-08-28T08:35:24.068Z'
updated: '2026-08-28T08:35:24.068Z'
---

## What

Wave 4 of [[EPIC-011]]. Second pass on `Pages/Operations/**` after [[PLAT-023]]: AI Job List panel (kind + detail, record, started by, created, state chip, actions Review estimate / Open query / Review / Complete job / Cancel), "Send Unidentified to AI" creating an UnidentifiedResolution job, Service health table with Retry/View, EVA handoffs panel from submission records.

## Owns

`src/Pegasus.Web/Pages/Operations/**`, `tests/Pegasus.IntegrationTests/OperationsWebTests.cs`.

## Blocked by

[[PLAT-023]], the AI job ledger ticket, the service health ticket.
