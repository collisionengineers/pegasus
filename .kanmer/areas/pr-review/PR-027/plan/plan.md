# Plan — PR-027

## Approach

Extend the existing focused test files and canonical runtime-role migration test. Reuse current Core/LocalDB/Web support; add no parallel test infrastructure.

## Steps

1. Add Core tests for management/list authorization, invalid input, internal-id Active/Disabled behavior and resolver actor authorization.
2. Expand persistence coverage for stale version, conflicting operation-key reuse, competing updates, exact immutable history and retained disabled row.
3. Expand authenticated Web coverage for add/replay, validation, update/disable, stale conflict and denied POST.
4. Add one exact migration-permission test for Web SELECT/INSERT/UPDATE, Web DELETE denial, and zero Worker permissions.
5. Run focused Core/integration lanes and four simplicity lenses; update evidence and push.

## Governing docs

- FRD-08: tests prove the exact configured Active-name boundary and permanent history.
- FRD-12: tests prove the Administrator page and denied access states.
No governing behavior changes are planned.

## Verification

Focused Core filter; focused catalogue persistence/Web/runtime-role tests; locked Release build; grant script; diff check.

## Risks

Relational concurrency can deadlock under shared LocalDB. Run the focused lane without another full suite and assert the stable business outcome rather than provider timing.

## Simplification pass — 2026-08-20

- Reuse: extended the existing Core fake, LocalDB factory, authenticated Web driver, ActionHistory queries and runtime-role permission reader.
- Simplification: added no test framework or operation table. One row-scoped SQL Server lock fixes the race exposed by the concurrency test.
- Efficiency: the lock is scoped to one category id; all verification filters are catalogue-specific.
- Altitude: authorization/validation remain Core, transaction serialization remains Infrastructure, and Web tests exercise the thin page.
- Applied finding: simplified the Web form helper to accept parsed values directly instead of manufacturing an HTML tag.
- No unapplied findings.
