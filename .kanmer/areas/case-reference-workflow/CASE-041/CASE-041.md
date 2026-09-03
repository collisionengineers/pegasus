---
id: CASE-041
type: ticket
title: >-
  Inspect-at fast update from claimant, repairer, storage location and principal
  address history
status: preparing
area: case-reference-workflow
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-09-02T22:09:37.240Z'
labels:
  - case
  - inspection
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links:
  - INTK-058
blocks:
  - UIIMP-014
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
archived: false
created: '2026-09-02T20:31:38.819Z'
updated: '2026-09-03T10:54:53.361Z'
---

## What

Inspect at becomes a choice: Image Based Assessment, Claimant address, Repairer location, Storage location, previous addresses used for this principal, Manual entry; options without a recorded value are disabled; the Case records a storage location.

## Why

D33. Mockup source: `Pegasus_UI_v2_src/src/21-case-sections.js` §inspection, `05-state.js` (`inspectAtOptions`).

## Approach

- Extend the CASE-027 inspection-address partial and the Core resolution; principal history is a query over the principal's cases, no new table; storage location is one column with grants.

## Verification

- [ ] Choosing an option with a recorded value fills the address; Manual keeps the input.
- [ ] Repairer location is offered disabled with its condition until a repairer address exists on the case (no repairer address is persisted anywhere today; [[INTK-058]] extracts one from the instruction material). Amended 2026-09-03 by operator answer.
- [ ] History lists distinct previous addresses newest first.

## Outcome
