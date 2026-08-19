---
id: PLAT-008
type: ticket
title: Place the four remaining supplied Pegasus interface marks
status: backlog
area: platform-operations
assignee: ''
profile: feature
labels:
  - ui
  - design
  - assets
  - future-capability
groups:
  - EPIC-003
links:
  - PLAT-001
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-19T11:01:40.581Z'
updated: '2026-08-19T11:01:40.581Z'
---

## What

Place the four supplied but currently unused Pegasus interface marks—`activity`, `brand`, `calendar`, and `casefolder`—on their semantically appropriate application surfaces.

## Why

The Collision Engineers operator confirmed on 2026-08-19 that these custom interface illustrations were supplied to be used in the appropriate locations. They are not candidates for retirement. [[PLAT-001]] adopted the ten marks whose prototype placements were explicit but correctly left these four out because the design package did not map them to screens.

This ticket belongs to the cross-domain UI group because the eventual surfaces may span activity/history, product identity, date/due-work, and case navigation, while remaining presentation-only.

## Approach

- Recover the immutable supplied source bytes and verify their recorded source identity/checksums; do not redraw, regenerate, recolour, or substitute them.
- Review the artwork itself and the accepted interface journeys to map each mark to one existing or separately accepted future surface whose meaning it genuinely represents.
- Record the exact source-to-runtime mapping and checksum in `docs/design/README.md`.
- Optimize/copy runtime assets using the same reviewed process as the ten already placed marks.
- Use each mark decoratively beside visible text that carries the meaning; keep empty `alt` and `aria-hidden` unless design review establishes non-decorative content.
- Do not create a feature, route, panel, placeholder, or inactive control merely to give a mark somewhere to appear.
- If an appropriate destination depends on an unimplemented capability, link that owning ticket and place the mark when the real surface activates.

## Acceptance

- [ ] Every mark has one documented semantic meaning and mapped surface, accepted against the actual artwork and interface context.
- [ ] No duplicate icon meaning conflicts with the existing Lucide action/state vocabulary.
- [ ] Runtime copies and source-to-runtime checksums are recorded.
- [ ] Responsive and accessibility checks prove the marks remain decorative and do not replace text.
- [ ] All four marks are placed on genuine surfaces, or an individually linked capability owner records why placement must wait for that surface.
- [ ] No speculative product capability or empty UI is introduced for asset placement.

## Decision record

Operator decision, 2026-08-19: all four supplied custom interface illustrations are meant to be used in the appropriate locations.
