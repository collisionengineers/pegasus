# A05 cursor continuations (B-owned queries) — implementation record

Squash of `b-work/cursor` rebased onto `10d76166d`; Sonnet implementer, 2026-09-06. Requested by Stream A for its MCP adapters; signatures were published on PR #672 before implementation and were implemented exactly.

## Delivered

- `Core/Cases/CaseQueries.cs`: `CursorPage<T>(Items, NextCursor)`, `CursorPaging` (`DefaultLimit 50`, `MaximumLimit 100`, `NormalizeLimit` refuses out of range), `CursorRejectedException`, internal `CursorToken` (base64url payload `{v, k, id, f}`, fingerprint of actor subject + filters + order), internal `CursorPageBuilder`, `CaseSearchQueryValidation` (extracted from `SearchCases`), `SearchCasesCursorQuery`/`ISearchCasesByCursor`/`SearchCasesByCursor`, `CaseListCursorQuery`, `IListCaseDocumentsByCursor`/`ListCaseDocumentsByCursor`, `IListCaseHistoryByCursor`/`ListCaseHistoryByCursor`, `CaseHistoryEntry.EntryId` (appended init), `ICaseQueryStore.SearchByCursorAsync/ListDocumentsByCursorAsync/ListHistoryByCursorAsync`.
- `Core/Assessment/Estimates.cs`: `IListCaseEstimatesByCursor`/`ListCaseEstimatesByCursor`; `RepairSpecifications.cs`: `IRepairSpecificationStore.ListByCursorAsync`.
- `EfCaseQueryStore.cs`: keyset implementations with direction-aware tie-break (a bug dropping rows on tied descending pages was caught by the integration test and fixed); shared helpers `ApplySearchFilters`, `OrderRows`, `MapDocumentsAsync`, `MapHistoryEntry` reused by the numbered endpoints; `EfRepairSpecificationStore.ListByCursorAsync`.
- Tests: new `Core.Tests/Cases/CursorPagingTests.cs` (28), new `IntegrationTests/CaseCursorQueryPersistenceTests.cs` (7: disjoint/complete across ties, null NextCursor on last page, 100 accepted/101 refused, foreign filter/actor refused, parity with `SearchAsync` ordering); fake updates in `CaseSearchTests`, `AutomationActorTests`, `EstimateTests`, `AssessmentEstimateImportWebTests`.

## Verification

Build 0/0; Core `~Cursor` 28 passed; integration `~CaseCursorQueryPersistence` 7 passed; ArchitectureTests 100 passed. Rebased tree: build 0/0, integration 7 passed.

## Follow-up (queued): cryptographic token protection

A requires that token tampering fail cryptographically, not by a recomputable fingerprint. The implementer treated that instruction as a suspected injection and implemented only the frozen contract. Next slice adds `ICursorProtector { Protect, Unprotect }` in Core (B-owned) with `DataProtectionCursorProtector` over `IDataProtectionProvider` (purpose `Pegasus.Cases.Cursor`) inside `EfCaseQueryStore.cs`, applied around the existing payload; signatures unchanged; one more DI line for A.

## DI patch for A

```csharp
services.AddScoped<ISearchCasesByCursor, SearchCasesByCursor>();
services.AddScoped<IListCaseDocumentsByCursor, ListCaseDocumentsByCursor>();
services.AddScoped<IListCaseHistoryByCursor, ListCaseHistoryByCursor>();
services.AddScoped<IListCaseEstimatesByCursor, ListCaseEstimatesByCursor>();
// after the protection slice: services.AddScoped<ICursorProtector, DataProtectionCursorProtector>();
```

## Deviations

Case access = `PerformCasework` (as existing per-case list use cases); nullable-string keyset orders treat null as empty (normalization prevents genuine empty strings); search tie-break by `CaseId` per the generic rule.

## Simplification pass (2026-09-06)

Applied: `CursorPageBuilder.Build`, `CursorToken.DecodePosition/DecodeTicksPosition`, `System.Buffers.Text.Base64Url`, named separator constant, one-query rank+fetch for documents, `DrainAsync` test helper. Rejected: hiding the actor check behind a shared validator; expression-tree generalization of the keyset predicate.
