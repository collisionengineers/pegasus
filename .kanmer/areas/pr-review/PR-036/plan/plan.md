# Plan

Estimated diff: about 4 migration/bootstrap edits, 35–50 focused SQL-test lines, and one current-state sentence.

1. Remove UPDATE from the unmerged migration's Up/Down permission lists and the bootstrap expected matrix.
2. Add exact migrated-database permission proof for Web SELECT and Worker SELECT/INSERT/DELETE, with UPDATE absent.
3. Reconcile `docs/current-architecture.md` with the exact caller-backed matrix.
4. Run migration-grant and deployment-plan scripts, the focused SQL test, receipt-store focused tests, Release build, and diff check.
5. Record four-lens dispositions and PIR.

Reuse: the existing migration stream, bootstrap permission census, SQL test harness, and current architecture snapshot. No follow-up migration or permission framework.

## Simplification pass — 2026-08-20

- **Reuse:** Reused the unmerged feature migration, bootstrap permission census, established SQL permission reader, and current architecture snapshot.
- **Simplification:** Removed the unsupported verb directly; no follow-up migration or permission abstraction is necessary before merge.
- **Efficiency:** Runtime store behavior is unchanged; the Worker keeps only the SELECT/DELETE/INSERT operations its replacement path performs.
- **Altitude:** Schema permissions remain in the migration/bootstrap owners and current-state truth remains in `docs/current-architecture.md`. No unapplied findings.
