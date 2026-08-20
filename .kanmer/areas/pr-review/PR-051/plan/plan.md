# Plan

## Governing docs

FRD-08 continues to require exact-message reasoned association and fail-closed identity. No governing-doc or ADR change.

1. Add exact message id, server-derived receipt id and Link/Unlink intent to the existing protected TempData payload.
2. Validate the full prepared payload against route, resolved receipt, action and submitted authority before calling Core.
3. Treat mismatch as definitive and reuse existing lease compensation; retain matching state after success so exact replay still reaches Core.
4. Make final dialog rendering action-specific.
5. Add exact authenticated cross-message, Link→Unlink and Unlink→Link proofs and rerun replay regressions.

No new store, framework, schema or policy owner.

## Simplification pass — 2026-08-20

- **Reuse:** extended the existing protected TempData authority and existing release compensation; Core link/unlink fingerprinting remains the replay owner.
- **Simplification:** one delimited payload and one exact comparison method serve the two existing concrete handlers; no new record, store, middleware, filter or generic action framework.
- **Efficiency:** the final POST performs the already-required exact message→receipt lookup, then compares scalar authority before Core.
- **Altitude:** Web binds confirmation authority; Core remains the mutation, conflict, replay and history owner.
- **Applied findings:** changed TempData reading to protected Peek plus explicit clear so an unauthorized request cannot consume valid authority before the authenticated retry.
- **Unapplied findings:** none.
