---
id: INTK-039
type: ticket
title: Complete grouped image matching and lifecycle merge
status: preparing
area: intake-processing
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-25T12:11:45.000Z'
labels:
  - operator-reported
  - image-intake
  - upload
  - azure-sql
  - box
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-12-operator-experience.md
  - docs/adr/0029-image-initiated-case-projection.md
deployment: not-deployed
archived: false
created: '2026-08-25T12:11:40.078Z'
updated: '2026-08-25T12:11:45.000Z'
---

## What

Make a grouped image upload remain visibly processing until its group outcome is settled, and allow the production Worker to complete the Image-initiated Case merge after automatic association.

## Why

The 2026-08-25 live tests showed a split post-upload result, a hidden-but-counted Not Ready Image Intake, and images left in the temporary Box folder. Production SQL proves the receipt association committed while the lifecycle event/merge custody work did not; the Worker runtime role lacks the lifecycle-event permissions used by the merge transaction.

## Verification

- A two-image group with one readable VRM settles as one group and associates every image to the one eligible Case.
- The upload page polls and exposes no staff decision while group reconciliation is pending.
- Image-first then instruction-first pairing records the merge lifecycle/history/custody work, folds the images into the formal Case, and leaves queue counts equal to visible rows.
- The deployed Worker role has the exact append-only lifecycle-event permissions.

## Outcome
