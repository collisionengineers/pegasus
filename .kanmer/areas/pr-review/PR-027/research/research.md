# Research — PR-027

## Question

Which acceptance claims in MAIL-004 lack direct evidence?

## Findings

- Core tests cover normalization, one management denial, and Active/absent resolution, but not list denial, invalid inputs, empty-id resolution, or resolver actor denial.
- Persistence tests cover sequential replay, duplicate name, disable and row retention, but not stale version, conflicting operation-key reuse, exact before/after history, or a competing update outcome.
- Web tests cover GET denial and one add, but not denied POST, validation, disable, stale conflict, or exact replay/recovery.
- `AzureSqlRuntimeRoleMigrationTests` is the canonical relational permission reader; `Test-MigrationGrants.ps1` proves only that a created table has some grant and cannot prove the exact role matrix.
- The production seams already expose every required behavior. Tests should reuse current Core use cases, LocalDB support, authenticated Web driver and runtime-role permission queries.

## Implication

Add focused tests first. Change production code only if those tests expose a correctness defect.
