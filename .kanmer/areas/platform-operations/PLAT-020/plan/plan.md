# Approach

Repair the two proven permission drifts in one append-only runtime-role migration because both are the same production least-privilege reconciliation. Preserve the existing role model and denial set. Narrow automatic lookup's exception filter using SQL Server error numbers so concurrency remains idempotent and operational failures surface.

# Steps

1. Add a new migration granting Worker INSERT on VehicleLookupRequests and Web/Worker UPDATE on ImageIntakes; explicitly retain DELETE denials.
2. Update exhaustive expected grant sets and add assertions that unrelated permissions are unchanged.
3. Add a reusable local predicate only at the persistence boundary to recognize SqlException 2601/2627 beneath DbUpdateException, and use it in EnqueueDueAsync.
4. Add focused tests for duplicate suppression, non-duplicate propagation, and runtime-role execution of automatic lookup and image lifecycle/custody updates.
5. Run locked restore/build and focused/full non-corpus tests; perform the required simplification review.
6. Open a PR to dev. After review/merge, request exact production migration and replay approvals; do not perform those writes beforehand.
7. Following approval, migrate, verify grants, recover the named backlog, observe two sweeps, and refresh current-state docs.

# Governing docs

- FRD-05: restores truthful image-custody lifecycle transitions.
- FRD-06: restores automatic lookup evidence gathering without changing acceptance hierarchy.
- ADR-0007: keeps deployment and recovery behind exact-target approval.

# Risks and mitigations

- Excess privilege: assert complete effective grant lists and DELETE denials.
- Duplicate race regression: retain only SQL 2601/2627 suppression.
- Recovery duplication: use existing idempotent operation/work identifiers and verify exact targets before replay.
- Production proof unavailable before approval: merge code proof separately; keep deployment/recovery unfinished until authorized.

## Simplification pass — 2026-08-21

Reuse: preserved PR #493's vehicle grant and duplicate filter rather than duplicating them. Simplification: one SQL-only migration with two exact grants. Efficiency: no runtime path changed. Altitude: no new role abstraction. No unapplied findings.
