# Checklist

## Core
- [ ] `EvaBundleSchema.cs`: delete the hand-off request/result/port/policy types
- [ ] `EvaBundleSchema.cs`: drop `Revision` from `EvaHandoffProxyRequest`
- [ ] `EvaBundleSchema.cs`: F3 — `ValidateSource` keeps every throw, stops rebuilding the provenance array
- [ ] `CaseEvaMapping.cs`: delete `MapForProduction`, `ValidateAcceptedEvidence`, `EvaMappingResult`, `IsResolved`
- [ ] `CaseEvaMapping.cs`: F2 — delete `EvaEvidenceStatus.Corrected`
- [ ] `CaseEvaMapping.cs`: F1 — correct the `ExportDateSource` comment
- [ ] `CaseQueries.cs`: drop `EvaHandoff` and the `IEvaHandoffQueries` dependency
- [ ] `AssessmentReportProjection.cs`: comment names a deleted type

## Infrastructure
- [ ] `EvaHandoffStore.cs`: delete every hand-off method and orphaned helper
- [ ] `EvaHandoffStore.cs`: export records the proxy once per case, after the bundle exists
- [ ] `EvaHandoffStore.cs`: the delivery/assignment-claim rejection moves across verbatim
- [ ] `EvaHandoffEntities.cs`: three entities deleted; proxy loses `RevisionId` and `OperationKey`
- [ ] `EvaHandoffModelConfiguration.cs`: three configs deleted; proxy FK + `RevisionId` index dropped
- [ ] Both `CK_EvaFirstHandoffProxies_*` constraints still declared and still hold
- [ ] `PegasusDbContext.cs`: three `DbSet`s deleted
- [ ] `LocalEvaHandoffProxy.cs`: `Revision` guard removed
- [ ] `DependencyInjection.cs`: four registrations removed

## Web
- [ ] `Pages/Cases/Eva/` deleted
- [ ] `Export.cshtml.cs`: `OnGetAsync` → named `OnPostBundleAsync`
- [ ] `Details.cshtml`: anchor → form post, reusing the `ClaimLease` shape
- [ ] No new operator-facing copy (`docs/design/README.md:422-445`)
- [ ] `Vehicle.cshtml.cs`: handler and two constructor parameters deleted
- [ ] `_CaseWorkflow.cshtml`: EVA panel deleted
- [ ] `AssessmentMcpTools.cs`: two tools, four records, two constructor parameters deleted

## Schema
- [ ] `dotnet ef migrations add DropEvaHandoffTables`
- [ ] `Up()` drops FK → index → columns → three tables, child-first
- [ ] `Down()` restores all of it, empty
- [ ] No historic `*.Designer.cs` modified (`git diff --stat` check)
- [ ] `Invoke-AzureDatabaseBootstrap.ps1`: migration added to `$removedTables`
- [ ] `Invoke-AzureDatabaseBootstrap.ps1`: still contains the string `20260819180000_GrantEvaHandoffDownloadOperations`
- [ ] `Test-MigrationGrants.ps1` passes
- [ ] `Test-AzureDeploymentPlan.ps1 -Mode Local` grant-migration guard passes

## Tests
- [ ] `EvaHandoffPersistenceTests.cs` deleted
- [ ] `CustodyOutboxIntegrationTests.cs`: proxy assertion **inverted**
- [ ] A second export of the same case records no second proxy row
- [ ] `CaseWorkflowMigrationTests.cs`: `EvaHandoffDownloadOperations` now absent
- [ ] `IntakePersistenceIntegrationTests.cs`: migration census extended
- [ ] `CaseDetailsWebTests.cs`: hand-off routes 404
- [ ] `CaseVehicleWebTests.cs`, `ProductionCompositionTests.cs`, `ReadinessEndpointTests.cs` updated
- [ ] `DependencyDirectionTests.cs`: Eva assertions rewritten around survivors
- [ ] `EvaHandoffPolicyTests.cs`, `EvaBundleContractTests.cs` updated
- [ ] `AzureSqlRuntimeRoleMigrationTests.cs` **unchanged** (pinned historic)

## Docs
- [ ] FRD-07 `:35-38`, `:42`
- [ ] FRD-07: **neither `###` heading renamed** (anchor check)
- [ ] `capabilities.md`: EXT-03, CASE-21, CASE-30, MCP-06
- [ ] `current-architecture.md`: `:142`, `:514`, `:526` (incl. F6), `:634`
- [ ] `infra/modules/platform.bicep`: F1 comment
- [ ] `docs/operations.md` **unchanged**

## Verification (from the ticket)
- [ ] One route produces the package; the hand-off routes 404
- [ ] Export is a POST with antiforgery; a refresh does not double-record
- [ ] First export records exactly one proxy row; the second records none
- [ ] The dashboard "sent to engineer" count still works, fed by export
- [ ] The proxy still cannot claim delivery or Engineer assignment
- [ ] Package bytes unchanged from ENG-014/ENG-015
- [ ] Dropped tables leave no orphaned FK, grant, or migration-guard failure
- [ ] `dotnet build --configuration Release`
- [ ] `dotnet test` Core + Architecture
- [ ] `dotnet test` integration, chunked
- [ ] Migration up → down → up clean
- [ ] Byte audit: no stray CR introduced by any edit

## Close
- [ ] Simplification pass recorded in the plan under a dated heading
- [ ] Commits in small slices, co-author trailer
- [ ] PR `--base task/eng-015-eva-field-values`, stacking stated
- [ ] `post-implementation-report`, then `move_item` to `review`
- [ ] CI green
