---
id: DELIV-021
type: ticket
title: Release and prove near-real-time durable intake
status: backlog
area: delivery-repository
assignee: ''
profile: chore
labels: []
links: []
archived: false
created: '2026-08-25T15:18:41.009Z'
updated: '2026-08-25T15:18:41.009Z'
---

## What
Release the completed near-real-time durable intake changes and prove production behaviour, latency, recovery, telemetry, and normalized cost.

## Why
Build and test evidence cannot establish deployed callback routing, queue latency, cold-start behaviour, or Azure cost.

## Acceptance
- Deploy only with explicit approval and refresh current-architecture.md and operations.md in the same task.
- Prove both e-mail and manual-upload paths stage by stage in production.
- Observe seven normalized days: idle Functions cost at or below GBP 0.50/day and ordinary intake p95 at or below ten seconds.
- Keep scale-to-zero unless measured callback cold start prevents the target; any always-ready change requires an exact approved cloud write.

## Outcome
