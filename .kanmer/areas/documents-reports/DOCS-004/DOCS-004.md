---
id: DOCS-004
type: ticket
title: >-
  Activate addendum report rendering when an approved template and workflow are
  supplied
status: backlog
area: documents-reports
assignee: ''
profile: feature
labels:
  - RPT-05
  - later
  - post-alpha
  - blocked
  - evidence-required
groups:
  - EPIC-004
links:
  - TICK-100
  - TICK-092
  - TICK-094
  - TICK-208
  - DOCS-001
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
archived: false
created: '2026-08-19T10:53:54.911Z'
updated: '2026-08-19T10:53:54.911Z'
---

## What

Activate RPT-05 addendum rendering only after Collision Engineers supplies and approves a representative addendum template and confirms the real workflow/caller that invokes it.

## Why

The operator confirmed on 2026-08-19 that addenda are deferred. No approved addendum template or caller behavior currently exists. The generic imported `addendum-report` preset is not product authority and must remain unavailable.

## Activation trigger

Both of the following exist:

1. a concrete representative Collision Engineers addendum report/template; and
2. a confirmed real case workflow and caller for creating it.

## Scope when activated

- Record the supplied artifact as immutable governing evidence and obtain explicit approval for wording, layout, fields, conditions, signatures, and packaging.
- Define amendment identity/reason, the exact predecessor report relationship, inherited versus editable fields, and accepted source-version linkage.
- Define authorization, review/approval, recovery, correction, and independent Sent-evidence behavior.
- Store only the accepted amendment delta while retaining the immutable predecessor and all earlier artifacts.
- Reuse the existing Core-owned report identity/readiness/render contract and integrated Infrastructure renderer.
- Expose the accepted user-facing operation through the application and MCP with the same policy, authorization, confirmation, versioning, attribution, and recovery behavior.
- Prove deterministic rendering, version lineage, and representative PDF parity through real Chromium.

## Current boundary

Until both activation conditions exist, RPT-05 remains unsupported, unavailable, and fail closed. Do not expose a dormant descriptor, feature flag, generic template, placeholder content, or inferred professional/legal wording.

## Relationships

- Follows the deferral recorded by [[TICK-100]].
- [[DOCS-001]] owns immutable report identity/version/custody.
- [[TICK-092]] and [[TICK-094]] own accepted case and Engineer-entered source data.
- [[TICK-208]] owns preservation of each issued version's Sent evidence.
