---
id: CASE-026
type: ticket
title: >-
  Port the Search page (/Search) with the advanced filter grid and selected-Case
  pane
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - ui
  - wave-2
  - search
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-28T08:35:23.906Z'
updated: '2026-08-28T08:35:23.906Z'
---

## What

Wave 2 lane D of [[EPIC-011]]. Port `Pages/Search/Index.cshtml(.cs)` (moved from Cases/Index by PLAT-029) to `context.md` §1.7: advanced filter grid mapping 1:1 to the existing UI-07 inputs, results table with selectable rows (`tr[data-select-href]` + template preview, keyboard Enter/Space), server-rendered "Selected Case" pane for `?selected=` (facts, outstanding, Open Case, Copy Case/PO), "Closed · <outcome>" chip for non-design terminal states (D3).

## Owns

`src/Pegasus.Web/Pages/Search/**`, `tests/Pegasus.IntegrationTests/CasesIndexWebTests.cs`, `AdministrationSearchAccountWebTests.cs` (search parts).

## Blocked by

[[PLAT-029]].

## Verification

- [ ] Old `/Cases?query=` bookmarks 301 to `/Search` with the same values.
- [ ] No clipped text/overflow at 1580/1100/760.
