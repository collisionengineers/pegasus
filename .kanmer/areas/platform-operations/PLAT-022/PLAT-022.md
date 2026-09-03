---
id: PLAT-022
type: ticket
title: Prepare consent-gated Cazana provider activation
status: backlog
area: platform-operations
order: 700
assignee: ''
profile: feature
labels:
  - cazana
  - external-integration
  - requires-live-approval
  - deferred
links: []
blocks:
  - ENG-008
docs_todo: true
archived: false
created: '2026-08-21T12:55:59.971Z'
updated: '2026-09-03T15:15:28.310Z'
---

## Why

Cazana credentials and outbound provider access require separately recorded commercial, technical, and exact operator approval before Pegasus can activate a live valuation route.

## Scope

- Define and obtain the governing Cazana valuation requirements.
- Prepare the approved Azure Key Vault, Worker identity, sandbox, telemetry, recovery, and rollout evidence.
- Do not add a live key, enable a client, or deploy the route without exact operator consent.

## Verification

- The ticket records the exact consent and target approvals before any Azure or provider write.
- Web never receives the provider key; the Worker is the only proposed credential consumer.
- The activation evidence proves an approved Cazana sandbox/live request and recovery path.

## Notes

- Blocks [[ENG-008]] until its explicit activation gate is complete.
- Cazana valuation is a 1.0.0 capability; this ticket does not promote it by itself.
