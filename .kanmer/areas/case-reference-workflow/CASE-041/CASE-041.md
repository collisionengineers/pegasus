---
id: CASE-041
type: ticket
title: >-
  Inspect-at fast update from claimant, repairer, storage location and principal
  address history
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - case
  - inspection
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - UIIMP-014
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
docs_todo: true
archived: false
created: '2026-09-02T20:31:38.819Z'
updated: '2026-09-02T22:04:12.795Z'
---

## What

Inspect at becomes a choice: Image Based Assessment, Claimant address, Repairer location, Storage location, previous addresses used for this principal, Manual entry; options without a recorded value are disabled; the Case records a storage location.

## Why

D33. Mockup source: `Pegasus_UI_v2_src/src/21-case-sections.js` §inspection, `05-state.js` (`inspectAtOptions`).

## Approach

- Extend the CASE-027 inspection-address partial and the Core resolution; principal history is a query over the principal's cases, no new table; storage location is one column with grants.

## Verification

- [ ] Choosing Repairer fills the address; Manual keeps the input.
- [ ] History lists distinct previous addresses newest first.

## Outcome
