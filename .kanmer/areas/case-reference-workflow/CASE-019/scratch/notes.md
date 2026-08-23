## Production verification, 2026-08-23 — the export fails, and it is not the export

Signed-in operator session on release 24. Pressing Export on QDOS26011 returns **"The case could not be exported."**

### Traced to the content read, not the export

Three surfaces fail with one fault:

| Surface | Result |
| --- | --- |
| Evidence tab images | eight `img` elements never complete |
| `/Cases/{caseId}/Documents/{occurrenceId}/Download?versionId=…`, ids taken from the database | **404** |
| Case export | "The case could not be exported." |

`BoxDocumentContentStore` throws `FileNotFoundException` (an `IOException`) or `InvalidDataException`; the download page and the export handler both catch that set and turn it into 404 / a friendly error, which is why the cause is invisible from outside.

The records are not at fault. QDOS26011 holds 8 `Image` occurrences at ordinals 2–6 and 8–10, all version 1, `CustodyStatus = Confirmed`, `IsCurrent`, none logically removed, none third-party confirmed. Case custody is `confirmed` with `CustodyRootRemoteId = 411135978094`.

### Ruled out by reading the code

- **Folder name mismatch.** `BoxCaseCustody.CaseFolderName(reference)` and `BoxDocumentContentStore.ResolveCaseFolderAsync` both use `CustodyNames.SafeName(reference)` under the same root. Identical.
- **File name mismatch.** Upload uses `$"{ordinal:D3} {SafeName(fileName)}"`; the read uses `$"{OccurrenceOrdinal:000} {SafeName(FileName)}"`. `D3` and `000` are the same for positive integers, and the occurrence ordinal is the ordinal the upload used.
- **Media type absent from Box.** `GetFileAsync` explicitly requests `content_type` in its `fields` list, so it is not simply missing.

### Still open

`VerifyFileMetadataAsync` rejects on parent id, size **or** media type, and `Verify` then rejects on content hash — four ways to reach the same opaque outcome. Distinguishing them needs the exception text, which is what the log watch is for.

### Why the diagnosis is this slow

- Application Insights has been `OverQuota` throughout (0.1 GB/day, resets 03:00Z) — PLAT-036. No traces, no exceptions.
- The container console stream is dominated by health-check `SELECT 1` at Information level, so a 300-line tail spans only seconds.
- `az containerapp logs show --follow` block-buffers through a pipe, so a streaming grep emits nothing. A polling loop that re-runs `--tail 300` flushes per iteration and is the working approach.

### What this does and does not say about CASE-019

The export's own code is proven by `ExportingACaseProducesTheEvaFormatArchive`, which builds and opens a real archive and hash-checks every entry against `manifest.sha256`. What is unproven is that production can read the bytes at all — and that is [[DOCS-010]], a pre-existing defect that predates this ticket. `EvaHandoffRevisions` is empty, so nothing had ever read content back from Box in production before now.
