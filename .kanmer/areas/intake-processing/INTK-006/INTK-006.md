---
id: INTK-006
type: ticket
title: Associate each vehicle-image group or create one Image-Only case
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
groups:
  - EPIC-007
links:
  - TICK-011
  - PLAT-006
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-19T09:13:45.922Z'
updated: '2026-08-19T09:48:49.320Z'
---

## What
Fix grouped vehicle-image processing so every accepted image group reaches one of the two operator-required outcomes:

1. If the group yields one unambiguous confident VRM and exactly one eligible case matches without overlap, associate every image in the group to that case.
2. Otherwise create one Image-Only case containing the whole image group.

The Upload/status surface must show which outcome occurred. Production diagnostics must also distinguish the two recognition layers without recording image content.

## Why
A damage close-up may contain no registration while another image selected with it does. The group is the evidence unit. The 2026-08-19 production JPEG was retained and both plate-detection and plate-recognition ran, but the partial suggestion was below threshold; Pegasus left it in `Needs sorting` with no Image Intake, association, or Image-Only case.

Today the engine also collapses “no plate detected” and “plate detected but unreadable” into `NoReadableResult`, so production evidence cannot say which recognition layer abstained. Both the third terminal path and the diagnostic ambiguity must be removed.

## Verification
- Fixtures cover groups with one readable VRM plus unreadable damage close-ups, one unique existing-case match, no match, ambiguous/conflicting reads, low-confidence reads, and no-readable results.
- One confident, unambiguous group VRM with one eligible case match associates every group image to that case.
- Every other accepted group creates one Image-Only case containing every group image.
- Conflicting evidence fails closed against existing cases and is kept together in the Image-Only case.
- No group member terminates as an unrelated generic `Needs sorting` item.
- The status/result surface identifies the resulting existing or newly created case for the whole group.
- Non-sensitive diagnostics distinguish at least: detector found no plate; detector found a crop but recognition returned no usable registration; usable suggestion produced; technical failure.
- Diagnostics prove whether each layer ran without logging source-image content or creating a second business-decision taxonomy.

## Outcome
