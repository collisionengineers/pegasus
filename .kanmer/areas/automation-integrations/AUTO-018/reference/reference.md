# Review record — AUTO-018 (PR https://github.com/collisionengineers/pegasus/pull/654)

Reviewer family: Claude (Opus) dispositions over an independent
gpt-5.6-terra xhigh read; the PR was built by the Codex family.
Head SHA reviewed: `265a09277cfdf6249af1440a619ceaf375c02fbb`.
Review checkout: `.worktrees/auto-018-review` (detached, read-only).
Date: 2026-09-03.

## Verdict

**REQUEST CHANGES.** One blocker: the new `AiJobs` check constraint is
unbalanced T-SQL, so *every* migration application fails and the whole SQL
surface of the product is unbootable on this branch. Reproduced locally and
red on five CI lanes. Four should-fix findings and two nits follow it. No
scope drift, no forbidden path, no weakened assertion, no explanatory copy,
no second operator label, and the migration needs no grant change.

## Findings

| # | Severity | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| R1 | **blocker** | `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs:246-266` (and the same literal in `Migrations/20260903195515_MarketResearchAiJob.cs:106` and `PegasusDbContextModelSnapshot.cs:598`) | `CK_AiJobs_MarketResearchResult` has **two opening and three closing parentheses**. The second branch is closed early after `<> 'MarketResearch')` and a stray `)` remains at the end of the string, so EF emits `... AND [MarketResearchCompletionHash] IS NULL));` and SQL Server rejects it with `Incorrect syntax near ')'`. Every migration run fails at this statement, so no database can be created. | **Fix (implementer).** Balance the expression — wrap the whole second branch: `(([ResultKind] IS NULL OR [ResultKind] <> 'MarketResearch') AND … IS NULL)` — in the model configuration, then **regenerate** the migration, its Designer and the model snapshot rather than hand-editing them. Then run the SQL integration suite locally at least once before pushing. |
| R2 | should-fix | `src/Pegasus.Infrastructure/Persistence/EfMarketResearchAiJobCompletionStore.cs:55` | Replay is keyed only on `AiJobEntity.LastOperationKey`. The staff confirmation overwrites that field (`EfAiJobStore.cs:163`), so a connector retry of the original completion key after confirmation no longer replays: it falls through to `RequireTakenJob` and is refused. No duplicate document or valuation is written, so this is a lost idempotency guarantee rather than data loss, but the plan's Step 3 acceptance says replay returns the original result. | **Fix (implementer).** Resolve the completion from its own persisted evidence (the `ai_job_draft_ready` history entry or the stored `MarketResearchCompletionHash`) instead of the mutable last-operation key, and add a replay-after-staff-confirmation test. |
| R3 | should-fix | `src/Pegasus.Infrastructure/Persistence/EfMarketResearchAiJobCompletionStore.cs:198-224` | `RequireTakenJob` re-implements the taken-state, lease-lapse, holder and version rules with raw `nameof` string comparisons instead of reusing `AiJobPolicy.EffectiveState` / `AiJobPolicy.IsLegalTransition`, which `EfAiJobStore.TransitionAsync` (`:108-140`) uses for every other kind. That is a second implementation of a Core-owned vocabulary (one list per concept), and it diverges: a lapsed lease is refused outright here, where the ledger's own path records `ai_job_expired` and re-queues. | **Fix (implementer).** Express the precondition through the existing Core predicates and keep the concurrency recheck in the transaction. |
| R4 | should-fix | `src/Pegasus.Infrastructure/Persistence/EfMarketResearchAiJobCompletionStore.cs:152-166` | The failure path is a plainer copy of `EfDocumentCustodyStore.AddAsync`'s (`:62-100`): if `RollbackAsync` itself throws, content cleanup is skipped and the newly written artifact is orphaned with no aggregated failure. The refactor shared the happy path (`PrepareAddAsync`) but not the compensation path. | **Fix (implementer).** Share the rollback/aggregate-failure handling too, so both callers compensate identically. |
| R5 | should-fix | `tests/Pegasus.IntegrationTests/AutomationAiJobIngressTests.cs:380-452`; `AssessmentPersistenceIntegrationTests.cs:857-935` | The added tests prove success, immediate replay and changed-payload refusal. The plan's Step 6 additionally requires — each proved separately — a missing `automation.jobs` scope, a missing and an expired case edit lease, a stale Case or job version leaving nothing written, completion after the Administrator switch-off, and compensation of a failed content write. None of those is asserted. | **Fix (implementer).** Add the focused refusal tests, each asserting the document and valuation row counts are unchanged after the refusal. |
| R6 | nit | `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs:253-255` | In the MarketResearch branch the money and mileage columns are guarded only by `>= 0`, which is UNKNOWN when the column is NULL, so a check constraint is satisfied by a NULL. The three sibling GUID/date columns use `IS NOT NULL`. | **Fix with R1** — add `IS NOT NULL` beside each `>= 0` while the constraint is being corrected. |
| R7 | nit | `src/Pegasus.Core/AiWork/AiJobOperations.cs:608-614` | The completion use case passes a synthetic `new(AiJobResultKind.MarketResearch, "pending", null)` result purely to satisfy `ValidateTransition`'s "a Draft ready job names its result" rule; the value never reaches persistence. | **Accept risk.** Cosmetic; the alternative is a validator overload that this ticket has no second caller for. Fix only if it is free while addressing R1–R5. |
| R8 | — | `src/Pegasus.Web/Mcp/AiJobMcpTools.cs:68,205,250` | terra flagged the tool descriptions naming the raw `MarketResearch` enum rather than the `Market research` operator label. | **Rejected.** The MCP wire vocabulary is a machine contract, not an operator surface: `AiJobToolItem.Kind` is already `job.Kind.ToString()` on `dev` and `ListAsync`'s existing description already enumerates the raw kinds. `OperatorLabels` governs rendered operator text, and exactly one label — `Market research` — was added there. |
| R9 | — | branch vs `origin/dev` | The branch's merge base is `659cec77`; `origin/dev` has since taken ENG-035 (`20260903110926_ExtendAssessmentVocabulary`) and PLAT-070 (`20260903153134_RemoveStaffReviewFlags`). Both sort **before** this migration, so the order is fine, but the branch's model snapshot predates them and its migration census (`IntakePersistenceIntegrationTests.cs:114-118`) omits both. | **Fix (implementer).** Merge `origin/dev` (never rebase), regenerate the snapshot on the merged model with the R1 correction, and extend the census to all three new migrations. |

