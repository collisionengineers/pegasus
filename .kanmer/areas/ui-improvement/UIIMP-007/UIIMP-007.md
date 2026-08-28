---
id: UIIMP-007
type: ticket
title: >-
  FRD-12, capabilities and boundaries for the new shell, routes and activated
  capabilities
status: backlog
area: ui-improvement
assignee: ''
profile: chore
labels:
  - ui
  - docs
  - capabilities
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-28T08:05:30.058Z'
updated: '2026-08-28T08:05:30.058Z'
---

## What

- `docs/frd/frd-12-operator-experience.md`: route rename Queues→Cases (`/Cases`, rail groups Workflow / Pre-Case work / Exceptions), Cases→Search (`/Search`), Dashboard→Work Centre (needs-attention kinds), `/Unidentified` redirect retargets to `/Cases?tab=unidentified`, the Vehicle-images list route removed (detail page remains the image record), workspace tabs (max 4 LRU), command palette, keyboard map, breakpoints.
- `docs/capabilities.md`: add UI-16 "Integrated Operations Workspace shell" (Now); move AI-10 (AI job catalogue), EXT-09 (estimate editor), EXT-10 (valuations) and the engineer report to Now; note D7 (disabled seams) for Experian/Glass's/Audatex/Cazana.
- `docs/boundaries.md` + `docs/index.md`: outbound mail row moves out of the deferred table per D4 (the MAIL ticket owns the FRD-08/ADR text); AI ledger row updated per ADR-0035.

## Owns

`docs/frd/frd-12-operator-experience.md`, `docs/capabilities.md`, `docs/boundaries.md`, `docs/index.md`.

## Verification

- [ ] Allocation summary arithmetic in capabilities.md still adds up.
- [ ] `scripts/Test-DocumentationLinks.ps1` passes.
