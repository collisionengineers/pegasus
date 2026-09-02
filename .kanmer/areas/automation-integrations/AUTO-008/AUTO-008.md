---
id: AUTO-008
type: ticket
title: Measure and reduce durable intake processing latency
status: preparing
area: automation-integrations
order: 20
assignee: ''
profile: spike
stageEntered:
  preparing: '2026-08-21T14:20:04.663Z'
labels:
  - performance
  - intake
  - provider-api
  - research
groups:
  - HZN-002
  - EPIC-009
links:
  - TICK-058
refs:
  - docs/frd/frd-09-provider-and-intermediary-routes.md
archived: false
created: '2026-08-21T14:19:30.094Z'
updated: '2026-09-01T14:50:16.692Z'
---

## What

Measure end-to-end durable intake latency and separate queue wait from actual processing cost.

## Why

Provider submissions are expected to complete quickly, but the current Worker dispatch schedule may add up to 15 seconds before processing begins. Architecture changes must be based on measured latency rather than a separate provider-facing Processing feature.

## Approach

- Measure durable receipt, dispatch wait, processing, allocation, and terminal persistence independently.
- Compare representative current fixtures and approved predecessor evidence when available.
- Recommend the smallest measured improvement and file separate implementation tickets for any change.

## Verification

- [ ] Median, p95, and worst-case timings identify queue wait versus processing cost.
- [ ] Recommendations cite evidence and preserve durable replay and failure semantics.

## Outcome
