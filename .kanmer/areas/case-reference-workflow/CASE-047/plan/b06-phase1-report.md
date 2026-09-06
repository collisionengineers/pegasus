# B06 phase 1 — implementation record

Integrated as `22bf79e0e` (squash of `b-work/b06`, rebased onto `8c78e97ac`); Sonnet implementer, 2026-09-06. Four new files only: `src/Pegasus.Core/Documents/CaseAssetPreparation.cs`, `src/Pegasus.Infrastructure/Persistence/EfCaseAssetPreparationStore.cs`, `tests/Pegasus.Core.Tests/Documents/CaseAssetPreparationTests.cs`, `tests/Pegasus.IntegrationTests/CaseAssetPreparationPersistenceTests.cs`.

## Contract

`CaseAssetReportRole { NotUsed, CloseUp, Overview, Supporting }`; `CaseAssetRotation { None=0, Clockwise90=90, Half=180, Clockwise270=270 }`; `CaseAssetCrop(Left, Top, Width, Height)` (fractions of the rotated source; `Full`, `IsFull`, `Validate()`); `CaseAssetPreparation(CaseId, OccurrenceId, DocumentId, VersionId, SourceVersion, SourceSha256, SourceContentType, Role, Order?, Rotation, Crop, PreparationVersion, PreparedBy?, PreparedAtUtc?)`; `CaseAssetPreparationEdit(OccurrenceId, ExpectedPreparationVersion, Role, Order?, Rotation, Crop)`; `SaveCaseAssetPreparationRequest(..., Edits)` and `ResetCaseAssetPreparationRequest(..., OccurrenceIds)` : `CaseMutationRequest`; `PreparedReportImage(OccurrenceId, VersionId, Sha256, ContentType, Role, Order?, Rotation, Crop)`; `ICaseAssetPreparationQueries.ListForCaseAsync`; `ICaseAssetPreparationStore { SaveAsync, ResetAsync }`; `CaseAssetPreparationVersionConflictException`; `CaseAssetPreparationPolicy.ValidateSet(caseId, proposed, confirmedSourcesByOccurrence: IReadOnlyDictionary<Guid, DocumentVersion>)` and `ForReport(current)`. Store: `IDbContextFactory`, one serializable transaction, replay by operation key, per-row `PreparationVersion`, cross-Case/stale-source rejection, three-row history, one version bump; `internal static PrepareSaveAsync(PegasusDbContext, CaseWorkflowEntity, SaveCaseAssetPreparationRequest, DateTimeOffset now, CancellationToken)` for the workspace transaction (no commit, no version bump). Only `SemanticRole == Image` occurrences are eligible. Reset renormalizes the remaining Supporting order through `ValidateSet`. Bytes/sha256 never change.

## Verification (agent run on `0c00c74a7`; repeated on the rebased tree before integration)

Build 0 errors; Core `~CaseAssetPreparation` 30 passed; integration `~CaseAssetPreparation` 14 passed; ArchitectureTests 100 passed (agent run). Rebased tree: build 0 errors, Core 30, integration 14.

## DI patch for A (phase 2, when the Files/Report wiring lands)

```csharp
services.AddScoped<ICaseAssetPreparationStore, EfCaseAssetPreparationStore>();
services.AddScoped<ICaseAssetPreparationQueries, EfCaseAssetPreparationStore>();
```

## Deviations

`ValidateSet` takes the confirmed sources map explicitly (reusing `DocumentVersion`) so freshness is unit-testable; list/query scope limited to image occurrences.

## Simplification pass (2026-09-06)

Fixed: Reset left Supporting-order gaps (now renormalized via the one `ValidateSet`, regression test added); `PrepareSaveAsync` returned pre-mutation state; `ResetAsync` read the after-state before `SaveChangesAsync`. Accepted: repeated open/transaction/replay/guard prologue per public method, consistent with sibling stores. No second policy owner, no new content reader.
