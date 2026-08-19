# Checklist — INTK-005

- [ ] Confirm PLAT-006 is merged and read EPIC-007 context plus the complete ticket folder.
- [ ] Record baseline focused Upload/intake test results in scratch.
- [ ] Add Core submission-group, member, result, and store/query contracts.
- [ ] Add the group submission use case and reuse `IIntakeSubmission` once per ordered member.
- [ ] Derive deterministic per-member receipt tokens/operation keys from group token plus ordinal.
- [ ] Add EF group/member entities with all required unique indexes and foreign keys.
- [ ] Implement idempotent/concurrency-safe group persistence and ordered queries.
- [ ] Add the grouped-intake migration, model snapshot, and only required runtime grants.
- [ ] Register the group store/use case through existing DI conventions.
- [ ] Add Core tests for empty, one-member, ordered multi-member, duplicate-name, replay, and partial-failure cases.
- [ ] Add persistence tests for constraints, concurrent replay, conflicting replay, and receipt-to-group lookup.
- [ ] Change authenticated Upload binding and markup to accept multiple files.
- [ ] Retain the native no-JavaScript path and extend PLAT-006 selected-file presentation accessibly.
- [ ] Validate all members and request bounds before staging; retain the form token on validation error.
- [ ] Process and dispose one file stream at a time.
- [ ] Add a group result/status composition that shows every member and uses existing receipt-status queries.
- [ ] Extend multipart test helpers and web tests for several files and preserved original filenames.
- [ ] Test duplicate filenames, empty/oversize members, aggregate rejection, partial success, exact replay, and conflict.
- [ ] Test keyboard and no-JavaScript behavior where browser coverage exists.
- [ ] Run `dotnet restore`.
- [ ] Run `dotnet build --configuration Release`.
- [ ] Run focused Core, persistence, web, and browser tests.
- [ ] Run full `dotnet test`.
- [ ] Perform and record the dated four-lens simplification pass.
- [ ] Update this checklist and write the post-implementation report with actual evidence.

## Execution progress — 2026-08-19

- [x] Rebased the private worktree onto merged PLAT-006 changes from `origin/dev`.
- [x] Added Core grouped-submission contracts and sequential orchestration around the existing per-file `IIntakeSubmission`.
- [x] Added EF group/member entities, constraints, migration, model snapshot, and DI registration.
- [x] Updated authenticated Upload binding, merged dropzone JavaScript, and group status page.
- [x] Added replay/conflict Core tests and a multi-file SQL-backed web integration test.
- [x] Updated the integration harness to drain every staged member of a group.
- [x] `dotnet restore` passed; Release build passed; focused Core and grouped web tests passed.
- [ ] Run the final full test suite, complete simplification review, commit, push, open PR, and move to Review.
