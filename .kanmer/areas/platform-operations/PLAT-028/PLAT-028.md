---
id: PLAT-028
type: ticket
title: Redesign Organizations and Principals with provider API controls
status: preparing
area: platform-operations
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-21T14:23:40.633Z'
labels:
  - ui
  - administration
  - organizations
  - principals
  - provider-api
  - credentials
  - operator-requested
groups:
  - EPIC-008
  - HZN-002
  - EPIC-009
links:
  - TICK-058
  - TICK-061
  - PLAT-024
refs:
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-09-provider-and-intermediary-routes.md
docs_todo: true
archived: false
created: '2026-08-21T13:19:14.403Z'
updated: '2026-08-25T06:46:25.867Z'
---

## What

Redesign Organizations and Principals as one consolidated Administration experience and add the administrator controls required for the principal-scoped provider API.

## Why

The existing Organizations/Principals surface needs a deliberate redesign. Provider submission access belongs to a stable Principal, so credential generation and lifecycle controls must sit with that Principal rather than in a separate settings area.

## Approach

- Research and redesign the existing organization list, organization detail, principal creation, and principal replacement workflows.
- Add principal-scoped provider credential generation, reset/rotation, revocation, pause, and resume controls.
- Show a generated or reset secret once; retain only its hash and never display it later.
- Pause blocks new submissions while authenticated reads of the Principal's prior receipts/results remain available; revocation invalidates the credential.
- Reuse the existing Administrator authorization and permanent administration history conventions.
- Coordinate the API contract through [[TICK-058]] and credential lifecycle through [[TICK-061]].

## Verification

- [ ] The approved redesign supports existing Organization and Principal workflows without explanatory copy or page duplication.
- [ ] An Administrator can generate, reset, revoke, pause, and resume a Principal's provider access with the required confirmations and history.
- [ ] Non-administrators and provider clients cannot access the Administration surface.

## Outcome