## What was verified and found sound

- Owned paths only. Nothing outside the ticket's allocation is touched; no
  `_CaseValuation.cshtml`, `Details.cshtml[.cs]`, `site.css`, `site.js` or
  `docs/design/test-ui/**` change; no guide-month field or column anywhere;
  no `ValuationSource` operator label map (both correctly left to CASE-029).
- D44 honoured: `ReviewAction` and the Operations markup are unchanged and
  only `CanCompleteByHand` gained the kind; no review flag, checkbox, dialog
  or history event exists. The staff closure runs through the existing
  `IConfirmAiJob` handler (`Operations/Index.cshtml.cs:197-215`), which the
  new `WorkAiJob.CompleteAsync` guard does not intercept.
- The generic path genuinely refuses the kind (`AiJobOperations.cs:489-495`)
  with a regression test (`AiJobTests.GenericCompletionRefusesMarketResearch`),
  and the custody helper is genuinely shared, not duplicated
  (`EfDocumentCustodyStore.PrepareAddAsync`); the single 10 MiB constant now
  lives once in `AutomationMcpErrors`. The simplification pass's four claims
  all check out against the code.
- No assertion was weakened. The removed `ValuationTests` case asserting that
  Automation may save a Glass's valuation was replaced by a stronger one
  proving Automation is now refused on the staff save path, which is exactly
  the Step 2 narrowing (`Valuations.cs:141-145`); `StaffAuthorization`
  otherwise grants `PerformCasework` to Automation, so the added check is
  load-bearing rather than tautological.
- Grants: the migration adds columns, indexes and check constraints to two
  existing tables and creates none, so the approved permission matrix in
  `scripts/Invoke-AzureDatabaseBootstrap.ps1:353-404` is unchanged. The
  claim is true.

## Commands and exit codes (review checkout, head 265a0927)

| Command | Exit |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | **0** |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | **0** — 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.IntegrationTests/... --filter "FullyQualifiedName~IntakePersistenceIntegrationTests"` | **1** — Failed 10, Passed 0; every failure `SqlException: Incorrect syntax near ')'` inside `Migrator.MigrateAsync` |
| `dotnet ef database update --verbose` (diagnostic, throwaway LocalDB) | **failed** — names the offending statement: `ALTER TABLE [AiJobs] ADD CONSTRAINT [CK_AiJobs_MarketResearchResult] …` |

Scope rationale: the changed types are the AI job ledger, the valuation
vocabulary, the completion transaction and the MCP ingress, all of which are
exercised by `Pegasus.IntegrationTests` against SQL. That project is the
right targeted scope, and it cannot create a database at all on this head, so
no narrower run would have been meaningful; `Update-TestUiSnapshots.ps1` was
not run because `docs/design/test-ui/**` is unchanged. The full suite was
left to CI per the review instructions.

## CI on the pull request (run 33800588799, head 265a0927)

| Lane | Result |
| --- | --- |
| `unit`, `changes`, `documentation`, `reference-data`, `local-development-scripts`, `sql-integration-coverage` | pass |
| `sql-integration (1)`, `(2)`, `(3)` | **fail** — the R1 migration failure |
| `browser` | **fail** — same |
| `test-ui` | **fail** — same |

The sibling PR #653, on the same base, has all three `sql-integration`
shards green, so the failure is this branch's own and not a `dev` breakage.

## Outcome

Not merged. AUTO-018 stays in Review with R1 as a merge blocker and R2–R6,
R9 to be addressed on the same branch; R7 accepted, R8 rejected with reason.
