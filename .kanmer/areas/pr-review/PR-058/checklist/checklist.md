# Checklist — PR-058

- [ ] Replace the exporter’s serial image-read loop with one ordered `ReadVersionsAsync` call using the existing helpers.
- [ ] Add the EVA persistence source-shape assertion requiring the batch call and forbidding the serial call.
- [ ] Confirm the diff preserves occurrence order and changes no readiness, history, archive-schema, API, or custody behavior.
- [ ] Run the locked Release build.
- [ ] Run the focused EVA architecture assertion.
- [ ] Run `BoxDocumentContentStoreTests`.
- [ ] Run `ExportingACaseProducesTheEvaFormatArchive`.
- [ ] Run `git diff --check` and audit the branch diff.
- [ ] Write the post-implementation report with exact files, commands and results.

## Progress notes
