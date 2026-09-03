---
id: CASE-037
type: ticket
title: >-
  Bind Search row actions to the shell script instead of the CSP-discarded
  inline block
status: backlog
area: case-reference-workflow
order: 170
assignee: ''
profile: fix
labels:
  - ui
  - search
  - audit-2026-09-02
  - functional-gap
groups:
  - EPIC-011
links:
  - CASE-026
  - PLAT-029
  - UIIMP-011
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-09-02T01:50:58.282Z'
updated: '2026-09-03T15:15:27.248Z'
---

## What

Bind the Search page's row actions — Copy Case/PO reference and Refresh — to the shell's delegated row-selection module in `wwwroot/js/site.js` instead of the per-page inline `@section Scripts` block in `Pages/Search/Index.cshtml`, so that a client-side row selection is reflected in production.

## Why

Phase 2 Done audit of [[CASE-026]] (2026-09-02, rubric C4/C6): production's Content Security Policy (`default-src 'self'`, no nonce or hash — `Program.cs`) silently discards the inline script that CASE-026's review fix added (`Index.cshtml` § Scripts), so in every deployed environment Copy Case/PO and Refresh act on the previously loaded row after a client-side selection. The defect was disclosed in the first proof pass ("F1 — needs a disposition") and dropped from the later closeout pass; no ticket owned it. CASE-026 stays Done (audit disposition `functional_gap`; a Done record is never reopened).

## Approach

- Reuse the delegated `[data-copy-target]` / row-selection handling that `site.js` already provides for other routed pages (one list per concept; no second selection-state owner); remove the inline block from `Pages/Search/Index.cshtml`.
- `site.js` belongs to the `global_shell` lock (PLAT-029's files): coordinate the lane; whole-file ownership per the EPIC-011 rules.
- Keep the Search markup and endpoints unchanged; regenerate the Test UI snapshot for the Search route if the rendered markup changes.

## Verification

- [ ] With the production CSP applied, selecting a row and pressing Copy Case/PO copies the selected row's reference, and Refresh re-queries with the selected row retained.
- [ ] No inline `<script>` remains in `Pages/Search/Index.cshtml`; a Browser-lane test covers the selection → copy path.
- [ ] Test UI snapshot for /search regenerated when the markup changed; CI repository-check green.

## Outcome
