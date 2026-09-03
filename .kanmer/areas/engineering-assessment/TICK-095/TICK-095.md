---
id: TICK-095
type: ticket
title: UI-15 — Residual future Engineer workbench capabilities
status: backlog
area: engineering-assessment
order: 1200
assignee: ''
profile: feature
labels:
  - capability
  - UI-15
  - later
groups:
  - EPIC-003
links:
  - ENG-031
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-12T15:06:02.616Z'
updated: '2026-09-03T15:15:29.416Z'
---

## What

Retain only the future UI-15 Engineer-workbench behavior that is not already delivered, in flight, or owned by a precise ticket.

## Why

The original umbrella pre-dates the EPIC-011 port and now overlaps the Case/Assessment shell, evidence, valuations, estimates, report, correspondence and administration tickets. It must not become a second implementation of those capabilities.

The newly activated report-image crop/selection/order behavior has the precise owner [[ENG-031]] and is no longer residual work here.

## Approach

- Treat existing EPIC-011 tickets and their governing FRDs as the owners of delivered or in-flight workbench behavior.
- Link rather than duplicate any new precise capability ticket.
- At later activation, research only a named residual with a real caller and accepted contract; do not reopen a generic whole-workbench port.
- Keep this ticket Later / 1.0.0 until a concrete remaining capability is identified.

## Verification

- [ ] The ticket contains no work already owned by an active precise ticket.
- [ ] Every activated residual is split to one bounded owner before implementation.
- [ ] No parallel Case/Assessment shell or business-policy implementation is created.

## Notes

- Source: `docs/capabilities.md` — UI-15.
- Report image preparation/selection: [[ENG-031]].

## Outcome
