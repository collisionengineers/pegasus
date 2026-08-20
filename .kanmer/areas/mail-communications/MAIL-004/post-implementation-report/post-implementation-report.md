# Post-implementation report — MAIL-004

## Summary

Implemented the smallest global approved Outlook category catalogue. Administrators maintain exact display names as Active/Disabled; MAIL-13's Core seam accepts an internal id and reloads only the current Active server-owned name. Saves are versioned, reasoned, replay-safe, same-category concurrent operations are serialized, and ActionHistory preserves exact before/after state. No Graph metadata/synchronization, message mutation, search/linking, generic rules editor, deployment or external write was added.

## Exact final file inventory — 24 paths

| Path | Rationale |
|---|---|
| `docs/capabilities.md` | Records the operator-activated narrow local prerequisite and its undelivered/live boundaries. |
| `docs/design/README.md` | Specifies the existing Administration pattern, alternatives, independent review and remaining manual visual gate. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Owns exact Active/Disabled catalogue and internal-id resolution behavior. |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1` | Adds the catalogue table to the canonical exact Web grant/DELETE-denial matrix. |
| `src/Pegasus.Core/Identity/StaffAuthorization.cs` | Adds the named Administrator-only catalogue management right. |
| `src/Pegasus.Core/Intake/ApprovedOutlookCategories.cs` | Defines the one Core catalogue vocabulary, management use cases and Active-only MAIL-13 resolver. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Registers the existing Core ports/use cases with the EF implementation. |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyEntities.cs` | Adds the narrow persisted category entity. |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyModelConfiguration.cs` | Configures table, lengths and unique normalized display name. |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedOutlookCategoryStore.cs` | Implements list/update/resolve, replay/history/versioning and same-category transaction serialization. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260820114412_ApprovedOutlookCategoryCatalogue.Designer.cs` | Generated EF migration model metadata. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260820114412_ApprovedOutlookCategoryCatalogue.cs` | Creates the table/index and exact Web grants with DELETE denied. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Updates the generated current EF schema snapshot. |
| `src/Pegasus.Web/Pages/Administration/Index.cshtml` | Adds one existing-pattern Administrator navigation card. |
| `src/Pegasus.Web/Pages/Administration/MailCategories.cshtml` | Renders the dedicated names/state/reason form without Graph metadata. |
| `src/Pegasus.Web/Pages/Administration/MailCategories.cshtml.cs` | Posts internal id/version/operation key through Core and maps safe validation/conflict states. |
| `tests/Pegasus.Core.Tests/Identity/AutomationActorTests.cs` | Keeps the canonical Automation-right denial inventory exhaustive. |
| `tests/Pegasus.Core.Tests/Intake/ApprovedOutlookCategoryTests.cs` | Proves management/list authorization, validation and Active-only resolver authorization. |
| `tests/Pegasus.IntegrationTests/AdministrationSearchAccountWebTests.cs` | Keeps canonical route/render/antiforgery/role inventories exhaustive. |
| `tests/Pegasus.IntegrationTests/ApprovedOutlookCategoryAdministrationWebTests.cs` | Proves authenticated add/replay/disable/validation/conflict and denied GET/POST behavior. |
| `tests/Pegasus.IntegrationTests/ApprovedOutlookCategoryPersistenceTests.cs` | Proves uniqueness, replay/concurrency, conflicts, exact history and disable-not-delete. |
| `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | Proves exact Web grants, Web DELETE denial and no Worker permission. |
| `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` | Adds the route to the canonical authenticated accessibility inventory. |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | Keeps committed migration and current schema-table fixtures exhaustive. |

## Governing docs

FRD-08 owns the catalogue behavior and FRD-12 owns the Administrator UX boundary. Design records the authorized narrow local route and its remaining manual visual gate. Capabilities preserves MAIL-13's Next allocation and explicitly keeps message mutation, Graph permission/validation, deployment, live evidence and operator release acceptance undelivered. No ADR is required.

## Exact verification evidence

Current reviewed-head evidence:

- Release solution build: pass, 0 warnings/errors.
- Core filter `FullyQualifiedName~ApprovedOutlookCategoryTests`: 8/8 pass.
- Integration filter `FullyQualifiedName~ApprovedOutlookCategory|FullyQualifiedName~LatestMigrationGivesOnlyWebExactCategoryCataloguePermissions`: 8/8 pass.
- Web-only category filter: 5/5 pass.
- Azure deployment-plan Local mode: pass.
- Migration-grant script: 59 migration files pass.
- Documentation links: 192 files pass.
- Markdown placement for `origin/dev..HEAD` and `git diff --check`: pass.

Earlier unchanged-suite evidence on this branch before the blocker additions: all Core 831/831, Architecture 98/98, and authenticated accessibility 22/22 passed. Those counts are not represented as a rerun of the final head.

## Outstanding evidence and exclusions

PR-026's required rendered desktop/200%-zoom manual visual inspection is not complete: the dedicated authenticated local app was prepared, but the in-app Browser runtime exposed no browser instance. This is the sole known review blocker and is not claimed as passed.

No live Outlook/Azure check, deployment, Graph permission/validation, message mutation, search/linking, category color/id synchronization or operator release acceptance is required or authorized by this implementation. TICK-054 remains the message-action owner and must preserve unrelated Outlook categories.
