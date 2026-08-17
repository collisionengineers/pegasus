# Files — <ticket id>

*The files document. Not the research — this is the **surface area** of the change, not the findings behind it.*

Surveyed BEFORE planning. Two tables, and the second is the one that earns its
keep.

## Where the change lands

What this ticket will modify, and why each file is in scope.

| Path | Why |
|---|---|
| `src/…` | what happens to it, and what could break |

## Context files

What an implementer must **read** to avoid a trap — files they will not
necessarily edit. Say what each one tells them, not just that it is relevant; a
bare path is a reading list, and a reading list is not context.

| Path | What it tells the implementer |
|---|---|
| `src/…` | the constraint, gotcha or precedent that lives here |

## Ripple effects

Callers, tests, documentation, and committed build artifacts that follow from
the change above.

## Out of scope

What this ticket deliberately does not touch, so the reviewer knows it was a
decision rather than an oversight.
