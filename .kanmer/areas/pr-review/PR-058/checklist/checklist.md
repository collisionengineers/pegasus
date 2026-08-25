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

## Closeout — PR-058

- [x] PR merge verified: PR #539 merged at 2026-08-25T00:47:21Z
- [x] proof.md finalised with PR URL, merge date and immutable Release 28 evidence
- [x] Moved to final stage
- [x] Outcome recorded with release evidence and honest verification boundary
- [ ] Shared worktree removal — deliberately deferred to preserve the two pre-existing modified EVA reference samples
- [ ] Shared branch deletion — deliberately deferred with the shared worktree
- [ ] Fetch/prune — deliberately deferred; no shared Git state changed
- [ ] Ticket claim release — performed only after all Kanmer records are finalised

- [x] Ticket claim released after Kanmer proof, traceability, outcome and deployment records were finalised. Shared Git cleanup remains intentionally deferred to preserve the modified reference samples.
