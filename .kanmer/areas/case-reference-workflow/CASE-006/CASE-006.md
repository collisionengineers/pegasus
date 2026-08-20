---
id: CASE-006
type: ticket
title: >-
  Case images are viewable in Pegasus: thumbnail preview, click to expand,
  Box-backed storage
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - images
  - case-detail
  - ui
  - operator-reported
links:
  - TICK-018
  - INTK-014
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
archived: false
created: '2026-08-20T03:16:37.709Z'
updated: '2026-08-20T03:16:44.513Z'
---

## What

Operator, 2026-08-20, verbatim: *"Not able to view images in Pegasus on image-initiated cases or any other kind. They should be stored on box and then viewable, with a preview in Pegasus, that you can click to expand."*

So on Image-initiated Case pages and case detail pages: an image gallery — thumbnails, click to expand — served by Pegasus, with Box as the storage home ([[TICK-018]]).

## Verification

- [ ] An image-initiated case shows its images as previews; clicking expands full-size.
- [ ] Instruction cases with image evidence show the same gallery.
- [ ] Images render from authorised, staff-only endpoints (no public URLs); large images load progressively.
- [ ] Browser + accessibility suites green.
