---
id: TICK-081
type: ticket
title: >-
  EXT-08 — Activate deterministic report generation from accepted Core-owned
  data through the approved renderer contract
status: preparing
area: documents-reports
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-19T09:04:18.407Z'
labels:
  - capability
  - EXT-08
  - later
  - requires-live-approval
groups:
  - EPIC-004
links:
  - TICK-092
  - TICK-093
  - TICK-094
  - DOCS-002
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
  - docs/adr/0028-run-integrated-renderer-in-web-container-app.md
archived: false
created: '2026-08-12T15:05:40.146Z'
updated: '2026-08-25T06:46:34.317Z'
---

## What

Plan and research **EXT-08**: Activate deterministic report generation from accepted Core-owned data through the approved renderer contract.

## Why

This is allocated to **Later / 1.1.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, one shared monolith caller/service, its render contract, failure behavior, and acceptance evidence.
- Every document and report type uses that same caller/service. A type may provide a different approved template to the shared contract, but it must not introduce a type-specific caller, service, renderer family, or deployment unit.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the shared caller's exact contract, template-selection input, and tests across document types.
- [ ] All activation conditions are accepted before implementation starts.
- [ ] Audit and Inspection reach the same Core-owned caller when their reports are activated; their template/provenance inputs may differ without creating a second caller.

## Notes

- Source: `docs/capabilities.md` — EXT-08.
- [[TICK-098]] defines Audit's physical-output parity and reference provenance within this shared-caller boundary.
- Blocked by: [[TICK-092]] — The renderer activation waits for accepted structured case and engineering data.
- Blocked by: [[TICK-093]] — The renderer activation waits for an accepted repair-specification contract.
- Blocked by: [[TICK-094]] — The renderer activation waits for accepted Engineer-owned outcomes and values.
