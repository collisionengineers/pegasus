---
id: TICK-118
type: ticket
title: 'Activate live completeness and Review, Not ready, and Held queues'
status: done
area: platform-operations
order: 1980
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-20T05:34:52.385Z'
  implementing: '2026-08-20T05:35:45.114Z'
  review: '2026-08-20T05:35:54.711Z'
  verifying: '2026-08-20T05:36:00.197Z'
  done: '2026-08-20T05:36:11.085Z'
labels:
  - now
  - source-now
  - requires-live-approval
groups:
  - EPIC-003
  - HZN-003
links: []
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-12-operator-experience.md
deployment: production
archived: false
created: '2026-08-12T15:08:02.439Z'
updated: '2026-09-03T09:06:53.429Z'
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
