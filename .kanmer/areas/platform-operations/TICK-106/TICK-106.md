---
id: TICK-106
type: ticket
title: >-
  MI-02 — Per-principal report counts, types, and periods feeding invoice
  generation
status: backlog
area: platform-operations
assignee: ''
profile: feature
labels:
  - capability
  - MI-02
  - later
groups:
  - EPIC-003
links:
  - TICK-075
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-12T15:06:02.846Z'
updated: '2026-08-25T06:46:39.065Z'
---

## What

Plan and research **MI-02**: Per-principal report counts, types, and periods feeding invoice generation

## Why

This is allocated to **Later / 1.2.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — MI-02.
- Blocked by: [[TICK-075]] — Report and invoice measures consume accepted report-send events.
