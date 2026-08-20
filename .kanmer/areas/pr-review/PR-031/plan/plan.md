# Plan

Estimated diff: three existing files, under 90 lines.

1. Add Azure Identity's established authentication exception to the existing Deleted external-boundary catch without changing cancellation handling.
2. Add a throwing credential test and drive that real source through authenticated `/Inbox` to the existing unavailable rendering.
3. Run focused Graph/Web checks and four-lens/PIR updates.

## Governing docs

FRD-08's fail-closed unavailable state covers recoverable token acquisition without inventing a new error model or swallowing caller cancellation.

## Simplification pass — 2026-08-20

- Reuse: Azure Identity's established exception and the existing Deleted unavailable state/catch policy are used directly.
- Simplification: one catch alternative; no exception taxonomy, wrapper, retry, or flag.
- Efficiency: authentication failure stops before HTTP and renders through the existing caller state.
- Altitude: credential mapping stays at the external boundary; Web wording is unchanged.
