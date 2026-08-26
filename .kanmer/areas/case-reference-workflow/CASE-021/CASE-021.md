---
id: CASE-021
type: ticket
title: >-
  Refuse Review for a case with no images instead of asserting its images are
  complete
status: done
area: case-reference-workflow
order: 625
assignee: claude-code
profile: fix
stageEntered:
  review: '2026-08-24T11:10:25.512Z'
  verifying: '2026-08-24T14:57:04.495Z'
  done: '2026-08-26T14:37:28.882Z'
taken_at: '2026-08-24T08:53:04.274Z'
branch: task/case-021-observed-images
worktree: ../pegasus-worktrees/case-021-observed-images
labels:
  - qdos26013
  - production-defect
  - found-during-qa
  - readiness
links: []
commits:
  - e03eb81d
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/528'
deployment: production
archived: false
created: '2026-08-23T15:18:20.008Z'
updated: '2026-08-26T14:37:28.882Z'
---

## What the operator saw

`a.QDOS26013` — an audit instruction with an original report and **no
photographs** — was created straight into **Review**. Operator, 2026-08-23:

> *"This went into review despite lacking images. Review is for cases ready to
> pass to engineer. Lacking images should keep the case in 'Not Ready'. Export
> didn't work due to lacking images (this is correct, but the case shouldn't be
> in review if export doesn't work for it). Images are an EVA requirement /
> Report Requirement."*

That is exactly right, and the two halves are already consistent in Core — the
export refuses on `EvaHandoffPolicy.NoRetainedImagesReason`. Only the readiness
gate disagrees.

## Root cause

`AllocateIntake.AutomaticCompleteness` (`IntakeAllocation.cs:225`) is a **static
constant**:

```csharp
private static readonly CaseCompleteness AutomaticCompleteness =
    new(InstructionComplete: true,
        ImagesComplete: true,          // <- asserted, never observed
        InstructionConfirmedByStaff: false,
        ImagesConfirmedByStaff: false);
```

Every automatically created case is born claiming complete images, whatever
arrived. Confirmed in production — `Cases` for `a.QDOS26013`:
`ImagesComplete = True`, and `DocumentOccurrences` for that case holds exactly
three rows, none of them an `Image`:

| Ordinal | Role | File |
| ---: | --- | --- |
| 1 | OriginalSource | `…​.eml` |
| 2 | Instruction | `49378_1_LtrtoAuditEngin.pdf` |
| 3 | Instruction | `Bodyshopreport119508-V1.pdf` |

`QDOS26014`, forwarded seconds later *with* images, carries the same
`ImagesComplete = True` — so the flag distinguishes nothing.

## How it got here

The comment above the constant records it: CASE-013 changed all four fields
from `false` to `true` because every automatic case was born "details
incomplete" and could never reach Review. That fixed a real problem and
overshot — it replaced *always false* with *always true* rather than with
*observed*.

## Shape of the fix

Derive `ImagesComplete` from what the receipt actually retained, not from a
constant. `InstructionEvidenceImages.Select` is already the single Core owner
of "which assets count as this case's photographs" — custody uses it to decide
what to retain, so allocation asking the same question keeps one rule rather
than inventing a second definition of "has images".

`InstructionComplete: true` is defensible as-is: the receipt reached
`CaseCreated` only because a definitive authorised instruction was identified.
It is the *images* half that is unobserved.

## Watch for

- An audit with no photographs is a legitimate shape of work — it must still be
  **creatable**, just not **Review-ready**. The fix is a readiness gate, not a
  refusal to allocate.
- `CaseLifecycleRules` already reads `ImagesComplete` for the Engineer-assignment
  gate (`CaseLifecycle.cs:555,575`), so correcting the flag moves more than one
  surface. That is the point, but it wants checking rather than assuming.
