---
id: INTK-038
type: ticket
title: Replace raw Image Intake analysis metadata with operator terms
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels:
  - ui
  - design
  - image-intake
  - follow-up
links:
  - PLAT-015
refs:
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-25T06:39:54.701Z'
updated: '2026-08-25T06:39:54.701Z'
---

## What

Replace Image Intake's engine key, engine version, raw disposition enum, and case-version integer with the operator-facing result and supported business context.

## Why

The copy audit in [[PLAT-015]] found implementation metadata presented as though it were an operator decision. Engine and version identifiers are diagnostic facts; raw enums and concurrency versions are not operator language.

## Requirements

- Show the accepted analysis result using the repository's single operator-facing label mapping.
- Keep engine keys, engine versions, raw enum names, and case-version integers out of the ordinary operator view.
- Preserve diagnostic metadata in the existing diagnostic or audit boundary where it is genuinely required.
- Reuse the current Image Intake analysis and evidence model; do not create a second classification vocabulary.

## Verification

- [ ] The ordinary Image Intake page shows the supported business result without engine/version, raw enum, or case-version values.
- [ ] Diagnostic and audit evidence remains available through its existing supported route.
- [ ] Tests cover the operator label and prove that raw implementation values are not rendered.

## Outcome
