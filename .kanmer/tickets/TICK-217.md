---
id: TICK-217
type: ticket
title: Accept per-field extraction thresholds with zero false case creation
status: todo
area: assurance-quality-cohorts-acceptance
priority: medium
assignee: ''
labels:
  - now
  - source-now
  - decision-required
links:
  - TICK-009
archived: false
created: '2026-08-12T15:10:50.296Z'
updated: '2026-08-12T15:10:50.296Z'
---

## What

Accept the per-field extraction thresholds from the reviewed cohort and untouched holdout, including the zero-false-case-creation gate.

## Why

This is the unresolved third step of the QDOS production path in `NOW.md`; assembling the evidence cohort alone does not accept the thresholds.

## Approach

- Review the completed cohort and untouched-holdout evidence against the defined field-level thresholds.
- Record the operator decision and any resulting boundary conditions without promoting local evidence to live acceptance.

## Verification

- [ ] The accepted thresholds and zero-false-case-creation outcome are recorded in their canonical owner.
- [ ] The evidence provenance and holdout separation are retained.

## Notes

- Source: `NOW.md` — Path 3.
- Related capability: MAIL-21 ([[TICK-009]]) .
