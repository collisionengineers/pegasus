---
id: UIIMP-003
type: ticket
title: Integrate approved Test UI experiments into Live Razor pages
status: preparing
area: ui-improvement
order: 210
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-26T12:10:20.470Z'
labels:
  - ui
  - design
  - razor
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-26T12:09:14.838Z'
updated: '2026-09-01T14:50:16.816Z'
---

## What

Carry user-approved Test UI experiments back into the Live Razor UI, and create a repeatable conversion skill only if manual round-tripping proves error-prone.

## Why

The disposable catalogue is useful only when selected improvements can be integrated safely into the deployable Razor Pages application without losing its dynamic behavior or creating parallel implementations.

## Approach

- Start only after the user identifies an approved prototype.
- Port presentation changes into the existing Razor page, shared CSS, and existing components.
- Preserve handlers, authorization, antiforgery, validation, accessibility, and dynamic states.
- Document the Razor-to-prototype-to-Razor workflow; add a dedicated Codex skill only when the ticket's research demonstrates a repeated conversion need.
- Remove superseded markup and never retain Test UI as a runtime fallback.

## Verification

- [ ] The selected Live UI matches the approved prototype at the agreed visual states.
- [ ] Focused Razor, integration, and browser tests pass.
- [ ] Authorization, validation, antiforgery, accessibility, and dynamic behavior remain intact.
- [ ] Test UI remains absent from deployment output.

## Outcome
