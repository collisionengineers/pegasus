---
id: TICK-218
type: ticket
title: Record operator acceptance of the QDOS production workflow
status: todo
area: assurance-quality-cohorts-acceptance
priority: medium
assignee: ''
labels:
  - now
  - source-now
  - requires-live-approval
  - blocked
links:
  - TICK-031
archived: false
created: '2026-08-12T15:10:50.326Z'
updated: '2026-08-12T15:10:50.326Z'
---

## What

Record designated-operator acceptance of the real end-to-end QDOS production workflow.

## Why

`NOW.md` identifies this as a final 0.1.0-alpha.1 acceptance step. Deployment, smoke checks, and individual live proofs do not themselves establish operator acceptance.

## Approach

- At activation, confirm each prerequisite journey and its live evidence.
- Obtain and record the designated operator's explicit acceptance only for the behavior actually demonstrated.

## Verification

- [ ] The real workflow evidence is linked and its limits are recorded.
- [ ] Operator acceptance is recorded by the authority owner, separate from deployment evidence.

## Notes

- Source: `NOW.md` — Path 8.
- Related capability: OPS-23 ([[TICK-031]]) .
- Blocked by: completion of the live QDOS workflow-path evidence.
