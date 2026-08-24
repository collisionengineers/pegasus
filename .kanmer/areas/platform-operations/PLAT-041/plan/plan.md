# Plan

A latency fix. **The archive bytes do not change** — that is the regression signal,
and ENG-014 owns the format.

## Round-trip arithmetic

Per image today, `BoxDocumentContentStore.OpenReadVersionAsync` = 9 Box calls:
resolve (1) + find child (ancestry 1 + listing 1) + verify metadata (ancestry 2 +
GET 1) + download (ancestry 2 + GET 1).

| | Fixed | Per image | 5 images |
| --- | ---: | ---: | ---: |
| Today | 0 | 9 | **45** |
| After, single-read path (Evidence tab, document download, report) | 0 | 4 | 20 |
| After, batch path (EVA export and hand-off) | 3 | 1 | **8** |

The 3 fixed = case-folder resolve (1, a listing of the custody root) + case-folder
listing (ancestry 1 + listing 1). The remaining ancestry GET on the case folder is
left in place deliberately: removing it needs a *second* fenced method for one call
out of eight (~0.4 s of ~3.2 s) and no correctness gain. Recorded, not taken.

## Steps

### 1. `IDocumentContentStore.ReadVersionsAsync` — the batch port

`src/Pegasus.Core/Documents/DocumentContracts.cs`.

```csharp
public sealed record ManagedDocumentContentRead(
    ManagedDocumentContentAddress Address, string ExpectedSha256, long ExpectedLength);

Task<IReadOnlyList<ReadOnlyMemory<byte>>> ReadVersionsAsync(
    IReadOnlyList<ManagedDocumentContentRead> reads, CancellationToken cancellationToken)
```

**Reuses**: the existing default-interface-implementation convention already in this
interface (`StoreVersionAsync` and `OpenReadVersionAsync` both have one, `:247-280`).
The default is a sequential loop over `OpenReadVersionAsync`, so `LocalDocumentContentStore`
and every test double are unchanged.

**Justification for the abstraction** (repo rule: no abstraction without a second
concrete caller). Four one-case N-file loops exist today:
`EvaHandoffStore:518` (hand-off), `EvaHandoffStore:790` (export),
`EfDocumentCustodyStore:589` (document zip), `EfAssessmentReportProjectionSource:89`
(report photos). The first two are converted here — two real callers in this diff.
The Evidence-tab thumbnail (`EfDocumentCustodyStore:252`) is a single-file request
per HTTP call and cannot batch; it is a real caller of step 2/3, not of this method,
and drops 9 → 4 calls.

`ReadOnlyMemory<byte>` because that is exactly what `EvaBundleImage.Content` takes
(`EvaBundleSchema.cs:37`) — no conversion at either end.

### 2. Drop the redundant metadata GET

`BoxDocumentContentStore.cs`. `FindChildAsync` returns a `BoxItem` parsed from a
listing requested with `fields=id,name,type,etag,size,content_type,parent`
(`BoxCaseCustody.cs:303`) — the same fields `VerifyFileMetadataAsync` re-GETs.
So call the existing `IsExpectedRevision` (`:192-200`) on the item already in hand
and delete `VerifyFileMetadataAsync`. Same exception, same message.

A listed child cannot be trashed (Box omits trashed items from folder listings), so
the `trashed_at` check that `EnsureDescendantAsync` contributed is not lost.

**Reuses**: `IsExpectedRevision` unchanged — it is already `internal static` and
already directly tested (`BoxManagedRevisionTests.cs:53`).

### 3. Fenced download

`BoxCaseCustody.cs`. Split `DownloadAsync(string fileId, ct)` into the ancestry check
plus a private `DownloadContentAsync(fileId, ct)`, and add:

```csharp
Task<byte[]> DownloadFencedAsync(BoxItem listedChild, string fencedParentId, ct)
```

which asserts `listedChild.ParentId == fencedParentId` locally and downloads. The
fence argument is *checked*, not asserted in a comment: the parent's descent from
the approved root was proved when it was listed, and the child claims that parent.
Not an optional parameter and not a memo — the caller must produce the fenced parent
it listed under, which is why the ticket's stale-id hazard cannot arise: nothing is
carried across operations.

