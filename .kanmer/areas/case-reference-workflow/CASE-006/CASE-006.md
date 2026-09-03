---
id: CASE-006
type: ticket
title: >-
  Case images are viewable in Pegasus: thumbnail preview, click to expand,
  Box-backed storage
status: done
area: case-reference-workflow
order: 900
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-20T06:07:05.668Z'
  review: '2026-08-20T06:34:44.383Z'
  verifying: '2026-08-20T08:22:51.129Z'
  done: '2026-08-20T12:45:19.560Z'
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
commits:
  - edceb77e
  - 56a71a38
  - f409cb7b
  - 67e3f63b
prs:
  - '464'
deployment: production
archived: false
created: '2026-08-20T03:16:37.709Z'
updated: '2026-09-03T09:06:46.836Z'
---

## What

Operator, 2026-08-20, verbatim: *"Not able to view images in Pegasus on image-initiated cases or any other kind. They should be stored on box and then viewable, with a preview in Pegasus, that you can click to expand."*

So on Image-initiated Case pages and case detail pages: an image gallery — thumbnails, click to expand — served by Pegasus, with Box as the storage home ([[TICK-018]]).

## Verification

- [ ] An image-initiated case shows its images as previews; clicking expands full-size.
- [ ] Instruction cases with image evidence show the same gallery.
- [ ] Images render from authorised, staff-only endpoints (no public URLs); large images load progressively.
- [ ] Browser + accessibility suites green.
