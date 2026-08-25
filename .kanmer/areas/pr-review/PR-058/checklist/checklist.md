# Checklist — PR-058

- [x] Replace the exporter’s serial image-read loop with one ordered `ReadVersionsAsync` call using the existing helpers.
- [x] Add the EVA persistence source-shape assertion requiring the batch call and forbidding the serial call.
- [x] Confirm the diff preserves occurrence order and changes no readiness, history, archive-schema, API, or custody behavior.
- [x] Run the locked Release build.
- [x] Run the focused EVA architecture assertion.
- [x] Run `BoxDocumentContentStoreTests`.
- [x] Run `ExportingACaseProducesTheEvaFormatArchive`.
- [x] Run `git diff --check` and audit the branch diff.
- [x] Write the post-implementation report with exact files, commands and results.

## Progress notes


2026-08-25: implementation and focused verification completed at c86b803c. Simplification lenses: reused existing lock and batch-read conventions; removed obsolete switches and catch/retry path; no new abstraction; no deferred code finding.
