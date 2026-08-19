## Takeover — claude-code, 2026-08-19, DELIV-012, operator decision

Took over INTK-005 / PR #416 (branch `intk-005-grouped-upload`, worktree `.worktrees/intk-005`) from a prior agent by explicit operator decision. CI was red (sql-integration shards 1-3: 9 failures across IntakeWebNegativeTests x3, InstructionDraftWebTests x2, QdosIntakeWebTests x2, IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema) and five Codex review comments were unaddressed. Branch was 25 commits behind `origin/dev`.

### Dev merge
`git fetch origin && git merge origin/dev` completed with **no textual conflict markers**, but it silently dropped this branch's own `20260819101344_GroupedIntakeSubmission` id from the expected-migration list in `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` (dev's later migration ids landed on adjacent lines so git's line-based merge took dev's list wholesale). Restored the id in chronological order; verified the final list matches the actual `Migrations/` folder exactly (`diff` against `ls *.cs` sans Designer/snapshot files — identical).

### Fixes applied (see plan.md dated "Simplification pass / review dispositions — 2026-08-19" for full detail and Codex comment mapping)
1. `GroupedIntake.ChildToken` — ordinal 0 now keeps the parent token verbatim (`token`), only ordinal >= 1 gets `:n`.
2. `Upload.cshtml.cs` — one-member group redirects to `/Upload/Status/{id}` (with duplicate flag) exactly as before; only a genuine multi-file group goes to `/Upload/Group/{groupId}`.
3. `20260819101344_GroupedIntakeSubmission.cs` migration — added provider-guarded `GRANT SELECT, INSERT` for `pegasus_web_runtime_role` on both new tables (Web is the only runtime touching them; verified by reading `EfIntakeSubmissionGroupStore.cs` and `git grep -n "IntakeSubmissionGroup" -- src/` — no Worker reference at all, no UPDATE/DELETE anywhere in the store).
4. Migration list — see merge note above.
5. `IntakeEnvelopeLimits.MaximumBatchFileCount` (20) + `MaximumBatchContentLength` added; `Program.cs` derives `MultipartBodyLengthLimit` from it; `Upload.cshtml.cs` validates the count with a named message; `Upload.cshtml` copy reuses `MaximumFileCount`.
6. `UploadGroupStatus` — added `RefreshAutomatically` + `data-auto-refresh="2000"`, gated on any member being non-terminal (or status not yet resolved), mirroring `UploadStatus.cshtml`.
7. `EfIntakeSubmissionGroupStore` — added the same 3-attempt SQL-concurrency retry shape as `EfIntakeWorkStore.ReceiveWithRetryAsync` to **both** `AddMemberAsync` and `GetOrCreateAsync` (Codex flagged both had the same race window; the operator's blocker text only named `AddMemberAsync`'s lines but I extended to `GetOrCreateAsync` too since the finding applies identically there).
8. Restored the "already received; no duplicate created" replay notice on the redirect.

### A gap found beyond the eight listed items
Fixing #1/#2/#8 wasn't sufficient on its own: `EfIntakeSubmissionGroupStore.ListMembersAsync` hardcodes `IsDuplicate = false` for every member it reads back (it has no per-call knowledge of duplication), and `SubmitGroupedIntake.ExecuteAsync`'s replay branch (`existing is not null` → `continue`) never called `IIntakeSubmission` at all, so it never even had an `IsDuplicate` value to record. Net effect: every replay of a one-member group always reported `IsDuplicate=false`, silently eating the notice regardless of the redirect fix. Fixed by tracking `IsDuplicate` per ordinal during `SubmitGroupedIntake`'s own loop (true when `FindMemberAsync` found an existing row; otherwise the submission's own `IsDuplicate`) and stamping it onto the members returned by `ListMembersAsync` before building the result. Found this via the first integration test run (`InstructionDraftWebTests.SameManualUploadTokenReplaysOneReceiptDraftAndAssetSet` and `QdosIntakeWebTests.ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage` both failed on "was already received" not found even after the redirect fix).

Also restored singular "That file is empty." wording for a one-file submission (multi-file keeps "File N is empty.") — `IntakeWebNegativeTests.EmptyUploadReturnsValidationAndDoesNotPersist` asserted the pre-grouping singular wording.

### Verification run so far
- `dotnet build ./Pegasus.slnx -c Release` — 0 warnings, 0 errors.
- `dotnet test tests/Pegasus.Core.Tests -c Release` — 644/644 passed.
- `dotnet test tests/Pegasus.ArchitectureTests -c Release` — 97/97 passed.
- `dotnet test tests/Pegasus.IntegrationTests --filter "IntakeWebNegativeTests|InstructionDraftWebTests|QdosIntakeWebTests|IntakePersistenceIntegrationTests"` — first run (before the duplicate-flag/empty-message fix): 27 passed, 3 failed, 6 skipped. Second run in progress after the fix; result to follow in plan.md and the final report.

### Sibling branch note (read-only, not touched)
`intk-006-grouped-image-routing` (commit 866d305e, not merged anywhere) independently fixed the single-file redirect by bypassing the group path entirely for `Upload.Length == 1` (calling `IIntakeSubmission` directly from the page). I did **not** import that approach — it duplicates the submission call path outside Core's one orchestration use case. My fix keeps everything going through `IGroupedIntakeSubmission` for every upload (including one-file) and only branches the *redirect* on member count, per the operator's explicit blocker-2 instructions. Per the ticket plan's "Parallel-branch execution note", INTK-006 is expected to rebase onto this branch later — flagging here so whoever does that reconciliation knows the two branches solved the same symptom differently.

Not touched: `.worktrees/kanmer`, `dev`, `main`, any other worktree. No merge to `dev` performed or attempted.
