---
id: CASE-026
type: ticket
title: >-
  Port the Search page (/Search) with the advanced filter grid and selected-Case
  pane
status: review
area: case-reference-workflow
assignee: zcode
profile: feature
stageEntered:
  implementing: '2026-08-28T18:45:18.734Z'
  review: '2026-08-28T19:04:46.136Z'
taken_at: '2026-08-28T18:33:20.835Z'
branch: task/case-026-search-page
worktree: ../pegasus-worktrees/case-026-search-page
labels:
  - ui
  - wave-2
  - search
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/606'
archived: false
created: '2026-08-28T08:35:23.906Z'
updated: '2026-08-28T21:53:37.394Z'
---

## What

Wave 2 lane D of [[EPIC-011]]. Port `Pages/Search/Index.cshtml(.cs)` (moved from Cases/Index by PLAT-029) to `context.md` §1.7: advanced filter grid mapping 1:1 to the existing UI-07 inputs, results table with selectable rows (`tr[data-select-href]` + template preview, keyboard Enter/Space), server-rendered "Selected Case" pane for `?selected=` (facts, outstanding, Open Case, Copy Case/PO), "Closed · <outcome>" chip for non-design terminal states (D3).

## Owns

`src/Pegasus.Web/Pages/Search/**`, `tests/Pegasus.IntegrationTests/CasesIndexWebTests.cs`, `AdministrationSearchAccountWebTests.cs` (search parts).

## Blocked by

[[PLAT-029]].

## Verification

- [x] Old `/Cases?query=` bookmarks 301 to `/Search` with the same values. Proved 2026-08-28 by `AdministrationSearchAccountWebTests.OldCasesSearchLinksRedirectToSearchWithTheirValuesIntact`: a thirteen-parameter bookmark, the 301 target asserted byte for byte, and every value rendered back into its field (PASS 6/6).
- [ ] No clipped text/overflow at 1580/1100/760. Needs a browser run; not done in the page lane, left for the orchestrator's walk.
