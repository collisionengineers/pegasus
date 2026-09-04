# Plan — CASE-045 (2026-09-04, gpt-5.6-terra high)

Starting state: CASE-032's branch is `ed0dc6a…` and is not merged; CASE-042 has no remote branch yet. Execute only after both merge, re-check their exact heads, and apply this as a delta to CASE-042's Awaiting row/quick-detail shape. D51 settles stored nullable `PrincipalId`; no matching or creation policy is open.

Governing constraints: FRD-02 keeps image records pre-Case until association; FRD-12 makes Awaiting instruction a dedicated Pre-Case queue with its own row shape. D51 requires the exact `Not known` display exception. No explanatory copy, no disabled/inert control, no new packages, one Core owner, and no sender/registration/case-association inference.

1. Extend the image-intake contract and assignment boundary.

   - Reuse `ImageIntakeLifecycleRules` for staff casework authorization, expected-version validation, and operation-key validation; reuse the default-member pattern already present on `IImageIntakeStore` so unrelated test fakes remain valid.
   - Touch:
     - `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`
     - `src/Pegasus.Core/ImageIntake/ImageIntakeLifecycle.cs`
     - `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeLifecycleTests.cs`
   - Add nullable `PrincipalId` to `ImageIntakeRecord`, projected optional principal code to `ImageIntakeSummary` and `ImageIntakeDetail`, and a small active-principal option shape.
   - Add a staff-only `SetPrincipalAsync` request that accepts either an active principal ID or `null` for `Not known`; it must carry the existing operation key and expected lifecycle version.
   - Preserve lifecycle and intake policy: no change to `IntakeDecisionPolicy`, registration, automatic association, or `RegisterImageIntakeRequest`. There is no current principal-authenticated image-intake route, so add none.
   - Acceptance: null remains valid; an empty non-null ID, invalid operation key, stale version, or non-staff actor is rejected before persistence.

2. Persist the optional relationship and project it in bulk.

   - Reuse `EfImageIntakeStore.ProjectAsync`, `ToDetailAsync`, `TransitionAsync`'s serializable replay/concurrency structure, and `PrincipalEntity` as the canonical source of the display code.
   - Touch:
     - `src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs`
     - `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`
     - `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs`
     - `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs`
   - Add nullable `ImageIntakeEntity.PrincipalId`, a restrictive FK to `Principals`, and the model index created for that FK.
   - Implement active-principal options and principal assignment in the existing store. Validate the selected ID is active at write time; preserve a previously recorded value even if that principal later becomes inactive.
   - Reuse `ImageIntakeLifecycleEvents` for idempotent assignment replay and optimistic concurrency; assignment must not transition lifecycle state, modify case association, or queue external work.
   - Add the principal code to the existing `ProjectAsync` SQL projection and detail read. Do not add per-row principal reads or a principal matcher.
   - Acceptance: assigned principal ID/code round-trips through record, detail, and summary; null round-trips unchanged; replay returns the committed result; a registration match or linked Case never supplies or overwrites this field.

3. Add the nullable schema migration and verify the existing permission census.

   - Reuse EF's generated migration/designer/snapshot workflow and `scripts/Test-MigrationGrants.ps1`.
   - Touch:
     - `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_ImageIntakePrincipal.cs`
     - `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_ImageIntakePrincipal.Designer.cs`
     - `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
     - `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
   - Generate after rebasing on the post-CASE-032/CASE-042 migration tail. The Up migration adds nullable `PrincipalId`, its FK/index; Down removes them. No backfill: existing rows remain null and display `Not known`.
   - Verified grant/census reuse (checked directly against `scripts/Invoke-AzureDatabaseBootstrap.ps1` and `Migrations/20260729199000_RuntimeRoleReconciliation.cs` during this planning pass): both runtime roles already hold `UPDATE` on `ImageIntakes` (bootstrap script lines 316-317, carried from the PLAT-020 lifecycle-state grant) and both already hold `SELECT` on `Principals` (`RuntimeRoleReconciliation.cs` lines 252 and 289, echoed in the bootstrap script's baseline matrix). Therefore no new grant or `Invoke-AzureDatabaseBootstrap.ps1` census change is needed for this column. Record that reviewed result — file:line evidence included — in the implementation report and prove it stays true with `Test-MigrationGrants.ps1`.
   - Add the generated migration ID to the chronological applied-migrations assertion and assert the nullable column/FK schema as appropriate.
   - Acceptance: migration applies cleanly, rollback is limited to dropping the optional value, and no runtime permission is widened.

4. Add the detail-page fact and staff assignment control.

   - Reuse `DetailsModel.OnPostCloseAsync` error/reload handling, `StaffPageModel.TryGetActor`, the detail page's definition-list markup, and `OperatorLabels` as the sole owner of operator text.
   - Touch:
     - `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml.cs`
     - `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml`
     - `src/Pegasus.Web/Presentation/OperatorLabels.cs`
     - `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs`
   - Add a `Principal` fact showing the stored principal code or `OperatorLabels`' exact `Not known` value.
   - Add a labelled active-principal select and a real POST handler. Its `Not known` option submits null; it is a valid selectable state, not a disabled placeholder. Preserve antiforgery, operation-key replay, stale-version response, authorization, and validation-summary behavior.
   - Render no helper prose, matching explanation, inferred suggestion, or new status. The fact remains visible even when no principal is recorded.
   - Acceptance: a staff member can set, replace, or clear the value; the detail page shows the exact value after redirect; unmatched/unknown records remain `Not known`.

5. Extend CASE-042's Awaiting row and quick view.

   - Reuse CASE-042's merged `ImageRow`, `QueueRow.Facts`, and generic `RecordDetail` quick-detail rendering; no `.cshtml` change is expected because facts already render generically.
   - Touch:
     - `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`
     - `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`
   - Add the `Principal` fact to the Awaiting image row/quick view using the projected code or `OperatorLabels.NotKnown`; do not restore image records to Not ready, add a Principal filter, or alter CASE-042's source/count/custody facts.
   - Reuse the local `DbCommandInterceptor` counting idiom from `AssessmentPersistenceIntegrationTests` inside `TriageQueuesWebTests`: prove that an otherwise identical Awaiting read has the same reader-command count with and without a recorded principal.
   - Acceptance: both known and unknown values appear in the Awaiting row and quick view; principal display is from `ImageIntake.PrincipalId` only; queue read count is unchanged.

Named dependency, not a CASE-045 implementation step: scoped Test UI snapshot output for the changed routed pages must be coordinated with the EPIC-012 snapshot owner. Do not alter `scripts/*.ps1`, `.github/workflows/ci.yml`, or `TestUiSnapshotTests.cs`; do not absorb generated `docs/design/test-ui/**` changes unless the ticket's owned-path scope is explicitly extended.

Local validation, after the generated migration is present:

```powershell
pwsh ./scripts/Test-MigrationGrants.ps1
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImageIntakePersistenceTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImageIntakeWebTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~TriageQueuesWebTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IntakePersistenceIntegrationTests"
```

Do not run the whole integration or browser suite locally. Stop on any failed command, stale dependency shape, missing migration ordering, changed grant requirement, or required path outside the owned list. The stop condition is: scoped checks pass, report is written, PR targeting `dev` is open, and CASE-045 is moved to Review; do not merge or begin another ticket.

## Simplification pass

Not yet run — this is the planning document. Record the dated "Simplification pass" heading and dispositions in this document (or its own scratch note) after the branch's diff exists, per the repository workflow.
