---
id: TICK-090
type: ticket
title: EXT-17 — Tractable or Ravin guided-capture integration
status: backlog
area: automation-integrations
order: 1180
assignee: ''
profile: feature
labels:
  - capability
  - EXT-17
  - later
  - requires-live-approval
groups:
  - HZN-002
  - EPIC-009
links: []
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-12T15:06:02.515Z'
updated: '2026-09-03T15:15:29.380Z'
---

## What

Plan and research **EXT-17**: Tractable or Ravin guided-capture integration

## Why

This is allocated to **Later / 1.4.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — EXT-17.


Operator Note: 03/09/2026 - Tractable will be the likely candidate.

Options were from:

1. Box File Request
2. In-built Pegasus link (current integration)
3. ravin.ai
4. Tractable

Will continue on hardening 2. as custom integration preferential but likely to be replaced with Tractable API and integration pending receipt of credentials and API documentation.
