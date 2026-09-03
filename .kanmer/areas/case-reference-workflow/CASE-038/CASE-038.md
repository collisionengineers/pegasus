---
id: CASE-038
type: ticket
title: >-
  Deliver the single-scroll Case workspace frame with sticky identity, action
  bar and section jump-nav
status: preparing
area: case-reference-workflow
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-09-02T22:12:33.208Z'
labels:
  - ui
  - case
  - frame
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - ENG-034
  - CASE-039
  - CASE-040
  - CASE-041
  - CASE-029
  - CASE-009
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
archived: false
created: '2026-09-02T20:31:38.600Z'
updated: '2026-09-02T22:12:33.208Z'
---

## What

Replace the `?section=` tab semantics of `/Cases/{id}` with one scrolling record: sticky identity ribbon (adds Sign-off), action bar and section jump-nav with scroll-spy; the first three sections render on load and the rest render lazily as they approach the viewport; `?section=` jumps.

## Why

Operator direction (D29): the whole case, including the Engineer's work, on one page. Mockup source: `Pegasus_UI_v2_src/src/20-case.js`, `30-record.css`.

## Approach

- Reuse `Pages/Cases/Details.*`, `_CaseWorkspaceNav`, the existing edit lease and sticky edit bar; add a partial-fetch handler for lazy sections.
- Owns `wwwroot/css/site.css`, `wwwroot/js/site.js`, `Presentation/OperatorLabels.cs` for this wave (shared locks); adds the component vocabulary (section-nav, case-sticky, suggest-btn, derived, outcome-option) to the design README.
- No layout switch; no explanatory copy.

## Verification

- [ ] Layout-integrity tests at 1580/1100/760.
- [ ] Scroll-spy tracks all sections; `?section=estimate` lands on Estimate.
- [ ] Unsaved edits and the lease survive lazy section loads.

## Outcome
