---
id: INTK-008
type: ticket
title: Give ImageIntake an Image-initiated Case lifecycle and merge closure
status: done
area: intake-processing
order: 110
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-19T11:20:52.541Z'
  review: '2026-08-19T11:39:29.235Z'
  verifying: '2026-08-19T21:57:48.272Z'
  done: '2026-08-20T01:29:44.217Z'
labels:
  - image-initiated
  - image-intake
  - lifecycle
  - merge
  - box-custody
groups:
  - EPIC-007
links:
  - INTK-006
  - INTK-007
refs:
  - docs/prd/pegasus-product.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/frd/frd-12-operator-experience.md
commits:
  - 2cefd942
  - 0cd4e119
  - fcd0a497
  - 855160b7
prs:
  - '423'
deployment: production
archived: false
created: '2026-08-19T11:11:41.336Z'
updated: '2026-08-25T01:26:59.683Z'
---

## What

Make the existing ImageIntake aggregate the user-facing Image-initiated Case route.

A usable vision/VRM result already allocates the immutable per-VRM reference (for example AB12ABC-01). When no unique Instruction-initiated Case matches, the complete grouped submission must remain as one searchable Image-initiated Case under that reference. It must not be inserted into the formal Cases table and must not receive a Case/PO.

## Required behaviour

- Reuse the existing ImageIntake registration, per-VRM sequence, receipt/source identity, and Box custody boundaries.
- Preserve all grouped images, original filenames, receipts, order, VRM suggestions, custody, and history.
- Search and display Image-initiated Cases using the existing staff authorization boundary.
- Show explicit lifecycle states: awaiting instruction, merged/subsumed into an Instruction-initiated Case, and staff-closed with a reason.
- When an eligible Instruction-initiated Case later matches the VRM, close/convert the Image-initiated Case as merged/subsumed, link it to the formal Case, and show the Image-initiated reference and merge history from the formal Case.
- Staff may close an unmatched Image-initiated Case when instructions never arrive; closure requires a reason and is permanent/reasoned under existing lifecycle conventions.
- Conflicting valid VRMs do not create an Image-initiated Case; INTK-007 owns one grouped Unidentified U<n> reference with a specific conflicting_vrms marker.
- Image-initiated references remain separate from formal Case/PO, Audit, and Unidentified references.
- Supersede ADR-0013 rather than editing its accepted text in place; reconcile operator notes, PRD, FRDs, design, capabilities, index, and CONTEXT.md.

## Boundaries

This ticket owns the ImageIntake-to-Image-initiated Case terminology, lifecycle, search/history, merge/closure, and custody presentation. INTK-006 owns grouped recognition and routing; INTK-007 owns Unidentified references and conflicting-VRM reasons. The implementation must not weaken formal Principal/Case/PO invariants or add a second Case store/allocator.
