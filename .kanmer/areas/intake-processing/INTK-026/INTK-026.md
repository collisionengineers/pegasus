---
id: INTK-026
type: ticket
title: Normalize kilometre case mileage to canonical miles
status: backlog
area: intake-processing
order: 420
assignee: ''
profile: feature
labels:
  - vehicle
  - mileage
  - normalisation
  - case-data
links: []
blocks:
  - ENG-008
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-21T12:56:00.000Z'
updated: '2026-09-03T15:15:27.767Z'
---

## Why

Pegasus must retain documented kilometre mileage faithfully while presenting canonical miles for case work and downstream valuation.

## Scope

- Convert kilometre mileage at new-case creation and later case-data writes using (1 km = 0.6213711922 miles), rounded to the nearest whole mile with midpoint values away from zero.
- Preserve typed original-kilometre provenance and display it as a compact marker beside the canonical miles value.
- Treat a missing documented unit as miles.
- Do not add a legacy conversion, batch, or read fallback for existing cases.

## Verification

- Tests cover kilometre conversion, rounding boundaries, missing-unit miles, provenance, and miles-first rendering.
- No existing persisted case is transformed.
- Blocks [[ENG-008]] so Cazana receives canonical case mileage.
