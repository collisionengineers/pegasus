---
id: INTK-005
type: ticket
title: Allow one Upload submission to accept and track multiple files
status: preparing
area: intake-processing
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-19T09:14:26.260Z'
labels:
  - upload
  - ui
  - intake
groups:
  - EPIC-007
links:
  - PLAT-006
blocks:
  - INTK-006
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-19T09:13:42.674Z'
updated: '2026-08-19T09:48:49.232Z'
---

## What
Expand the authenticated Upload page so one submission can accept as many selected files as staff need and preserve the selected image files as one processing group.

## Why
Staff intake commonly consists of several related documents and vehicle images. Close-up damage images may contain no visible registration, while another image in the same selected group identifies the vehicle. Processing each file as an unrelated receipt would strand valid evidence or attach only part of the group.

## Verification
- Staff can select or add multiple files in one Upload interaction.
- Every selected file is visibly listed and submitted without silent loss.
- Files preserve a durable submission-group identity in addition to their per-file receipt identities.
- VRM recognition considers all vehicle images in the group; one unambiguous confident registration can associate the entire image group.
- Conflicting/ambiguous registrations never attach any member of the group to the wrong case.
- Each file receives an honest receipt/processing outcome, including partial technical failures, without losing group membership.
- Existing single-file upload remains supported as a one-member group.

## Outcome
