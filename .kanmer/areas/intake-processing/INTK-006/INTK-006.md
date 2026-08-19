---
id: INTK-006
type: ticket
title: >-
  Guarantee case association or Image-Only case creation for every vehicle-image
  upload
status: preparing
area: intake-processing
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-19T09:14:26.298Z'
labels:
  - upload
  - production-diagnostics
  - intake
  - vehicle-image
links:
  - TICK-011
  - PLAT-006
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-19T09:13:45.922Z'
updated: '2026-08-19T09:27:45.860Z'
---

## What
Fix the production vehicle-image path so every accepted image reaches one of the two operator-required outcomes:

1. If Pegasus confidently reads a VRM and exactly one eligible case matches without overlap, associate the image to that case.
2. Otherwise create an Image-Only case.

The Upload/status surface must show which outcome occurred.

## Why
The 2026-08-19 production JPEG was retained and scanned, but its only VRM suggestion was below the automatic threshold. Pegasus then left the receipt in `Needs sorting`, created no Image Intake record, made no case association, and created no Image-Only case. That third terminal path conflicts with the operator-confirmed two-outcome rule and appears as though nothing happened.

## Verification
- Production evidence and regression fixtures cover confident unique match, ambiguous/no match, low-confidence read, and no-readable-result paths.
- A confident VRM with one unambiguous eligible match associates the image to that existing case.
- Every other accepted vehicle-image path creates an Image-Only case without weakening immutable principal/reference rules.
- No accepted vehicle-image upload terminates only as generic `Needs sorting`.
- The status page identifies the resulting case and whether it was associated or newly created.

## Outcome
