# Checklist — TICK-064

- [x] Add the typed Core logical-folder catalogue/policy and exhaustive no-recommendation mapping tests without changing MAIL-02 queue policy.
- [x] Add validated approved-mailbox folder-binding contracts with preserve-versus-replace semantics and replay/history coverage.
- [x] Add the normalized EF binding entity/configuration/store mapping and one migration without touching retained-message persistence.
- [x] Add read-only exact Graph folder discovery and the administrator resolve/display caller with no client-supplied folder identities or Graph writes.
- [x] Add focused persistence, fake-Graph and Web caller tests for scope, ambiguity, authorization, version/replay and honest unconfigured results.
- [ ] Run locked restore, Release build, focused tests and the full relevant suite.
- [ ] Run the four-lens simplification pass, apply safe findings, and record dated dispositions in plan.md.
- [ ] Commit/push, open the PR to dev, write the post-implementation report, record traceability and move the ticket to Review.

## Progress notes

- 2026-08-20: `dotnet restore --locked-mode` and Release build pass. Focused Core policy/administration tests pass (84); focused Graph resolver, Local resolver, mailbox persistence and Web caller tests pass (14). Full suite and simplification remain.
