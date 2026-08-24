# Files

Traced 2026-08-24 against `origin/dev` @ `a6acc782`.

| File | Change | Why |
| --- | --- | --- |
| `src/Pegasus.Core/Documents/DocumentContracts.cs` | Add `ManagedDocumentContentRead` and a batch `ReadVersionsAsync` on `IDocumentContentStore`, with a **default interface implementation** that loops `OpenReadVersionAsync`. | The port exposes no batch route today (`:226-282`), so a remote store has no way to resolve a case folder once for N files. The default keeps every other implementation and test double untouched. |
| `src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs` | Add `BoxContentClient.DownloadFencedAsync(BoxItem, string fencedParentId, ct)`; extract the existing download body it shares with `DownloadAsync(string, ct)`. | `EnsureDescendantAsync` (`:526-562`) re-walks ancestry on **every** call — ~20 of the 45. A child returned by listing an already-fenced folder is proved inside the approved root by construction; the fenced overload re-checks that locally (free) instead of by two GETs. |
| `src/Pegasus.Infrastructure/Custody/BoxDocumentContentStore.cs` | Override `ReadVersionsAsync`; replace both `VerifyFileMetadataAsync` calls with the in-memory `IsExpectedRevision` on the item the listing already returned; delete `VerifyFileMetadataAsync`; use `DownloadFencedAsync` in `OpenReadVersionAsync` and in the `StoreVersionAsync` replay branch. | `OpenReadVersionAsync` (`:75-99`) costs 9 Box calls. `VerifyFileMetadataAsync` (`:202-214`) re-GETs `id,size,parent,content_type` — the exact fields `FindChildAsync` already returned from the listing, so it is a pure duplicate. |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | `LoadEligibleImagesAsync` (`:743-828`): push the eligibility predicate into SQL, matching sibling `GetPreparationAsync` (`:80-105`); replace the per-image `foreach` with one `ReadVersionsAsync`. Same replacement for the hand-off loop at `:518-545`. | The two `foreach`+`await` loops are the sequential 45. The unfiltered `ToArrayAsync` selects every occurrence × version for the case and filters in memory. |
| `tests/Pegasus.IntegrationTests/BoxDocumentContentStoreTests.cs` | Add `RequestCount` to the existing `InMemoryBox` fake and a lock around `Handle`; add tests pinning the reduced round-trip count for a single read and a five-image batch, and batch order/content. | The counting fake already exists (`InMemoryBox`, `:203-427`, with `UploadCount`/`DeleteCount`/`LargestItemsRequestOffset`) — extend it rather than write a second one. The lock is needed because the batch now issues concurrent requests into a `Dictionary`-backed fake. |

## Read, not changed

- `src/Pegasus.Core/Eva/EvaBundleSchema.cs` — the archive format. **Out of scope**; ENG-014 owns it. `EvaHandoffPolicy.SelectEligibleImages` (`:462-471`) is a pure per-candidate `Where` + `OrderBy(Ordinal)`, which is what makes the SQL predicate push provably behaviour-preserving.
- `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs`, `EfAssessmentReportProjectionSource.cs` — two further N-image loops. They inherit the per-read savings (9 → 4 calls) without a diff; see the plan for why they are not converted to the batch route.
- `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs` — the `Cases.AnyAsync` at `:470`. See the plan: not taken, with a reason.
