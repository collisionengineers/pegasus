---
id: DOCS-001
type: ticket
title: >-
  Trigger report generation from complete accepted assessments and retain
  immutable report references
status: backlog
area: documents-reports
assignee: ''
profile: feature
labels:
  - now
  - renderer-integration
groups:
  - EPIC-004
links:
  - SIMPLI-014
  - TICK-081
  - TICK-092
  - TICK-093
  - TICK-094
  - TICK-096
  - TICK-097
blocks:
  - PLAT-007
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
archived: false
created: '2026-08-19T08:56:26.089Z'
updated: '2026-08-19T08:56:29.508Z'
---

## What

Add the Core-owned workflow that detects a complete, accepted assessment, invokes the integrated renderer, and records the generated report's immutable reference, version, hash, template/payload versions, provenance, and custody state against the case.

## Why

A renderer library is not an integrated product capability until a real Pegasus assessment caller produces and retains a report. `reference/rendererref1/` supplies the key assessment template/schema evidence.

## Approach

- Define readiness and idempotency in Core; fail closed on missing, unaccepted, or ambiguous required data.
- Map accepted case/assessment data to the renderer contract without a second business-policy implementation.
- Generate once per accepted input/version; retries return or reconcile the same durable job/result.
- Preserve earlier artifacts; corrections and addenda create new immutable versions.
- Surface generation state and actionable failures to staff without implying issue or delivery.

## Verification

- [ ] A complete accepted assessment produces a deterministic report through the composed application path.
- [ ] Incomplete or ambiguous assessment data cannot render.
- [ ] The case retains immutable reference/version/hash/provenance and idempotent retry behavior.
- [ ] Report generation does not count as approval, sending, or external receipt.

## Outcome
