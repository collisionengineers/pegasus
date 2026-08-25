---
id: INTK-041
type: ticket
title: Define near-real-time two-stage durable intake
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels: []
groups:
  - EPIC-002
  - EPIC-006
links: []
blocks:
  - INTK-003
  - INTK-042
  - MAIL-013
  - INTK-001
  - INTK-043
docs_todo: true
archived: false
created: '2026-08-25T15:18:14.748Z'
updated: '2026-08-25T15:18:47.405Z'
---

## What
Define the authoritative product, functional, and architectural contract for near-real-time e-mail and manual-upload intake through identification, classification, extraction, case creation, and custody publication.

## Why
The current polling path adds visible stale states and avoidable latency while sharply increasing Functions cost. The target must preserve durable recovery and Worker ownership without retaining competing generations of intake behaviour.

## Acceptance
- Add capability INT-33 and update the owning PRD plus FRD-02 and FRD-08.
- Add ADR-0032 for Graph wake-up plus immediate durable outbox publication, partially superseding ADR-0002's polling choice.
- Specify truthful sender/status behaviour, p95 latency, recovery timers, telemetry, and cost guardrails.

## Outcome
