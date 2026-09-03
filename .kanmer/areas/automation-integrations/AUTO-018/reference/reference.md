# Review record — AUTO-018 (PR https://github.com/collisionengineers/pegasus/pull/654)

- Reviewer family: Claude (Opus) wrapping gpt-5.6-terra xhigh — the other
  family from the implementer (gpt-5.6-sol / Codex).
- Head SHA reviewed: `265a09274b2c4e2d` (branch
  `task/auto-018-market-research-job`, three commits on top of `origin/dev`).
- Review checkout: detached worktree `.worktrees/auto-018-review` at
  `origin/task/auto-018-market-research-job`.
- **Verdict: REQUEST CHANGES.** One confirmed blocker stops the merge: the new
  check constraint's SQL has unbalanced parentheses and every migration apply
  fails. Not merged; the ticket stays in Review.

## Commands run in the review checkout, with exit codes

| Command | Exit | Why this scope |
| --- | --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | `RESTORE_EXIT=0` | Locked restore of the whole solution. |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | `BUILD_EXIT=0` — 0 warnings, 0 errors | Whole solution; the branch touches Core, Infrastructure, Web and three test projects. |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | `CORE_TESTS_EXIT=0` — 1198 passed, 0 failed | Covers the changed Core types: `AiJobPolicy`, `CreateAiJob`, `WorkAiJob`, `CompleteMarketResearchAiJob`, `ValuationPolicy`. |
| `dotnet test ./tests/Pegasus.IntegrationTests/... --filter "FullyQualifiedName~IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema"` | **`MIGRATION_TEST_EXIT=1` — FAILED** | The one test that applies every committed migration to LocalDB; the branch adds a migration. `Microsoft.Data.SqlClient.SqlException : Incorrect syntax near ')'`. |
| `pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1` | `GRANTS_EXIT=0` — "88 migration files checked, every created table is granted or exempted" | A migration was added. |

The full suite was not re-run locally; GitHub CI runs it on the PR. At the time
of writing, CI `unit`, `changes`, `documentation`, `local-development-scripts`
and `reference-data` had passed and the three `sql-integration` shards were
still pending — they exercise the same migration path that failed locally, so
they are expected to go red on finding 1.

## Findings and dispositions

| # | Severity | Source | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- | --- |
| 1 | **blocker** | Claude | `src/Pegasus.Infrastructure/Persistence/Migrations/20260903195515_MarketResearchAiJob.cs:109` (and the identical string in `PegasusDbContextModelSnapshot.cs:598` and `AssessmentModelConfiguration.cs:246-266`) | The `CK_AiJobs_MarketResearchResult` expression has unbalanced parentheses — depth −1. The second branch opens `([ResultKind] IS NULL OR [ResultKind] <> 'MarketResearch')`, closes it, and then ends the whole string with an extra `)`. `ALTER TABLE ... ADD CONSTRAINT ... CHECK (...)` is a syntax error, so **no database can apply this migration**. Reproduced locally: `Incorrect syntax near ')'` from `LocalDbTestDatabase.MigrateAsync`. | **Sent to the implementer.** Not fixed here (the review checkout makes no code changes). Fix in all three places: wrap the whole second branch, i.e. `(([ResultKind] IS NULL OR [ResultKind] <> 'MarketResearch') AND ... IS NULL)`. Regenerate rather than hand-edit if the snapshot can be regenerated. Re-run the migration census test. |
| 2 | **should-fix** | terra | `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs:254-259` | Inside the same expression the three money/mileage columns are tested only with `>= 0`, never `IS NOT NULL`. SQL Server passes a CHECK that evaluates to UNKNOWN, so a `MarketResearch` result row could keep null mileage, retail or trade. | **Accepted; sent to the implementer** — it is the same expression as finding 1 and must be fixed in the same edit: add `[MarketResearchMileage] IS NOT NULL AND [MarketResearchMileage] >= 0` (and the same for retail and trade), then prove the rejection at migration level. |
| 3 | **should-fix** | terra | `src/Pegasus.Infrastructure/DependencyInjection.cs:358-359` vs `:412`, `:461-473` | `IMarketResearchAiJobCompletionStore` / `ICompleteMarketResearchAiJob` are registered unconditionally, but `EfMarketResearchAiJobCompletionStore` takes `IDocumentContentStore`, which the composition registers only when `composesDocumentSurface` is true — exactly where `EfDocumentCustodyStore` and `IAddCaseDocument` are registered. A no-storage profile therefore throws at resolution instead of failing closed the way `UnavailableCaseCustody` does. Verified: the two registrations sit in the unconditional block; every other custody-dependent service sits inside `if (composesDocumentSurface)`. | **Accepted; sent to the implementer.** Move both registrations inside the `composesDocumentSurface` block beside the custody registrations, matching the file's own stated rule ("a profile must never silently resolve a different service set"). Downgraded from terra's "blocker" because production always composes the document surface, so no shipped profile is broken today — but it is a real divergence from the established composition and is cheap to correct while the branch is open. |
| 4 | **should-fix** | terra | `src/Pegasus.Infrastructure/Persistence/EfMarketResearchAiJobCompletionStore.cs:150-165` | The catch block calls `await transaction.RollbackAsync(...)` outside a try, so a rollback failure skips `DocumentContentRollback.RemoveOrphanAsync` and leaves orphaned custody content. The path this store was refactored out of handles it explicitly (`EfDocumentCustodyStore.cs:62-104`: rollback failure captured, cleanup still attempted, failures aggregated). Verified by reading both. | **Accepted; sent to the implementer.** Reuse the `EfDocumentCustodyStore` failure shape verbatim. Conduct rule 11 (concurrency results are never discarded) and the plan's own Step 3 acceptance ("an internal failure compensates any newly written content artifact"). |
| 5 | **should-fix** | terra + Claude | `tests/Pegasus.IntegrationTests/AutomationAiJobIngressTests.cs:379-451`, `AssessmentPersistenceIntegrationTests.cs:859-936` | The checklist ticks "missing `automation.jobs` and missing/expired lease refused separately" and the plan's Step 6 requires those two refusals, plus a stale-version/lost-lease case leaving no document, valuation or transition. Neither test file asserts any of them for the new tool: the ingress test covers only the happy path, the replay, the row counts and the actor attribution, and the existing scope test (`:110-136`) exercises `pegasus_ai_job_list`, not `pegasus_ai_job_complete_market_research`. | **Accepted; sent to the implementer.** Add the negative paths the checklist already claims, or un-tick the claim. Tests must prove the claim (conduct rule 19). |

