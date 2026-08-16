---
id: TICK-118
type: ticket
title: 'Activate live completeness and Review, Not ready, and Held queues'
status: backlog
area: ui-operations-dashboard-administration
assignee: ''
profile: feature
labels:
  - now
  - source-now
  - requires-live-approval
links: []
archived: false
created: '2026-08-12T15:08:02.439Z'
updated: '2026-08-13T14:40:07.645Z'
---

## What

Activate the staff workflow that records instruction and image completeness and presents accurate Review, Not ready, and Held queues against live application data.

## Why

Local registrations and tests do not establish that staff can use the deployed queues or that queue counts and case state agree in the live workflow.

## Approach

- Ensure the deployed caller reads the Core-owned completeness and workflow state.
- Correct any composition, data, routing, or presentation gap preventing staff use.
- Exercise authenticated staff transitions and confirm dashboard, queue, and case-detail agreement.
- Retain accessibility, authorization, and stale-state behavior required by the canonical workflow.

## Verification

- [ ] Staff can record both completeness judgements through the live caller.
- [ ] Review, Not ready, and Held contain the correct cases and agree with case detail/dashboard counts.
- [ ] Authorization, validation, and visible failure behavior are exercised.
- [ ] Live evidence states exact environment, caller, and limitations.

## Notes

- Source: `NOW.md` QDOS production path step 4.
- Live Azure, credential, deployment, or external operations require fresh approval for exact targets.
