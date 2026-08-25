# Plan — PR-058: Restore batched EVA image content reads

## Approach

Restore the existing `IDocumentContentStore.ReadVersionsAsync` path inside `EvaHandoffStore.LoadEligibleImagesAsync`: build one ordered request from the already ordered eligible rows, perform one batch read, and pair each returned content item with the row at the same index. This reuses the production Box adapter's folder-once, bounded-concurrency implementation and the interface's default fallback. It is simpler and safer than adding an exporter-specific abstraction, cache, queue, or fake-backed test harness.

## Governing docs

- **Meets — `docs/frd/frd-07-eva-and-external-engineering-handoff.md`.** The restored read path still includes every eligible retained Case-vehicle image, preserves deterministic occurrence order, and retains exact hash/length verification. It changes storage retrieval mechanics only; Review readiness, custody meaning, exported values, archive shape, and history do not change.
- **No governing-doc modification or ADR is required.** The existing document-content port and Box implementation already own this mechanism.

## Steps

1. In `EvaHandoffStore.LoadEligibleImagesAsync`, replace the per-row `OpenReadVersionAsync` loop with the prior ordered `ManagedDocumentContentRead` projection, one `ReadVersionsAsync` call, and index-based `BundleImage` projection. Reuse the existing `ContentRead` and `BundleImage` helpers; add no new type or service.
2. Extend the existing EVA persistence source-shape assertion in `DependencyDirectionTests` to require the batch call and reject a direct serial `OpenReadVersionAsync` call from the exporter.
3. Review the focused diff for order preservation and scope: the selected-row query remains occurrence-ordinal ordered, the batch request and returned projection use that same order, and no readiness/history/schema/API behavior changes.
4. Run focused verification and record the final files, rationale, commands and results in the post-implementation report.

## Verification

- Build the affected projects through the repository's locked Release build.
- Run the focused architecture assertion covering the EVA persistence source shape.
- Run `BoxDocumentContentStoreTests` to retain proof of one folder resolution/listing, input-order preservation, and fail-closed missing/corrupt content.
- Run `ExportingACaseProducesTheEvaFormatArchive` to prove the ordered verified bytes still reach the EVA archive.
- Run `git diff --check` and inspect the branch diff against its base.
- After merge, `kanmer-verify` repeats the focused tests on merged `main` and records the exact merged SHA/results in `proof.md`.

## Risks / open questions

- **Row/content index drift:** mitigated by keeping the existing occurrence-ordinal query and deriving both the read request and result projection from the same materialized `selectedRows` list.
- **Accidentally weakening content verification:** mitigated by using `ManagedDocumentContentRead` with expected hash/length and retaining the Box fail-closed tests.
- **Scope growth:** do not generalize other content consumers or add performance infrastructure; this is a two-file restoration.
- No open questions.