Findings terra raised and I did not carry: none — its four findings are all
above (one re-severitied). Findings I raised beyond terra's: finding 1, the
merge-blocking one; terra read the migration and reported it as clean.

## What I checked and found correct

- **Scope and owned paths.** All 23 changed files are inside the ticket's owned
  set. No `_CaseValuation.cshtml`, `Details.cshtml[.cs]`, `site.css`,
  `site.js`, `docs/design/test-ui/**`, `docs/operator-notes.md` or `corpus/`.
  No guide-month field anywhere (D40 — CASE-029 owns it); no valuation
  adjustment, rationale or revaluation-history type (TICK-083); no damage type
  (D45); no Create-Case route (D50); no vehicle-record extension (D49).
- **D35 / D44.** Automation completion ends at `DraftReady`
  (`EfMarketResearchAiJobCompletionStore.cs:118`); `Completed` stays the staff
  act through the existing `Complete job` confirmation, reached by the
  one-token `CanCompleteByHand` extension
  (`Pages/Operations/Index.cshtml.cs:443-449`). No review flag, checkbox,
  dialog or history event is added; `ReviewAction` and the markup are
  untouched. No AutoTrader integration or scraping.
- **Core owns policy.** One eligibility list — `MarketResearch` reuses
  `AiJobPolicy.IsEligibleEstimateCaseState`
  (`AiJobOperations.cs:358`). One document-size rule — `MaximumDocumentBytes`
  moved to `AutomationMcpErrors.cs:19` and read by `DocumentMcpTools`
  (`:143`, `:206`, `:296`); no copy of the number remains. One
  `ValuationSource` vocabulary (`Valuations.cs:13`).
- **The narrowing is real, not decorative.** `StaffAuthorization.PerformCasework`
  admits Automation, so the new `actor.Kind != ActorKind.Staff` guard in
  `ValuationPolicy.Record` (`Valuations.cs:141-145`) genuinely closes the staff
  save/edit path to Automation; `ValuationTests.cs:157-162` replaces the old
  "Automation may save a Glasses row" assertion with its refusal. An assertion
  was changed, not weakened.
- **Simplification-pass dispositions are honest.** Both claimed fixes exist:
  the generic path refuses `MarketResearch`
  (`AiJobOperations.cs:490-497`, proved by
  `AiJobTests.GenericCompletionRefusesMarketResearch`), and the custody helper
  is genuinely shared — `PrepareAddAsync` was moved out of
  `EfDocumentCustodyStore.AddAsync` and is called by both
  (`EfDocumentCustodyStore.cs:768`, `EfMarketResearchAiJobCompletionStore.cs:78`),
  not copied.
- **Evidence tier stated honestly.** Nothing on the branch creates a
  `MarketResearch` job in production — `pegasus_ai_job_create` still admits
  `UnidentifiedQueuePass` only (`AiJobMcpTools.cs:117-132`) — and the
  post-implementation report says so and names CASE-029 as the activating
  ticket. No delivery claim is made.
- **Labels and copy.** Exactly one label added,
  `OperatorLabels.cs:1023` (`Market research`). No `ValuationSource` label map.
  No Razor markup change, no explanatory copy.
- **Migration shape (apart from finding 1).** No `CREATE TABLE`, so the
  unchanged grant matrix is correct and `Test-MigrationGrants.ps1` passes;
  columns are additive and nullable; `Down` drops what `Up` adds and restores
  the prior constraints; the census is updated
  (`IntakePersistenceIntegrationTests.cs:118`).

## Next step

Findings 1–5 go back to the implementer on
`task/auto-018-market-research-job`. Finding 1 must be fixed before the PR can
go green; 2–5 ride the same push. No merge, no stage move.
