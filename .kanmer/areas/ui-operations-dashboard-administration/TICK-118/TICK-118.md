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
groups:
  - EPIC-003
  - HZN-003
links: []
archived: false
created: '2026-08-12T15:08:02.439Z'
updated: '2026-08-17T06:40:19.662Z'
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

- Source: the retired pre-Kanmer tracker QDOS production path step 4.
- Live Azure, credential, deployment, or external operations require fresh approval for exact targets.


## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.
