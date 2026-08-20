# Plan — PR-014

Estimated repository diff: 2 docs, under 20 lines.

1. Amend the MAIL-23 capability row to distinguish locally activated implementation from deployment/live evidence and from MAIL-05/06/07.
2. Amend the design deferred boundary with the same narrow local administrator-only exception, leaving ordinary staff/message controls deferred.
3. Update TICK-064 plan/PIR inventory, run documentation checks/diff hygiene and four lenses, commit/push PR #468, write PR-014 PIR, move to Review.

No abstraction or new behavior is introduced.

## Simplification pass — 2026-08-20

- Reuse: amended the existing capability row and deferred UI boundary; no new document or duplicate state.
- Simplification: one narrow exception, not a broad reclassification of Next email work.
- Efficiency: docs-only; no runtime cost.
- Altitude: capability inventory owns activation/evidence state; design owns visible-surface boundary; FRD behavior remains unchanged.

No unapplied findings.
