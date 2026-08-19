---
id: TICK-098
type: ticket
title: >-
  RPT-03 — Render Audit reports identically to Inspection reports with Audit
  reference provenance
status: preparing
area: documents-reports
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-19T09:06:03.452Z'
labels:
  - capability
  - RPT-03
  - later
  - post-alpha
  - blocked
groups:
  - EPIC-004
links:
  - TICK-092
  - TICK-093
  - TICK-094
  - TICK-205
  - TICK-207
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
archived: false
created: '2026-08-12T15:06:02.682Z'
updated: '2026-08-19T10:49:44.129Z'
---

## What

Plan and research **RPT-03**: render an Audit through the same approved physical report template and presentation as an Inspection.

## Why

Audit and Inspection differ in Collision Engineers' internal workflow and reference identity, not in the physical report issued. An Audit retains the normal Case/PO and uses the existing derived internal Audit reference: `a.{Case/PO}` for repairable or `ap.{Case/PO}` for total loss.

The current RPT-03 capability wording about conservative/maximised specifications and uplift is based on a false premise and must be corrected by this capability owner before implementation.

## Approach

- Reuse the existing Core-owned Inspection report contract and integrated renderer template; do not create an Audit template, output variant, uplift calculation, or dual-specification presentation.
- Bind the accepted Case/PO, applicable immutable Audit reference, Audit workflow provenance, and report identity/version without changing the physical report format.
- Fail closed when the Audit outcome/reference evidence is missing, conflicting, or ambiguous.
- Reconcile the RPT-03 governing wording in `docs/capabilities.md` and FRD-11 as part of implementation.

## Verification

- [ ] Audit and Inspection render through the same approved template and physical layout.
- [ ] Repairable and total-loss Audit journeys bind the correct existing `a.` / `ap.` reference without exposing a separate report family.
- [ ] No conservative/maximised pair, uplift field, calculation, wording, or presentation is introduced.

## Notes

- Related identity authority: CASE-03, CASE-04 and CASE-08.
- [[TICK-207]] records the shared-template decision.
- [[TICK-205]] is superseded by the operator correction recorded on 2026-08-19.
