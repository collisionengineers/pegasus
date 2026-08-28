---
id: UIIMP-007
type: ticket
title: >-
  FRD-12, capabilities and boundaries for the new shell, routes and activated
  capabilities
status: review
area: ui-improvement
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-28T08:08:15.950Z'
  review: '2026-08-28T08:18:26.948Z'
taken_at: '2026-08-28T08:13:08.212Z'
branch: task/uiimp-007-frd12-capabilities
worktree: ../pegasus-worktrees/uiimp-007-frd12-capabilities
labels:
  - ui
  - docs
  - capabilities
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
commits:
  - c63f1c20
  - e2f6bee3
  - e65571f4
  - b8b01479
  - 8d6d58cf
  - 1ee04b97
prs:
  - '586'
archived: false
created: '2026-08-28T08:05:30.058Z'
updated: '2026-08-28T08:32:35.591Z'
---

## What

- `docs/frd/frd-12-operator-experience.md`: route rename Queues→Cases (`/Cases`, rail groups Workflow / Pre-Case work / Exceptions), Cases→Search (`/Search`), Dashboard→Work Centre (needs-attention kinds), `/Unidentified` redirect retargets to `/Cases?tab=unidentified`, the Vehicle-images list route removed (detail page remains the image record), workspace tabs (max 4 LRU), command palette, keyboard map, breakpoints.
- `docs/capabilities.md`: add UI-16 "Integrated Operations Workspace shell" (Now); move AI-10 (AI job catalogue), EXT-09 (estimate editor), EXT-10 (valuations) and the engineer report (MI-01) to Now; note D7 (disabled seams) for Experian/Glass's/Audatex/Cazana.
- `docs/boundaries.md` + `docs/index.md`: AI ledger row updated per ADR-0035 (AUTO-009); design row names the shell contract. The automated-correspondence row is MAIL-024's and is untouched here.

## Owns

`docs/frd/frd-12-operator-experience.md`, `docs/capabilities.md`, `docs/boundaries.md` (except the automated-correspondence row), `docs/index.md`.

## Verification

- [x] Allocation summary arithmetic in capabilities.md still adds up (233 rows: Now 140 / Next 29 / Later 35 / Not planned 29; 204 planned).
- [x] `scripts/Test-DocumentationLinks.ps1` passes (124 files).
