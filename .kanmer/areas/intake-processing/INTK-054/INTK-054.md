---
id: INTK-054
type: ticket
title: Add append-only staff notes to the Triage History
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels:
  - triage
  - notes
  - work-pack
  - wave-B
groups:
  - EPIC-011
links:
  - INTK-046
  - UIIMP-012
refs:
  - docs/frd/frd-03-triage.md
  - docs/frd/frd-12-operator-experience.md
deployment: not-deployed
archived: false
created: '2026-09-01T21:54:35.701Z'
updated: '2026-09-01T21:54:35.701Z'
---

## What

The Triage `History` merges durable events and append-only attributable staff notes in chronological order; corrections are new notes; no edit or delete exists. The Triage `Files` view holds retained request sources and attachments plus linked vehicle images with view and download; no arbitrary file store or upload action is added.

## Why

Operator decision D25 (2026-09-01). Triage has no note entity today; every history entry is a retained business event ([[INTK-046]], `frd-03`). The panel keeps its shipped name "Permanent history" unless the operator rules otherwise on [[UIIMP-012]].

## Approach

- Reuse the Triage Core commands and the existing history projection; add the note command and store; labels in `Presentation/OperatorLabels.cs`; no explanatory copy.
- Files: `Pages/Triage/Details.cshtml(.cs)`, Triage Core and persistence, one migration (migration lock).

## Verification

- [ ] A note is attributable, timestamped and immutable; a correction is a new note.
- [ ] History renders events and notes in one chronological list with Date, Time, ID and text.
- [ ] Files shows retained sources, attachments and linked vehicle images with view/download and offers no upload.
- [ ] `QdosTriageIntegrationTests` remain green.

## Outcome