Callers: `OpenReadVersionAsync`, the `StoreVersionAsync` replay branch, and the new
batch. `DeleteAsync` keeps the id overload — it holds only a recorded id from an
uncommitted write, with no fenced parent, and runs once.

### 4. Batch override in `BoxDocumentContentStore`

Resolve the case folder once, `client.ListChildrenAsync` it once, index the children
by name, then `Parallel.ForEachAsync` the downloads into a pre-sized array by index
(order preserved, fan-out bounded — a case with 50 photographs must not fire 50
concurrent requests at Box's rate limiter). `Parallel.ForEachAsync` is the host's own
mechanism; no new gate type.

**Reuses**: `ResolveCaseFolderAsync`, `ListChildrenAsync`, `FlatFileName`,
`IsExpectedRevision`, `Verify`, `NormalizeSha256`, `Validate` — all existing.

Preserved failure modes, same exception types and messages: duplicate children →
`InvalidDataException("Box contains duplicate custody children for one exact identity.")`;
wrong type → `InvalidDataException("A Box custody child has the expected name but the wrong type.")`;
missing file or missing case folder → `FileNotFoundException("The document content is unavailable.")`;
metadata mismatch and hash/length mismatch → the existing `InvalidDataException`s.
All reads must share one `CaseId`/`CaseReference` — asserted.

### 5. `EvaHandoffStore` — SQL predicate and the two loops

Push into SQL the six predicates `GetPreparationAsync` (`:80-105`) already pushes:
`SemanticRole == Image`, `IsCurrent`, `!IsLogicallyRemoved`,
`CustodyStatus == Confirmed`, `ThirdPartyVehicleConfirmedAtUtc == null`,
`MediaType in ("image/jpeg","image/png")`. Behaviour-preserving because
`EvaHandoffPolicy.SelectEligibleImages` is a pure per-candidate `Where` +
`OrderBy(Ordinal)` — no cross-row logic — and the policy call **stays**, so Core
remains the authority and the sibling's convention wins.

Then replace both `foreach { await OpenReadVersionAsync }` loops with one
`ReadVersionsAsync`, zipping the results back onto the rows in order.

### 6. Test — the counting fake

`tests/Pegasus.IntegrationTests/BoxDocumentContentStoreTests.cs`. `InMemoryBox`
already counts (`UploadCount`, `DeleteCount`, `LargestItemsRequestOffset`); add
`RequestCount` and a lock around `Handle` (the batch is now concurrent and the fake
is `Dictionary`-backed). Assert the exact reduced counts from the table above, and
that a batch returns the requested order and bytes.

## Not taken, with reasons

- **`EfVehicleWorkflowStore.cs:470` `Cases.AnyAsync`.** ~5 ms of an 18 000 ms problem,
  and removing it changes the `IVehicleEvidenceQueries.GetAsync` contract from
  `null` to an empty `CaseVehicleEvidence` for a case that does not exist — a public
  port with four callers. Bad trade; skipped deliberately.
- **`EfDocumentCustodyStore:589` (document-export zip).** It streams each source
  straight into a `ZipArchive` entry under a `maximumArchiveBytes` bound. Batching
  would buffer every document in memory first — a memory-shape change on a path that
  is explicitly length-bounded. Out of scope for a latency ticket.
- **`EfAssessmentReportProjectionSource:89`.** A different feature path, not named in
  the ticket. It takes the 9 → 4 per-read win for free.
- **Persisting a generated operator export** so a second click is cheap — the ticket
  raises it as "worth noting, not necessarily fixing". Not done; it is a storage
  decision, not a latency one.

## Simplification pass — 2026-08-24

Four lenses over the branch's own diff (reuse, simplification, efficiency,
altitude), run by an independent `code-simplifier` agent plus my own pass. It
changed the shape of the fix in two places, so this is not a formality.

### Applied

