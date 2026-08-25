# Checklist — TICK-064

- [x] Add the typed Core logical-folder catalogue/policy and exhaustive no-recommendation mapping tests without changing MAIL-02 queue policy.
- [x] Add validated approved-mailbox folder-binding contracts with preserve-versus-replace semantics and replay/history coverage.
- [x] Add the normalized EF binding entity/configuration/store mapping and one migration without touching retained-message persistence.
- [x] Add read-only exact Graph folder discovery and the administrator resolve/display caller with no client-supplied folder identities or Graph writes.
- [x] Add focused persistence, fake-Graph and Web caller tests for scope, ambiguity, authorization, version/replay and honest unconfigured results.
- [x] Run locked restore, Release build, focused tests and the full relevant suite.
- [x] Run the four-lens simplification pass, apply safe findings, and record dated dispositions in plan.md.
- [x] Commit/push, open the PR to dev, write the post-implementation report, record traceability and move the ticket to Review.

## Progress notes

- 2026-08-20: Locked restore and Release build pass with zero warnings/errors. Focused Core policy/administration passed 84; focused Graph/local resolver, mailbox persistence and Web passed 14; exact committed-schema and previously deadlocked Qdos case passed 2. Canonical non-corpus run passed Core 828 and Architecture 98; Integration passed 798/799 with one unrelated SQL timeout in `IntakeWebNegativeTests.UnknownExtensionReachesReaderAndPersistsUnsupportedReceipt` while another ticket contended for shared LocalDB. No further full run was started under the coordinated serialization instruction.
- 2026-08-20: Four-lens simplification pass recorded in plan.md; the committed-migration proof and hostile Graph paging test were the applied findings, with none left unapplied.
- 2026-08-20: Commits `a1ae9608` and `f23f7e0e` pushed; PR #468 targets `dev`; post-implementation report and not-deployed traceability recorded.

<!-- kanmer-groom:release-take:TICK-064:2026-08-25 -->
### Board-hygiene claim release — 2026-08-25

Audit record written before releasing this completed ticket's stale take. Previous assignee: `codex-mcp-client`; branch: `task/tick-064-mail-23-folder-policy`; worktree: `../pegasus-worktrees/tick-064`; taken at: `2026-08-20T09:54:49.769Z`. The branch and worktree coordinates are preserved here; this groom does not delete either.
