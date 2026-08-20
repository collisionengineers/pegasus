# Research

## Verified premises

- `EfIntakeReceiptStore.ReplaceSearchDocuments` reads the receipt-owned children, removes them, and inserts replacement rows; there is no UPDATE caller.
- The unmerged feature migration grants Worker SELECT/INSERT/UPDATE/DELETE and the bootstrap matrix mirrors that overbroad set.
- The existing migration-grant script checks that new tables receive a grant but not exact per-table verbs; `AzureSqlRuntimeRoleMigrationTests` is the established exact SQL permission evidence owner.
- `docs/current-architecture.md` states only the Web projection permission, so the as-built Worker claim is incomplete.

## Assumptions

- None. Store operations, migration SQL, bootstrap matrix, tests, and current-state documentation were checked directly.
