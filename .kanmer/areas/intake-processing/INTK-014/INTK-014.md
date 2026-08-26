---
id: INTK-014
type: ticket
title: >-
  Create a Box folder per Image-initiated Case and fold it into the paired
  case's folder on merge
status: done
area: intake-processing
order: 1190
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-20T05:07:21.084Z'
  review: '2026-08-20T06:02:05.365Z'
  verifying: '2026-08-20T06:19:19.871Z'
  done: '2026-08-20T12:44:40.644Z'
labels:
  - box
  - image-initiated
  - custody
  - operator-reported
links:
  - TICK-018
  - INTK-008
refs:
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
commits:
  - 562ac23d
  - fe5c22ea
  - 5bc72eea
  - 4a7b42a4
prs:
  - '462'
deployment: production
archived: false
created: '2026-08-20T03:16:37.511Z'
updated: '2026-08-26T14:34:44.628Z'
---

## What

Operator, 2026-08-20, verbatim: *"An image initiated case did not create a box folder - one should be created under its registration. When the corresponding instructions are received, it should be merged into that case on Pegasus, and its contents moved out of its Box folder into that case. The box folder should then be removed."*

So:
- Registering an Image-initiated Case creates a Box folder named for its registration reference (e.g. `AB12ABC-01`) and stores the group's images there.
- When the Image-initiated Case is merged/subsumed into an Instruction-initiated Case ([[INTK-008]] lifecycle), its Box contents move into that case's Box location and the now-empty image-case folder is removed.

## Why

Images currently never reach Box, so the custody trail the operator expects does not exist, and merged cases lose nothing but also gain nothing. [[TICK-018]] (DOC-02) owns the broader Box storage capability; this ticket delivers the image-initiated slice with the merge choreography the operator specified.

## Verification

- [ ] New image-initiated case → Box folder under its registration containing every group image, verified in production Box.
- [ ] Pairing/merge moves contents into the instruction case's folder and removes the image-case folder.
- [ ] Failure to reach Box never loses the images (custody in Pegasus remains authoritative; Box sync retries).
