# Files

| File | Change |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<stamp>_GrantWorkerCaseDocuments.cs` | **New.** Grants `pegasus_worker_runtime_role` the three document-table permissions it has always been missing, mirroring `20260821095500_GrantWorkerVehicleLookupRequests` exactly. |
| `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | One `[Fact]` asserting the Worker's exact permission set on `CaseDocuments`, `DocumentVersions` and `DocumentOccurrences` after migrating, in the shape of `LatestMigrationGrantsWorkerAutomaticVehicleLookupInsert`. |

## Reused, not written

- `20260821095500_GrantWorkerVehicleLookupRequests` — the migration shape,
  including `IsSqlServer()`, `RequireWorkerRole()` and the symmetric `Down`.
  This is the third instance of that shape, which is the codebase's established
  way to patch the least-privilege baseline additively.
- `20260729199000_RuntimeRoleReconciliation.WebGrants` — the permission strings
  are copied verbatim from the Web role's entries for the same three tables, so
  no new permission vocabulary is invented.
- `AzureSqlRuntimeRoleMigrationTests.ReadGrantedPermissionsAsync` /
  `ReadDeniedDeleteTablesAsync` — existing helpers; the test adds no fixture.

## Deliberately not changed

`EfQueuedCustodyProcessor` and `EfDocumentCustodyStore` are correct. The code
writes the rows the design calls for; the database refuses them. Changing the
application to work around a missing grant would be the wrong repair.

`20260729199000_RuntimeRoleReconciliation` is **not** edited. It is applied
history — its `WorkerGrants` array records what that migration granted, and
rewriting it would make the migration lie about the estate it produced.
