---
id: TICK-097
type: ticket
title: >-
  RPT-02 — Assessment rendering covers four outcome variants and emits the fee
  note plus itemised repair-specification breakdown
status: backlog
area: documents-reports
assignee: ''
profile: feature
labels:
  - capability
  - RPT-02
  - later
  - post-alpha
  - blocked
groups:
  - EPIC-004
links:
  - TICK-092
  - TICK-093
  - TICK-094
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
archived: false
created: '2026-08-12T15:06:02.661Z'
updated: '2026-08-19T08:57:19.752Z'
---

## What

Plan and research **RPT-02**: Assessment rendering covers four outcome variants and emits the fee note plus itemised repair-specification breakdown

## Why

This is allocated to **Later / 1.1.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — RPT-02.
- Blocked by: [[TICK-092]] — The renderer activation waits for accepted structured case and engineering data.
- Blocked by: [[TICK-093]] — The renderer activation waits for an accepted repair-specification contract.
- Blocked by: [[TICK-094]] — The renderer activation waits for accepted Engineer-owned outcomes and values.
