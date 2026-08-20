# Plan

Estimated diff: two existing files, under 25 lines.

1. Reuse the retained `BodyPlainText` predicate as the sole retained body-search owner and restrict the existing receipt projection predicate to named attachment rows.
2. Extend the existing persistence search test with a root-only term absent from displayed body and prove it returns no row; keep existing body/filename/content match proofs green.
3. Run the focused owning tests and four-lens simplification pass; update PIR/traceability.

## Governing docs

FRD-08 requires every result to show the matching location. This keeps admission and visible evidence one-to-one without changing product scope.

## Simplification pass — 2026-08-20

- Reuse: retained BodyPlainText remains the sole displayed body-search owner; the existing receipt projection remains the attachment-content owner.
- Simplification: one predicate qualifier aligns admission with existing match labeling; no new result type or reconciliation layer.
- Efficiency: the restriction remains inside the SQL predicate before count and paging.
- Altitude: the change is confined to persistence query behavior and its owning integration test.
