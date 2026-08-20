# INTK-014 file map

## Core (policy + ports)
- `src/Pegasus.Core/Custody/CustodyContracts.cs` — extend `ICaseCustody` with `RetainImageCaseAssetAsync` + `MergeImageCaseContentsAsync` (default implementations fail closed).
- `src/Pegasus.Core/Custody/ExternalWorkProcessing.cs` — add `ExternalWorkKinds.CreateImageCaseCustody` / `MergeImageCaseCustody`; route both to the custody handler in `ProcessQueuedExternalWork`.
- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` — surface custody state on `ImageIntakeDetail` (columns are read back for honesty; no operator-facing GUIDs).

## Infrastructure (adapters + persistence)
- `src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs` — implement the two operations; `BoxContentClient` gains `ListChildrenAsync`, `MoveFileAsync`, `DeleteFolderAsync` (all root-fenced via `EnsureDescendantAsync`).
- `src/Pegasus.Infrastructure/Custody/LocalCaseCustody.cs` — same operations for local/offline; `UnavailableCaseCustody` keeps failing closed via interface defaults.
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` — `ExternalWorkItemEntity.CaseId` → `Guid?` + new `ImageIntakeId` FK; `ImageIntakeEntity` custody columns; model config.
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` — enqueue create work in `RegisterAsync`; enqueue merge work in the merge branch of `TransitionAsync`; map custody columns.
- `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` — accept + process the two image kinds (payload load, custody calls, completion transactions).
- `src/Pegasus.Infrastructure/Persistence/EfExternalWorkStore.cs` — image branches in `FailProcessingAsync` / `MarkPoisonedAsync` (backoff re-arm or terminal fail + honest `CustodyState`).
- `src/Pegasus.Infrastructure/Persistence/EfOperationsStore.cs` — the ExternalWork projection join survives nullable `CaseId` (cast only; image create rows deliberately excluded).
- `src/Pegasus.Infrastructure/Persistence/Migrations/` — one new migration (ALTER columns only; no new table, so no grant census change) + designer + snapshot.

## Web
- `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml(.cs)` — only if trivially needed; not required by the ticket (custody state is recorded, not yet an operator surface).

## Tests
- `tests/Pegasus.IntegrationTests/ProductionBoxCustodyTests.cs` — extend `StatefulBox` (move/delete) + new Box adapter tests for image-asset retention, merge move, fencing.
- `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` or a new `ImageCaseCustodyIntegrationTests.cs` — end-to-end: register (group) → work row → processor with `LocalCaseCustody` → columns confirmed; merge → move work → folder folded; Box outage → retry re-arm → images never lost.
- `tests/Pegasus.IntegrationTests/LocalCaseCustodyAtomicWriteTests.cs` — local adapter coverage for the new operations if it fits there.

## Scripts (verify only, no change expected)
- `scripts/Test-MigrationGrants.ps1`, `scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`.