- **The read path no longer checks file metadata at all** (was: re-pointed at
  the listing). The pass found the premise this rested on was unverified and
  its failure mode is DOCS-010 exactly: `IsExpectedRevision` refuses a null
  `Size` or `ParentId`, deliberately and under test
  (`BoxManagedRevisionTests.cs:41-50`), and **no production path has ever read
  either field off a folder listing** — every proven read of them comes from
  `GET /files/{id}`. Feeding it listing data would have bet every managed Box
  read on Box honouring `fields=…,size,parent` on `/folders/{id}/items`.
  So the check is now simply gone from the read, which is what the ticket asked
  for ("drop the redundant GET; the SHA-256 check is the real guarantee"), and
  the premise is gone with it. `VerifyFileMetadataAsync` survives unchanged on
  the **write** path, where it decides replay against conflict before anything
  is committed and costs one call per document at intake.
- **`DownloadFencedAsync` tolerates a parent Box does not state**, and refuses
  only one that contradicts. The listing under the fenced folder is the proof;
  the stated parent is corroboration. Same reasoning, same rule: no check here
  may depend on Box volunteering a field.
- **Reverted the SQL predicate push** (the ticket's first cheap win). It copies
  six Core-owned eligibility rules into an Infrastructure LINQ expression —
  a fourth copy of the jpeg/png list, which already disagrees with
  `EfAssessmentReportProjectionSource.PhotoMediaTypes` (that one also allows
  `image/webp`). `EvaHandoffPolicy.SelectEligibleImages` re-decides all six, so
  the result set is unchanged and the gain is a smaller row count on a query of
  a few dozen rows — nothing, against 18 s of Box. It would also have made the
  two structurally identical EVA queries diverge in order to match a third. One
  owner for that predicate is a real improvement and a different ticket.
- **Extracted `BundleImage`** — the 14-argument `EvaBundleImage` construction
  was duplicated verbatim in both loops; both call sites are now one `Select`.
- **Extracted `BoxContentClient.SelectChild`** — the duplicate-child and
  wrong-type refusals had been copied into the batch path. They now have one
  owner and `FindChildAsync` delegates to it.
- **Hoisted `NormalizeSha256` out of the parallel body**, next to the existing
  `Validate` loop, so a malformed hash fails before any I/O and not racing
  whatever else is in flight.
- **Lowered the download fan-out from 8 to 4** and recorded why. Every other
  Box primitive in Infrastructure serialises behind a `SemaphoreSlim(1,1)`;
  this is the only fan-out, and nothing on the Box path retries a 429. Four
  overlaps essentially all of a normal export's download time.
- **Documented that the batch materialises everything**, so the next reader does
  not point the size-bounded ZIP export at it.

### Found, not applied — with reasons

- **`EfAssessmentReportProjectionSource.cs:80-107` is a third identical loop.**
  Same shape, same store, same cost. Not converted: a different feature path,
  not named by the ticket, and it takes the 9 → 4 per-read win for free. A
  follow-up, named here rather than left silent.
- **`BoxCaseCustody.VerifyFileAsync:1128-1148` still does the metadata GET plus
  the ancestry walk** this diff removed from the read path. Kept: it is the
  custody-binding verification, which is stricter on purpose and is not on the
  export path.
- **One ancestry GET per operation survives** — `ResolveCaseFolderAsync` proves
  the case folder by finding it in the root listing, and the next call re-proves
  it. Removing it needs a `ListChildrenFenced` twin of `DownloadFencedAsync`,
  a second fenced variant for one call site, for 1 request of 8. Rejected.
- **`ResolveCaseFolderAsync` lists the entire custody root** and grows forever
  with the case count. It is now the largest fixed cost of an export. The
  durable folder id is already in `Cases.CustodyRootRemoteId`; using it is the
  next latency ticket, and it is the same stale-id question the ticket raised.
  Not attempted here.
- **The `oversized` hoist is not exactly behaviour-preserving in one respect**:
  a case with a good image first and an oversized one later used to download the
  good one before returning `Blocked`. It now returns `Blocked` first. The
  `Blocked` message and the successful path are identical.

### Open premise — the one thing I could not check

Nothing now depends on Box returning `size`/`parent` in a folder listing, which
was the risk. What the pass could not verify, and what I did not have authority
to check (it needs the Box app credentials out of Key Vault, a credential read
requiring approval), is a live `GET /2.0/folders/{caseFolderId}/items?fields=id,name,type,etag,size,content_type,parent`.
The change is written so that response's shape cannot break it either way.
