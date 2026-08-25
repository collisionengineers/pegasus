---
id: CASE-011
type: ticket
title: Add the shared image viewer to Triage
status: backlog
area: intake-processing
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
updated: '2026-08-25T06:40:46.784Z'
---

## What

Add the existing shared image viewer to Triage's image surface.

## Why

Case and Image Intake now use the reusable viewer requested after [[CASE-006]]. Triage remains the proven image-bearing omission, so this ticket owns only that missing caller rather than another cross-application gallery programme.

## Approach

- Reuse the current viewer component and Triage's existing evidence source.
- Preserve the established previous, next, close, and outside-click behavior.
- Do not create a second viewer or change custody and authorization rules.

## Verification

- [ ] Opening a Triage image uses the same viewer as the delivered Case and Image Intake surfaces.
- [ ] Previous, next, close, and outside-click dismissal work for the Triage image set.
- [ ] Existing authorization and evidence-source tests remain green.

## Outcome
