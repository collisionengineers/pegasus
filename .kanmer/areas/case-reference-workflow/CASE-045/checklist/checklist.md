# Checklist — CASE-045

- [ ] Step 1: Add the nullable record/detail/summary principal contract and the staff `SetPrincipalAsync` request (expected-version only, no operation key), reusing `ImageIntakeLifecycleRules`, the `IImageIntakeStore` default-member convention and the canonical `Principal` record; update the now-false `IImageIntakeStore` and `ImageIntakeDetail` summaries in the same diff.
- [ ] Step 2: Persist and bulk-project `ImageIntakeEntity.PrincipalId` with its restrictive FK/index; add the narrow active-principal options query (`IsActive`, ordered by `Code`) because the administration surface is gated behind `ManageOrganizationsAndPrincipals`; write no lifecycle event, reject a stale version, and add no matching, case creation or queue N+1 read.
- [ ] Step 3: Generate the nullable FK/index migration and designer/snapshot; update the applied-migrations assertion; record the no-new-grant evidence with file:line and prove the verbs with `AzureSqlRuntimeRoleMigrationTests`, not with `Test-MigrationGrants.ps1`; make no `Invoke-AzureDatabaseBootstrap.ps1` edit.
- [ ] Step 4: Add the detail-page Principal fact and active-principal select/POST handler, using `OperatorLabels.ImageIntakePrincipal` and `ImageIntakePrincipalNotKnown` in the CASE-045 block, with authorization, antiforgery and stale-write behaviour; assert the exact `Not known` and reject blank/`None`/`Unknown`/`Unassigned`.
- [ ] Step 5: Name CASE-042's exact row and quick-view outlets at merge prep, add the Principal fact there, and prove the Awaiting read count equals CASE-042's recorded baseline across several rows of mixed principal state (adding the interceptor hook to `IntakeWebTestSupport.cs` if needed).
- [ ] Step 6: Run the scoped Test UI snapshot capture for `/Cases` and `/VehicleImages/{id:guid}`, commit the `docs/design/test-ui/**` delta, and record each file's byte size, doctype and expected markers.
- [ ] Run `pwsh ./scripts/Test-MigrationGrants.ps1` (smoke only — not proof of the grants).
- [ ] Run `dotnet restore ./Pegasus.slnx --locked-mode`.
- [ ] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [ ] Run `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build`.
- [ ] Run `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build`.
- [ ] Run only the changed integration classes: `ImageIntakePersistenceTests`, `ImageIntakeWebTests`, `TriageQueuesWebTests`, `IntakePersistenceIntegrationTests`, `AzureSqlRuntimeRoleMigrationTests`.
- [ ] Run `./scripts/Update-TestUiSnapshots.ps1 -Verify` and `./scripts/Test-UiCatalogue.ps1`.
- [ ] Write the post-implementation report, including exact command outcomes, dependency heads, migration rollback/backfill statement, the grant/census evidence, and the snapshot artifact record.
- [ ] Open the PR targeting `dev` with `Kanmer: CASE-045`.
