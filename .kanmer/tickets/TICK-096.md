---
id: TICK-096
type: ticket
title: >-
  RPT-01 — Deterministic renderer validates accepted data, computes each figure
  once, and applies the fixed Collision Engineers de…
status: todo
area: reports-renderer-lifecycle-correspondence
priority: medium
assignee: ''
labels:
  - capability
  - RPT-01
  - later
  - post-alpha
  - blocked
links:
  - TICK-092
  - TICK-093
  - TICK-094
archived: false
created: '2026-08-12T15:06:02.638Z'
updated: '2026-08-12T15:09:20.107Z'
---

## What

Plan and research **RPT-01**: Deterministic renderer validates accepted data, computes each figure once, and applies the fixed Collision Engineers design

## Why

This is allocated to **Later / 1.1.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — RPT-01.
- Blocked by: [[TICK-092]] — The renderer activation waits for accepted structured case and engineering data.
- Blocked by: [[TICK-093]] — The renderer activation waits for an accepted repair-specification contract.
- Blocked by: [[TICK-094]] — The renderer activation waits for accepted Engineer-owned outcomes and values.
