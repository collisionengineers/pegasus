# Plan — PR-015

## Approach
Use `TryAddSingleton` for the unavailable fallback so existing explicit production registration wins by normal DI semantics. Add one composition test for fallback and production resolution. Estimate: 2 files, under 50 lines.

## Governing docs
FRD-08 requires a real read-only Deleted Items caller; this makes the already-designed Graph source reachable without changing behavior or permissions.

## Steps
1. Correct fallback registration and prove both compositions.
2. Run focused composition/build checks and the four-lens pass.

## Simplification pass — 2026-08-20

- Reuse: applied — the existing production registration remains the sole real source; Infrastructure now uses the existing `TryAdd` convention for its fallback.
- Simplification: no extra factory, flag, or wrapper introduced.
- Efficiency: one DI resolution path; no runtime branching added.
- Altitude: composition remains in Infrastructure and is proved at the production registration boundary.
