---
id: INTK-065
type: ticket
title: Refresh principal evidence source inventory after policy consolidation
status: preparing
area: intake-processing
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-09-08T07:15:57.942Z'
labels:
  - regression
  - source-inventory
groups:
  - EPIC-014
links:
  - TICK-035
  - INTK-060
refs:
  - docs/frd/frd-09-provider-and-intermediary-routes.md
archived: false
created: '2026-09-08T07:15:35.565Z'
updated: '2026-09-08T07:15:57.942Z'
---

## What

Correct the principal-identification evidence generator and tracked package's current source inventory after the accepted policy consolidation. Preserve immutable originals and historical evaluation/cohort data.

## Why

PR700's unit lane exposes source inventory drift inherited from [[TICK-035]]: three deleted policy paths and a changed classification-contract hash. The otherwise hash-equal QDOS extraction source also retains an obsolete v7 ID after [[INTK-060]] moved it to v8. One correction must update generator mappings, reference IDs, generated metadata and the existing verification expectations together.

## Approach

- Reuse the existing generator and hash-mode conventions; no runtime or corpus-original changes.
- Distinguish current source links from the dossier's historical review baseline; do not promote evidence or rewrite the v5 evaluation.
- Limit the proposed change to generator, tracked JSON, existing Core test and principal-rules README clarification.
- This is an EPIC-014 supplemental follow-up. The original frozen 218-ticket roster stays unchanged; root owns the new single-ticket supplemental execution record. Preparation only until root approves the complete plan and current ownership.

## Verification

- [ ] Existing generation then `-Verify` agree, every tracked source path/hash/byte count resolves, and historical sections/original hashes remain unchanged.
- [ ] Focused PrincipalIdentificationCorpusTests pass without dropped coverage, exclusions or fabricated old paths; root owns build/test execution.

## Outcome
