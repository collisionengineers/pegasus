---
id: UIIMP-014
type: ticket
title: >-
  Snapshot states, catalogue entries and the browser walk for the single-scroll
  Case record
status: preparing
area: ui-improvement
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-09-02T22:34:41.706Z'
labels:
  - ui
  - test-ui
  - catalogue
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - DELIV-030
  - DELIV-045
refs:
  - docs/engineering.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-09-02T20:31:38.991Z'
updated: '2026-09-04T09:56:25.076Z'
---

## What

Add snapshot states for every Case section in edit and read-only, the retired Assessment redirect, the Awaiting instruction queue and the Operations notice; walk the record at 1580/1100/760.

## Why

CI proves the routed pages; UIIMP-010 is the final walk. Mockup source: `Pegasus_UI_v2_src/cdp.js` (DevTools walk pattern).

## Approach

- Owns `docs/design/test-ui/**` for its wave; reuse the DevTools walk pattern.

## Verification

- [ ] `Update-TestUiSnapshots.ps1 -Verify` and `Test-UiCatalogue.ps1` pass.

## Outcome
