---
id: PLAT-047
type: ticket
title: >-
  FRD-01 and FRD-04 wording: workflow display labels, one Principals area,
  Action Logs
status: review
area: platform-operations
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-28T08:08:17.460Z'
  review: '2026-08-28T08:14:35.621Z'
taken_at: '2026-08-28T08:13:02.931Z'
branch: task/plat-047-frd01-frd04
worktree: ../pegasus-worktrees/plat-047-frd01-frd04
labels:
  - docs
  - case
  - administration
groups:
  - EPIC-011
links:
  - PLAT-028
  - PLAT-027
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
commits:
  - 8f45f36d
  - ce56c28a
  - 16b7e884
  - '64534588'
prs:
  - '583'
archived: false
created: '2026-08-28T08:05:30.098Z'
updated: '2026-08-28T08:22:59.512Z'
---

## What

- FRD-01: record the display-label mapping (D3) — Report preparation and Post report render as "With Engineer", Post-report complete as "Complete"; other terminal outcomes render "Closed · <outcome>" in Search and are excluded from the Cases rail; Assessment access is With Engineer or onwards, never Review (D11); report-sent completion is evidence-driven (D10): sent from Pegasus auto-links, sent through EVA is detected and attached.
- FRD-04: Organisations and Principals become one "Principals" administration area (D2); Create Principal creates the backing organisation inline; the Principal settings dialog carries route addresses (read-only), EVA API policy (manual / automatic / ZIP only) and the Provider API credential (D8, API-04); Access review folds into Action Logs with filters Area/Actor/Result/From/To.

`docs/operator-notes.md` is protected: quote it before proposing any wording; if a statement there names a stage or the two-area model, stop and report instead of editing.

## Owns

`docs/frd/frd-01-case-identity-and-lifecycle.md`, `docs/frd/frd-04-parties-accounts-and-access.md`.

## Verification

- [ ] No Core state is renamed; only display labels are specified.
- [ ] `scripts/Test-DocumentationLinks.ps1` passes.
