# Post-implementation report — PR-027

## Summary

Completed the promised Core, persistence, authenticated Web and relational-permission evidence. A focused concurrency test exposed and fixed one correctness defect: operation history was read before same-category serialization. The store now takes the existing SQL Server row/key-range update lock first, so same-key concurrent retries replay and competing expected versions yield one commit plus a domain version conflict.

## Changed files

- `src/Pegasus.Infrastructure/Persistence/EfApprovedOutlookCategoryStore.cs`: serialize one category before reading operation history.
- `tests/Pegasus.Core.Tests/Intake/ApprovedOutlookCategoryTests.cs`: list/update authorization, text/policy validation, Active/Disabled/empty-id resolver and system-actor denial.
- `tests/Pegasus.IntegrationTests/ApprovedOutlookCategoryPersistenceTests.cs`: replay/operation/version/duplicate conflicts, concurrent idempotence/competition, exact before/after history and retained disabled row.
- `tests/Pegasus.IntegrationTests/ApprovedOutlookCategoryAdministrationWebTests.cs`: denied GET/POST, add replay, disable, validation, stale and operation conflicts.
- `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs`: exact Web SELECT/INSERT/UPDATE plus DELETE denial and zero Worker permission.

## Governing docs

The evidence directly proves FRD-08's Active exact-name and permanent-history boundary and FRD-12's Administrator-only route. No behavior outside the accepted catalogue changed.

## Verification

- Release solution build: pass, 0 warnings/errors.
- Core filter `FullyQualifiedName~ApprovedOutlookCategoryTests`: 8/8 pass.
- Integration filter `FullyQualifiedName~ApprovedOutlookCategory|FullyQualifiedName~LatestMigrationGivesOnlyWebExactCategoryCataloguePermissions`: 8/8 pass.
- Web-only filter `FullyQualifiedName~ApprovedOutlookCategoryAdministrationWebTests`: 5/5 pass.
- `Test-AzureDeploymentPlan.ps1 -Mode Local`: pass.
- `Test-MigrationGrants.ps1`: 59 migrations pass.
- `Test-DocumentationLinks.ps1`: 192 files pass.
- Markdown placement and `git diff --check`: pass.

No external or live Outlook/Azure write ran. Commit `0b112237`, PR #473.
