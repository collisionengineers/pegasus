---
id: INTK-006
type: ticket
title: Associate each vehicle-image group or create one Image-initiated Case
status: implementing
area: intake-processing
assignee: Codex
profile: fix
stageEntered:
  preparing: '2026-08-19T09:14:26.298Z'
  review: '2026-08-19T10:46:45.932Z'
  implementing: '2026-08-19T10:49:31.628Z'
taken_at: '2026-08-19T10:39:01.883Z'
branch: intk-006-grouped-image-routing
worktree: .worktrees/intk-006
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
  - docs/prd/pegasus-product.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/frd/frd-12-operator-experience.md
commits:
  - 70d7c89c
prs:
  - '417'
archived: false
created: '2026-08-19T09:13:45.922Z'
updated: '2026-08-19T10:59:54.519Z'
---

## What
Fix grouped vehicle-image processing so every accepted image group reaches one of the two operator-required outcomes:

1. If the group yields one unambiguous confident VRM and exactly one eligible case matches without overlap, associate every image in the group to that case.
2. Otherwise create one Image-initiated Case containing the whole image group.

The Upload/status surface must show which outcome occurred. Production diagnostics must also distinguish the two recognition layers without recording image content.

## Why
A damage close-up may contain no registration while another image selected with it does. The group is the evidence unit. The 2026-08-19 production JPEG was retained and both plate-detection and plate-recognition ran, but the partial suggestion was below threshold; Pegasus left it in `Needs sorting` with no Image Intake, association, or Image-initiated Case.

Today the engine also collapses “no plate detected” and “plate detected but unreadable” into `NoReadableResult`, so production evidence cannot say which recognition layer abstained. Both the third terminal path and the diagnostic ambiguity must be removed.


## Product model and documentation scope

Pegasus has two Case-origin types:

1. **Instruction-initiated Case** — the main/formal type. It begins with an official instruction document (PDF, Word, or equivalent accepted instruction), uses the existing Principal and Case/PO identity rules, and may initially have no images.
2. **Image-initiated Case** — a secondary/pre-instruction type. It begins with retained vehicle images before formal instructions are received. It has no Case/PO because the Principal and formal instruction may be unknown. Its immutable reference is the VRM plus a per-VRM sequence, e.g. `AB12ABC-01`, then `AB12ABC-02`, `AB12ABC-03`, without reuse. It remains distinct from the later Instruction-initiated Case; VRM matching links the two when exactly one eligible instruction Case matches without overlap, preserving both origins and history.

INTK-006 includes the governing-document reconciliation required to make this model canonical and conflict-free. Amend the operator notes, PRD, FRD-01, FRD-02, FRD-06, FRD-12, design glossary/surfaces, capabilities registry, and `CONTEXT.md`; update or supersede any ADR wording that currently states image-led work must remain pre-Case. The docs must state that Instruction-initiated Cases may lack images initially, Image-initiated Cases have no Case/PO, and the VRM-based Image-initiated reference sequence is separate from Case/PO, Audit, and Unidentified references.

## Verification
- Fixtures cover groups with one readable VRM plus unreadable damage close-ups, one unique existing-case match, no match, ambiguous/conflicting reads, low-confidence reads, and no-readable results.
- One confident, unambiguous group VRM with one eligible case match associates every group image to that case.
- Every other accepted group creates one Image-initiated Case containing every group image.
- Conflicting evidence fails closed against existing cases and is kept together in the Image-initiated Case.
- No group member terminates as an unrelated generic `Needs sorting` item.
- The status/result surface identifies the resulting existing or newly created case for the whole group.
- Non-sensitive diagnostics distinguish at least: detector found no plate; detector found a crop but recognition returned no usable registration; usable suggestion produced; technical failure.
- Diagnostics prove whether each layer ran without logging source-image content or creating a second business-decision taxonomy.

## Outcome
