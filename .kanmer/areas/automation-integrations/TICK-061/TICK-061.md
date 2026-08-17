---
id: TICK-061
type: ticket
title: 'API-04 — Provider API credential issue, rotation, and revocation'
status: backlog
area: automation-integrations
assignee: ''
profile: feature
labels:
  - capability
  - API-04
  - next
  - post-alpha
  - blocked
  - requires-live-approval
groups:
  - HZN-002
links:
  - TICK-058
archived: false
created: '2026-08-12T15:05:19.485Z'
updated: '2026-08-17T06:41:43.851Z'
---

## What

Plan and research **API-04**: Provider API credential issue, rotation, and revocation

## Why

This is allocated to **Next / 0.4.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — API-04.
- Blocked by: [[TICK-058]] — Credential lifecycle belongs to the principal-scoped provider API contract.
