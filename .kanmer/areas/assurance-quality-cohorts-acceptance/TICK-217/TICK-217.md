---
id: TICK-217
type: ticket
title: Accept per-field extraction thresholds with zero false case creation
status: backlog
area: assurance-quality-cohorts-acceptance
assignee: ''
profile: custom
requires: {}
labels:
  - now
  - source-now
  - decision-required
links:
  - TICK-009
  - TICK-186
archived: true
created: '2026-08-12T15:10:50.296Z'
updated: '2026-08-17T04:09:46.843Z'
---

## What

Accept the per-field extraction thresholds from the reviewed cohort and untouched holdout, including the zero-false-case-creation gate.

## Why

This is the unresolved third step of the QDOS production path in the retired pre-Kanmer tracker; assembling the evidence cohort alone does not accept the thresholds.

## Approach

- Review the completed cohort and untouched-holdout evidence against the defined field-level thresholds.
- Record the operator decision and any resulting boundary conditions without promoting local evidence to live acceptance.

## Verification

- [ ] The accepted thresholds and zero-false-case-creation outcome are recorded in their canonical owner.
- [ ] The evidence provenance and holdout separation are retained.

## Notes

- Source: the retired pre-Kanmer tracker — Path 3.
- Related capability: MAIL-21 ([[TICK-009]]) .

## Migrated validation

This standalone proof/validation ticket was consolidated into [[TICK-186]]. Its pending checks and approval boundaries now live in that work ticket's `checklist.md`; actual results belong in the owner's `proof.md`. Archived rather than deleted to preserve history.


## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.
