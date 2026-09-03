---
id: PLAT-069
type: ticket
title: >-
  Move Service health off Operations; Operations shows a partial-data notice
  linking to Administration
status: preparing
area: platform-operations
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-09-02T22:22:00.643Z'
labels:
  - operations
  - ui
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - UIIMP-014
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-09-02T20:31:38.879Z'
updated: '2026-09-02T22:22:00.643Z'
---

## What

Remove the Service health table from `/Operations`; when any query is not current, administrators see a one-line notice with a link to Administration → Service health.

## Why

D37. Mockup source: `Pegasus_UI_v2_src/src/16-operations.js`.

## Approach

- Delete the panel from `Pages/Operations`; keep PLAT-051's admin area.

## Verification

- [ ] Snapshot states updated; no dead link.

## Outcome
