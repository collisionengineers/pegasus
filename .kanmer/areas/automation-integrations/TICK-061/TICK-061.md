---
id: TICK-061
type: ticket
title: 'API-04 — Issue, reset, revoke, pause, and resume provider credentials'
status: preparing
area: automation-integrations
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-21T14:20:04.602Z'
labels:
  - capability
  - API-04
  - next
  - post-alpha
  - blocked
  - requires-live-approval
groups:
  - HZN-002
  - EPIC-009
links:
  - TICK-058
blocks:
  - TICK-058
  - PLAT-028
refs:
  - docs/frd/frd-09-provider-and-intermediary-routes.md
archived: false
created: '2026-08-12T15:05:19.485Z'
updated: '2026-08-25T06:36:42.265Z'
---

## What

Deliver and plan **API-04**: Principal-scoped provider credential issue, reset/rotation, revocation, pause, resume, and authentication.

## Why

API-01 and API-03 need a real Principal-scoped machine identity. Administrators also need safe lifecycle controls without storing or redisplaying clear secrets.

## Approach

- Add one credential per Principal with one-time secret display and hash-only storage.
- Reset immediately invalidates the previous secret; revocation invalidates authentication.
- Pause blocks new submissions while authenticated reads of prior receipts/results remain available.
- Reuse existing Core Administrator authorization, expected-version, reason, operation-key, and permanent-history conventions.
- Supply the backend contracts consumed by [[PLAT-028]], then unblock [[TICK-058]].

## Verification

- [ ] Core, persistence, authentication, migration, and architecture plans/tests cover the complete lifecycle and fail-closed isolation.
- [ ] No live credential is issued without separate exact-target approval.

## Notes

- Source: `docs/capabilities.md` — API-04.
- Blocks API-01 and PLAT-028 through structured dependencies.

## Outcome
