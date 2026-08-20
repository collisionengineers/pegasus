# Plan — DOCS-005

Branch `task/box-custody` from origin/dev (0857f7b9), worktree `../pegasus-worktrees/box-custody`.

1. **Interface** (reuse: the RetainAcceptedIntakeSource overload pattern): `RetainAcceptedIntakeAttachmentAsync(root, IntakeSourceCustodyReference attachment, int ordinal, operationKey[, leaseGuard], ct)` with a fail-closed default so existing fakes compile and adapters that cannot retain attachments refuse rather than skip.
2. **Box**: `GetOrCreateBoundFolderAsync` → `GetOrCreateOwnedFolderAsync` — same staged `.pegasus-create-{token}` create/rename promotion, no binding upload or verification; existing folders verified by parent/name identity only (the DB remote id remains the authority via `ValidateRootAsync`'s id equality). `RetainAcceptedIntakeSourceCoreAsync` drops its binding block. New attachment method reuses `GetOrCreateFolderAsync` + `UploadOrVerifyFileAsync` with `"{ordinal:D3} {SafeName(name)}"`. The fold keeps skipping/deleting a legacy `pegasus-case-binding.json`. Dead helpers (`CaseBinding`, `AuditBinding`, `AcceptedSourceBinding`, `VerifyBoundFolderAsync`, unused byte-verify) deleted.
3. **Local**: attachment method stores content-addressed like the source (`documents/{receiptId:N}-attachments/{ordinal:D3}-{hash}`), verifying the artifact hash.
4. **Processor**: in the case-custody branch, after the source retention, read the receipt's attachment assets (`Kind = 'attachment'`, ordered by FileName then Id) from the context and retain each at ordinal `index + 2` with op key `{OperationKey}:attachment:{assetId:N}` — idempotent through the same upload-or-verify replay.
5. **Tests**: update custody suites that assert binding files; add — new folder has no binding JSONs; attachments land beside the source; legacy fold with a binding file still folds. Suites: CustodyOutboxIntegrationTests, local custody durability, QdosCustodialWebTests; Release build 0/0.

Deviation: subagents barred — self-review recorded.
