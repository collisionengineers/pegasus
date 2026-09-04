# Checklist — CASE-045

- [ ] Step 1: Add the nullable record/detail/summary principal contract and staff assignment validation, reusing `ImageIntakeLifecycleRules` and existing `IImageIntakeStore` default-member conventions.
- [ ] Step 2: Persist and bulk-project `ImageIntakeEntity.PrincipalId`; implement active-option lookup and replay-safe staff assignment without matching, case creation, or queue N+1 reads.
- [ ] Step 3: Generate the nullable FK/index migration and designer/snapshot; update the applied-migrations assertion and record the verified no-new-grant/bootstrap-census result.
- [ ] Step 4: Add the detail-page Principal fact and active-principal select/POST handler, with exact `Not known`, authorization, antiforgery, replay, and stale-write behavior.
- [ ] Step 5: Extend CASE-042's Awaiting `ImageRow` facts and quick view; prove known/unknown display and unchanged reader-command count.
- [ ] Run `pwsh ./scripts/Test-MigrationGrants.ps1`.
- [ ] Run `dotnet restore ./Pegasus.slnx --locked-mode`.
- [ ] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [ ] Run `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build`.
- [ ] Run `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build`.
- [ ] Run only the four changed integration classes: `ImageIntakePersistenceTests`, `ImageIntakeWebTests`, `TriageQueuesWebTests`, and `IntakePersistenceIntegrationTests`.
- [ ] Write the post-implementation report, including exact command outcomes, dependency heads, migration rollback/backfill statement, and grant/census review.
- [ ] Open the PR targeting `dev` with `Kanmer: CASE-045`.
