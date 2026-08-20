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

## Final completion plan

Estimated incremental diff: five existing files, about 120 lines.

1. Extend the existing search projection call with the already-computed route decision; normalize its root via `StaffForwardBodyCleaner`, choosing the existing attached-original source label where applicable.
2. Make retained SQL body search and match labeling use that root row, and render the same text in detail; keep cleaned retained-body fallback only for receipts without a root projection.
3. Prove normalized root search/display equality and preserve attachment search.
4. Run focused verification and final four-lens/PIR updates.

## Governing docs

FRD-08 gets a one-to-one visible body match from the existing receipt-owned projection. The no-reconstruction boundary remains intact.

## Final simplification pass — 2026-08-20

- Reuse: the receipt-owned root document, route decision, `StaffForwardBodyCleaner`, and existing EF match mapper now own both search and visible detail body.
- Simplification: removed raw retained body from search admission instead of adding a normalized column/table/backfill.
- Efficiency: root and attachment predicates remain in SQL before count/paging; detail loads one existing root row.
- Altitude: Core creates normalized projection policy, Infrastructure persists/queries it, and Web only renders the result.
