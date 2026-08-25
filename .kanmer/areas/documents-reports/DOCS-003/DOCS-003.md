---
id: DOCS-003
type: ticket
title: Activate diminution report rendering when an approved template is supplied
status: backlog
area: documents-reports
assignee: ''
profile: feature
labels:
  - RPT-04
  - later
  - evidence-required
groups:
  - EPIC-004
links:
  - TICK-099
  - TICK-067
  - TICK-092
  - TICK-094
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
archived: false
created: '2026-08-19T10:51:29.459Z'
updated: '2026-08-25T06:46:23.590Z'
---

## What

Activate RPT-04 diminution report rendering only after Collision Engineers supplies and approves a representative diminution report template.

## Why

The operator confirmed on 2026-08-19 that diminution reporting is deferred because no approved template exists. The generic imported `diminution-rebuttal` preset is not product authority and must remain unavailable.

## Activation trigger

A concrete representative Collision Engineers diminution report/template is supplied for review.

## Scope when activated

- Record the supplied artifact as immutable governing evidence and obtain explicit approval for its wording, layout, fields, conditions, signatures, and packaging.
- Define the Engineer-entered diminution percentage meaning, precision, calculation basis, and rounding from approved business evidence.
- Define accepted original-case/report identity and version linkage, human approval, correction/addendum behavior, and fail-closed states.
- Reuse the existing Core-owned report identity/readiness/render contract and integrated Infrastructure renderer; do not create a separate service, host, or deployment unit.
- Add the real user and MCP callers only after the behavior and template are accepted.
- Prove deterministic rendering and representative PDF parity through real Chromium.

## Current boundary

Until the activation trigger occurs, RPT-04 remains unsupported, unavailable, and fail closed. Do not expose a dormant descriptor, feature flag, generic template, placeholder content, or inferred professional/legal wording.

## Relationships

- Follows the deferral decision in [[TICK-099]].
- [[TICK-067]] owns the broader Diminution case capability.
- [[TICK-092]] and [[TICK-094]] own accepted source data and Engineer-entered values.
