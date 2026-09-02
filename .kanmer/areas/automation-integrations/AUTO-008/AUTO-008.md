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
archived: true
created: '2026-08-21T14:19:30.094Z'
updated: '2026-09-02T12:57:35.483Z'
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

Archived as stale and superseded on 2026-09-02.

The ticket's central 0–15 second dispatch-timer premise no longer describes the production path. Release 32 replaced timer-led normal dispatch with immediate post-commit publication, and release 33 deployed [[INTK-043]]'s unified, function-specific always-ready Worker route with stage telemetry. The planned median, p95, and worst-case measurement run was not completed, so this spike is not recorded as Done.

Remaining production measurement of the five-second p95 target belongs to [[INTK-043]] verification. Provider submission and terminal-result behavior remain owned by [[TICK-058]] and [[TICK-060]]; the standalone provider processing-status capability remains retired in [[TICK-059]].
