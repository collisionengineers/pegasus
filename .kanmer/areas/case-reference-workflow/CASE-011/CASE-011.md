---
id: CASE-011
type: ticket
title: Provide a reusable image gallery viewer across image-bearing pages
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - ui
  - images
  - gallery
  - operator-requested
links: []
refs:
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-21T13:19:14.240Z'
updated: '2026-08-21T13:19:14.240Z'
---

## What

Provide a pop-out image gallery for images shown in Case, Assessment, Receipts, and other image-bearing pages. It must show previous and next controls, a close control, and close when the user clicks outside the viewer.

## Why

Image review needs a consistent, focused viewer rather than page-specific expansion behaviour. This follows on from [[CASE-006]] while extending the scope to all relevant image surfaces.

## Approach

- Reuse one viewer component and the existing image sources.
- Apply the same navigation and dismissal behaviour everywhere it is used.

## Verification

- [ ] From every in-scope image surface, opening an image shows the pop-out viewer; previous, next, close, and outside-click dismissal work correctly.

## Outcome
