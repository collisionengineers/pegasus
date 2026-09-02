---
id: INTK-024
type: ticket
title: Consolidate the QDOS corpus and map every extraction shape
status: done
area: intake-processing
order: 1330
assignee: ''
profile: spike
stageEntered:
  verifying: '2026-08-21T12:27:59.922Z'
  done: '2026-08-21T15:07:06.559Z'
labels:
  - corpus
  - research
  - awaiting-approval
links: []
deployment: production
archived: false
created: '2026-08-21T10:45:37.970Z'
updated: '2026-09-01T14:44:32.915Z'
---

## What

Operator-directed corpus consolidation and the full mapping + methodology,
presented for approval at
<https://claude.ai/code/artifact/abb2c56d-a857-474a-add5-0b6c7e1875b0>.

Done (all local-only; `corpus/` is git-ignored and never committed):
- `reference/qdosmapping/` → `corpus/qdosmapping/` (21 files incl. the
  operator-added EREF10 letter — Inspection + Audit, no third-party report).
- collisionsuite harvests copied in: `corpus/cereference/` (2,269 files,
  1.6 GB) and `corpus/documentexamples/` (18). Sources untouched.
- Git-history search across all refs: no previously committed corpus exists —
  nothing to recover.
- Per-file mapping of every qdosmapping file; category mapping of the wider
  corpus; letter-title taxonomy ("ENGINEER NOTIFICATION (REPORT + AUDIT
  REPORT)" = Inspection + Audit with no TP report; "AUDIT REPORT NOTIFICATION"
  = audit of the attached bodyshop report).

## Awaiting operator approval

- Mapping rules 5 and 7 (report-shape labels: `Reg No`/`Speedo`/report
  `Vehicle:` line scoped to report-titled documents; the
  accident-circumstances paragraph rule) — [[INTK-023]] implements them once
  approved.
- Flags recorded, each its own ticket if wanted: Engineer-Triage
  classification grammar; EREF24 `case_type_unavailable` outcome; US-format
  instruction-date rendering on the case page.

The research document on this ticket is the approval artifact's content;
sits at review until the operator approves or amends.
