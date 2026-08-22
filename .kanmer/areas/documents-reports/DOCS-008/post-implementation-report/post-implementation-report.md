# Post-implementation report

Branch `task/docs-008-grant-worker-documents`, from `origin/dev` at `42125b34`.

## What changed

| File | |
| --- | --- |
| `Migrations/20260822044425_GrantWorkerCaseDocuments.cs` (+ Designer) | Grants `pegasus_worker_runtime_role` `SELECT, INSERT` on `CaseDocuments` and `DocumentOccurrences`, and `SELECT, INSERT, UPDATE` on `DocumentVersions`. `Down` revokes exactly those. |
| `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | `LatestMigrationGrantsWorkerTheCaseDocumentTables` — asserts the Worker's exact permission set per table and that DELETE stays denied. |

Two files, one of them generated. No application code changed, because none of
it was wrong.

## Verification

```
dotnet build --configuration Release          0 warnings, 0 errors
dotnet test … ~AzureSqlRuntimeRoleMigrationTests   12 passed, 0 failed
dotnet test … ~Custody                             77 passed, 0 failed
```

The negative case is proven from production rather than from a local red run,
which is stronger evidence here: the live `sys.database_permissions` dump taken
before this change shows the Worker holding **only** the DELETE deny on all
three tables. That is the state the new test now forbids.

## Simplification pass — 2026-08-22

Run over this branch's own diff (89 lines of migration, 27 of test).

| Lens | Finding | Disposition |
| --- | --- | --- |
| Reuse | The migration is the third copy of the `IsSqlServer` / `RequireWorkerRole` / symmetric-`Down` shape, after `20260821095500` and `20260821100623`. | **Not extracted.** Migrations are applied history and must stay self-contained — a shared base class would make an old migration's behaviour depend on code edited later. The duplication is the correct trade here, and the codebase has already made it twice. |
| Reuse | Permission strings taken verbatim from `RuntimeRoleReconciliation.WebGrants` rather than composed fresh. | Applied — one vocabulary for these tables. |
| Simplification | First draft wrote three separate `migrationBuilder.Sql` calls inline. | Applied — collapsed to one `Grants` table driving both `Up` and `Down`, so the two can no longer drift. |
| Efficiency | n/a — three DDL statements at migration time. | — |
| Altitude | Considered classifying `DbUpdateException` as transient so custody would retry. | **Rejected**, recorded in the plan: it would have retried a permanent failure forever and is wrong in general. |

One finding is deliberately **not** applied and is not silently dropped: no test
walks the Worker's `context.Set<T>()` call sites and checks each against the
grant matrix. That test would have caught this defect and both of its
predecessors. It is a different change from fixing the outage and needs a
dependable way to attribute a store to a runtime role, so it is filed as
[[PLAT-035]] rather than smuggled in here.

## Not yet proven

The acceptance condition — a live case reaching `CustodyState=confirmed` with
non-zero `CaseDocuments` — needs the migration applied to production and one
real instruction through the pipeline. That is the deploy's job, and it is what
`proof` will record.
