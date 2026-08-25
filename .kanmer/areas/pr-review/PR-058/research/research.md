# Research — PR-058: Restore batched EVA image content reads

## Question

What is the smallest change that restores the established Box-efficient image-read path for the single EVA Export act, while preserving exact content validation and deterministic archive order?

## Findings

- `IDocumentContentStore.ReadVersionsAsync` already exists in `src/Pegasus.Core/Documents/DocumentContracts.cs`. Its default implementation reads each requested version and verifies the supplied SHA-256 and length, so every store remains compatible without another abstraction.
- `BoxDocumentContentStore.ReadVersionsAsync` in `src/Pegasus.Infrastructure/Custody/BoxDocumentContentStore.cs` is the optimized production implementation. It validates the whole request before I/O, requires one Case per batch, resolves and lists the Case folder once, downloads with bounded concurrency, and verifies each result against the requested hash and length.
- `BoxDocumentContentStoreTests.ABatchReadResolvesTheCaseFolderOnceForEveryImage` proves five images cost eight Box requests, preserve input order and return the same bytes as one-at-a-time reads. `ABatchReadStillFailsClosedOnContentThatDoesNotVerify` covers corrupt and missing content.
- On `origin/dev`, `EvaHandoffStore.LoadEligibleImagesAsync` selects eligible rows in occurrence-ordinal order, converts them to `ManagedDocumentContentRead`, calls `ReadVersionsAsync` once, and combines rows and returned contents by index. Commit `4d3a3d04` introduced that focused latency correction.
- PR #539 at `cf28b8b0` retains the same eligibility query and ordering but replaces the single batch call with a `foreach` containing `OpenReadVersionAsync`. That bypasses the Box batch override and repeats folder resolution/listing serially for every image.
- `docs/frd/frd-07-eva-and-external-engineering-handoff.md` requires every eligible retained Case-vehicle image and deterministic archive order. Restoring the batch call changes only how verified bytes are obtained; it does not alter Review readiness, image eligibility, the archive schema, or export audit history.

## Implications

Restore the small `selectedRows` + one `ReadVersionsAsync` + index-based projection already present on `origin/dev`, including the existing `ContentRead` and `BundleImage` helpers. Do not add a service, queue, cache, flag, or new storage API.

Keep the existing Box behavioral tests. Add a narrow architecture/source-shape assertion beside the existing EVA store boundary assertions: the single Export store must call `contentStore.ReadVersionsAsync` once and must not call `contentStore.OpenReadVersionAsync`. The existing end-to-end export test and Box tests already prove archive bytes/order and batch integrity, so duplicating the full export fixture with a bespoke fake store would be disproportionate.

## Open questions

None. The ticket outcome, existing contract, optimized adapter and prior implementation all identify the same restoration.
