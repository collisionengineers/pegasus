---
id: PLAT-041
type: ticket
title: Cut the export from ~45 Box round trips to a handful
status: verifying
area: platform-operations
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-24T08:49:58.333Z'
  review: '2026-08-24T09:58:00.305Z'
  verifying: '2026-08-24T14:56:59.352Z'
taken_at: '2026-08-24T08:42:25.801Z'
branch: task/plat-041-box-round-trips
worktree: ../pegasus-worktrees/plat-041
labels:
  - qdos26014
  - qdos26015
  - found-during-qa
  - performance
  - box
links: []
docs_todo: true
deployment: production
archived: false
created: '2026-08-23T15:19:04.949Z'
updated: '2026-08-24T16:54:28.376Z'
---

## What the operator saw

> *"The export seems to take too long though, around 10ish seconds."*
> — `QDOS26014`, three photographs.

> *"from pressing, to showing the download, takes 18 seconds to fully process."*
> — `ap.QDOS26015`, five photographs (2026-08-24).

## Measurements

| Case | Images | Export |
| --- | ---: | ---: |
| `ap.QDOS26011` (2026-08-23, release 25) | 8 | ~25 s |
| `QDOS26014` (2026-08-23, release 26) | 3 | ~10 s |
| `ap.QDOS26015` (2026-08-24) | 5 | ~18 s |

Roughly linear in image count at ~3 s each, with a fixed overhead — which is
the signature of per-image work, not per-export work.

## Cause — traced in full (2026-08-24)

400 KB of content is not the problem. **~45 sequential Box HTTPS calls are**, of
which only 5 move any bytes.

`EvaHandoffStore.LoadEligibleImagesAsync` (`:792-828`) is a plain `foreach` with
one `await` per image — no `Task.WhenAll`. `IDocumentContentStore`
(`src/Pegasus.Core/Documents/DocumentContracts.cs:226-282`) exposes no batch
method at all, so parallelism was never designed for.

Each `BoxDocumentContentStore.OpenReadVersionAsync` (`:75-99`) then makes four
Box calls, and **every** `BoxContentClient` method opens with
`EnsureDescendantAsync` (`BoxCaseCustody.cs:526-562`), an ancestry walk issuing
one GET per level:

| Step | Calls | |
| --- | ---: | --- |
| (A) `ResolveCaseFolderAsync` | 1+ | **lists the entire custody root**, `limit=1000`, name filtered *client-side* (`BoxCaseCustody.cs:305-311`) — a floor, not a fixed cost |
| (B) `FindChildAsync(caseFolder, fileName)` | 2 | ancestry + listing |
| (C) `VerifyFileMetadataAsync` | 3 | **redundant** — its own doc comment (`:175-190`) admits SHA-256 is the real guarantee |
| (D) `DownloadAsync` | 3 | ancestry re-walked again |

≈9 per image × 5 images ≈ 45 × ~400 ms ≈ **18 s**. The same file id is fetched
three times in a row before a single byte moves.

`BoxContentClient` is stateless (`BoxCaseCustody.cs:263-266`) — **no cache of any
kind**. Folder ids, file ids and ancestry are recomputed on every call.

The same resolution also runs on every Evidence-tab thumbnail, so the gallery
pays it per image too.

### Cleared of suspicion, by reading the code

- Field mapping and provenance building do **zero I/O** — no PDF re-read, no
  re-extraction, no AI, no DVLA/DVSA call. The vehicle lookup is persisted at
  intake by the Worker; export reads `CaseDataFields`.
- The Box JWT lease is cached correctly and single-flighted.
- SQL is ~11 round trips (~1–3%, ~50–150 ms).
- Zip build and hashing are ~10–30 ms. `CompressionLevel.NoCompression` is
  deliberate and correct for JPEGs.

**Every HTTP call on this path goes to Box.**

## Shape of the fix

In impact order:

1. Resolve the case folder **once per export** and reuse it for every image.
   Prefer the explicit batch route over a per-case memo — a memo has a
   correctness question worth answering first: a case folder id is durable
   (`Cases.CustodyRootRemoteId`), so it is reasonable to hold, but a stale id
   after a Box-side move must fail loudly rather than read the wrong folder.
2. Drop the redundant `VerifyFileMetadataAsync` GET. −3 calls per image; the
   SHA-256 check at `BoxDocumentContentStore.cs:97` is the real guarantee.
3. Stop re-walking ancestry per call once the folder is known. ~20 of the 45.
4. Fetch the images concurrently.

Target: 1 folder resolve + 1 listing + N downloads ≈ **7 calls instead of 45**.

### Two cheap wins while in there

- `LoadEligibleImagesAsync` (`:743-769`) selects every occurrence × version for
  the case with no predicate and filters in memory, unlike its sibling
  `GetPreparationAsync` (`:80-105`) which pushes the predicate into SQL.
- `EfVehicleWorkflowStore.cs:470` runs a `Cases.AnyAsync` the caller has already
  proved at `EvaHandoffStore.cs:687-694`.

Also worth noting, not necessarily fixing: nothing persists a generated operator
export (`EvaHandoffStore.cs:668-673`), so a second click costs the full time
again. The hand-off path, by contrast, stores `BundleContent` and can replay it.

## Not urgent, and worth saying so

Nothing is broken; the archive is correct and hash-verified. This is a
proportionality ticket: three images should not cost ten seconds, and the cost
grows with the thing operators will do most. Filed rather than folded into the
Box work so it is scheduled on its own merits.

Independent of the import-parity work in [[ENG-014]] and [[ENG-015]] — different
files, so it can run in parallel with them.

## Verification

- [ ] Measure before and after in App Insights:
      `dependencies | where operation_Id == <export request>`. Web is
      instrumented (`Program.cs:193-199`) and auto-collects `HttpClient` calls,
      so the ~45 `api.box.com` rows are already visible with no code change.
      Note the daily cap resets 03:00Z.
- [ ] A five-image export completes in a few seconds, not eighteen
- [ ] The archive bytes are unchanged — this is a latency fix, not a format one
