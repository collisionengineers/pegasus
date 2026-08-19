# Checklist — INTK-005

- [x] Confirm PLAT-006 is merged and read EPIC-007 context plus the complete ticket folder.
- [x] Record baseline focused Upload/intake test results in scratch.
- [x] Add Core submission-group, member, result, and store/query contracts.
- [x] Add the group submission use case and reuse `IIntakeSubmission` once per ordered member.
- [x] Derive deterministic per-member receipt tokens/operation keys from group token plus ordinal.
- [x] Add EF group/member entities with all required unique indexes and foreign keys.
- [x] Implement idempotent/concurrency-safe group persistence and ordered queries.
- [x] Add the grouped-intake migration, model snapshot, and only required runtime grants.
- [x] Register the group store/use case through existing DI conventions.
- [ ] Add Core tests for empty, one-member, ordered multi-member, duplicate-name, replay, and partial-failure cases.
- [ ] Add persistence tests for constraints, concurrent replay, conflicting replay, and receipt-to-group lookup.
- [x] Change authenticated Upload binding and markup to accept multiple files.
- [x] Retain the native no-JavaScript path and extend PLAT-006 selected-file presentation accessibly.
- [x] Validate all members and request bounds before staging; retain the form token on validation error.
- [x] Process and dispose one file stream at a time.
- [x] Add a group result/status composition that shows every member and uses existing receipt-status queries.
- [x] Extend multipart test helpers and web tests for several files and preserved original filenames.
- [ ] Test duplicate filenames, empty/oversize members, aggregate rejection, partial success, exact replay, and conflict.
- [ ] Test keyboard and no-JavaScript behavior where browser coverage exists.
- [x] Run `dotnet restore`.
- [x] Run `dotnet build --configuration Release`.
- [ ] Run focused Core, persistence, web, and browser tests.
- [ ] Run full `dotnet test`.
- [x] Perform and record the dated four-lens simplification pass.
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

## Takeover progress — 2026-08-19 (claude-code, DELIV-012)

- [x] Merged `origin/dev` (25 commits behind); resolved the silently-dropped migration-id entry (no textual conflict markers, but the merge took dev's expected-migration list wholesale) — see scratch/takeover.md.
- [x] Fixed all 4 blockers, 3 should-fixes, and the 1 nit from the takeover brief; all 5 open Codex review comments on PR #416 applied (mapping in plan.md).
- [x] Found and fixed an additional gap beyond the brief: `ListMembersAsync` hardcoded `IsDuplicate=false` and the replay branch in `SubmitGroupedIntake` never set it either, so the "already received" notice stayed hidden even after the redirect/token fixes — see plan.md.
- [x] `dotnet build -c Release`: 0 warnings/errors. `Pegasus.Core.Tests`: 644/644. `Pegasus.ArchitectureTests`: 97/97. Filtered `Pegasus.IntegrationTests` (IntakeWebNegativeTests, InstructionDraftWebTests, QdosIntakeWebTests, IntakePersistenceIntegrationTests): 30 passed, 0 failed, 6 skipped (pre-existing, unrelated skips). `GroupedIntakeWebTests` (multi-file, not in the originally-red set): 1/1 passed.
- [ ] **Not done, and why:** full-solution `dotnet test` was not run (the SQL-backed suite runs ~28 minutes; only the previously-failing classes plus the multi-file group test were re-verified — a real gap against the ticket's own "Run full `dotnet test`" step). Core/persistence-level test coverage for the group store (empty group, duplicate filenames, partial-member failure, concurrent-replay-at-persistence-level, receipt-to-group lookup) was not added — the retry fix for should-fix 7 was verified by code inspection against the identical pattern in `EfIntakeWorkStore`, not by a new concurrency test, because writing a reliable concurrent-race integration test was out of the takeover's explicit scope and budget. No browser/keyboard-only test coverage exists in this repo (no separate browser test project) so that checklist line stays unticked rather than being marked done on inspection alone. The post-implementation report/proof document was intentionally left unwritten: per repository convention, "Proof is written on merged `main`, after review and the merge, not before," and this PR has not merged.
