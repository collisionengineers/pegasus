---
id: DOCS-018
type: ticket
title: Fee note preview on the Report section
status: preparing
area: documents-reports
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-09-02T22:32:57.970Z'
labels:
  - reports
  - fee-note
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - UIIMP-014
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
archived: false
created: '2026-09-02T20:31:38.940Z'
updated: '2026-09-02T22:32:57.970Z'
---

## What

Preview a fee note from the agreed fee and description lines using the report contract's fee terms; no sending.

## Why

D42; sending stays MAIL-17. Mockup source: `Pegasus_UI_v2_src/src/22-case-engineer.js` (`fee-preview`).

## Approach

- Reuse the renderer and report contract constants.

## Verification

- [ ] Preview renders totals with VAT; nothing is stored or sent.

## Outcome
