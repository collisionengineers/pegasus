# Post-implementation report — AUTO-018

## Outcome

Implemented the `MarketResearch` AI job kind through the highest evidence
tier available to this ticket: the Core kind/policy and typed completion
contract, one serializable EF completion transaction, the named MCP
completion tool, the schema migration, and the existing Operations staff
closure action. Core and architecture tests pass; the migration grant matrix
is unchanged and verified.

**Not activated end-to-end.** `pegasus_ai_job_create` still accepts only
`UnidentifiedQueuePass` (`AiJobPolicy` refuses Automation creation of any
other kind). The production creation caller — the Case Valuation-section
"AI market research" button — belongs to [[CASE-029]], per the ticket body
and D40/`docs/frd/frd-06-vehicle-and-engineering-evidence.md:218-220`.
CASE-029 also owns the `ValuationSource` operator label map and the guide
month field; when it adds guide month it must extend this ticket's
MarketResearch completion contract if the external connector is to supply
one — no guide-month field exists on this branch. Until CASE-029 merges its
caller, no production path creates a MarketResearch job, so "job appears in
Operations" cannot be demonstrated end-to-end; this report and `proof.md`
make no such claim.

## Files changed

Core:
- `src/Pegasus.Core/AiWork/AiJobs.cs` — `AiJobKind.MarketResearch`, typed
  completion command/result.
- `src/Pegasus.Core/AiWork/AiJobOperations.cs` — Case-only eligibility
  (reuses `IsEligibleEstimateCaseState`), MarketResearch completion use
  case ending at `DraftReady`; generic completion path explicitly refuses
  `MarketResearch`.
- `src/Pegasus.Core/Assessment/Valuations.cs` — `ValuationSource.AiMarketResearch`,
  Automation admitted only through the new completion use case.

Infrastructure:
- `src/Pegasus.Infrastructure/Persistence/EfMarketResearchAiJobCompletionStore.cs`
  (new) — one serializable transaction: one custody document, one
  `AiMarketResearch` valuation, the `DraftReady` job transition, replay-safe
  on operation key.
- `AssessmentEntities.cs`, `AssessmentModelConfiguration.cs`,
  `EfAiJobStore.cs`, `EfValuationStore.cs`, `EfDocumentCustodyStore.cs`
  (refactored shared custody-preparation helper), `DependencyInjection.cs`
  — supporting mapping, precision/checks, and DI registration.
- `Migrations/20260903195515_MarketResearchAiJob.cs` + `.Designer.cs`,
  `PegasusDbContextModelSnapshot.cs` — expanded `AiJobs`/`CaseValuations`
  check constraints and typed result columns; no new table, no grant-script
  change needed.

Web/MCP:
- `src/Pegasus.Web/Mcp/AiJobMcpTools.cs` — new named tool
  `pegasus_ai_job_complete_market_research` (`automation.jobs` scope);
  generic `pegasus_ai_job_complete` unchanged, refuses the new kind.
- `src/Pegasus.Web/Mcp/AutomationMcpErrors.cs`,
  `src/Pegasus.Web/Mcp/DocumentMcpTools.cs` — moved the single
  `MaximumDocumentBytes` (10 MiB) constant to `AutomationMcpErrors`.
- `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs` — `MarketResearch`
  added to `CanCompleteByHand`; `ReviewAction` and markup unchanged.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — one label,
  `Market research`, in `OperatorLabels.AiJobs.Kind`. No `ValuationSource`
  label map added (CASE-029's).

Tests:
- `tests/Pegasus.Core.Tests/AiWork/AiJobTests.cs`,
  `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs` — catalogue,
  eligibility matrix, actor/source isolation, generic-completion refusal.
- `tests/Pegasus.IntegrationTests/AutomationAiJobIngressTests.cs`,
  `AssessmentPersistenceIntegrationTests.cs`,
  `IntakePersistenceIntegrationTests.cs` (migration census),
  `OperationsWebTests.cs` — written and compiled; not executed locally (see
  Commands below).

Files this ticket deliberately did not touch:
`_CaseValuation.cshtml`, `Details.cshtml[.cs]`, `site.css`, `site.js`,
`docs/design/test-ui/**` (CASE-029); TICK-083's valuation adjustment/
rationale/revaluation-history types; no guide-month column anywhere.

## Commands and exit codes

Run independently by the Codex implementer and again by the Claude
orchestrator, both in the ticket worktree `.worktrees/auto-018`:

| Command | Exit |
| --- | --- |
| `git merge --no-edit origin/dev` | 0 (fast-forwarded to `659cec77`) |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 — 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 — 1,198 passed, 0 failed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 — 100 passed, 0 failed |
| `./scripts/Test-MigrationGrants.ps1` | success — "88 migration files checked, every created table is granted or exempted" |

`Pegasus.IntegrationTests` and the full `Pegasus.slnx --filter
"Category!=Corpus&Category!=Browser"` run were **not executed locally**, by
design: the orchestrator's execution instructions reserve that ~26-minute
suite for GitHub CI's sharded run on the PR, which is the gate the reviewer
blocks on. The integration test source changes compile cleanly in the
Release build above. No routed Razor page markup changed (only a page-model
predicate), so `Update-TestUiSnapshots.ps1` / `Test-UiCatalogue.ps1` were not
run — consistent with the plan's stated expectation.

## Deviations from the plan

None. The plan's settled resolution (`MarketResearchCompletionTargetState =>
AiJobState.DraftReady`), Step 4b's `CanCompleteByHand` wiring, the two-scope
MCP authorization workflow, the shared-lock protocol for
`OperatorLabels.cs`/`Persistence/Migrations/**`, and the deferred guide-month/
`ValuationSource`-label/creation-caller hand-off to CASE-029 were all followed
exactly as specified after the 2026-09-03 plan review.

One process note, not a scope deviation: an earlier attempt at this same
ticket failed before touching any file, due to a transient Windows
process-launch failure (`0xC0000142`) unrelated to the code; the environment
was healthy on this run and the worktree was empty/clean at the start of it.

## Simplification pass

Recorded in the ticket plan under "Simplification pass (2026-09-03)" —
summary: fixed the generic completion path initially admitting
`MarketResearch` after the result vocabulary expanded (now explicitly
refused, with a regression test); removed a tautological Core assertion;
kept the one specialised EF completion transaction and the typed persisted
result columns as necessary, with reasons recorded. No unapplied findings,
no scope drift.

## PR

https://github.com/collisionengineers/pegasus/pull/654

## Review round fixes (2026-09-04)

Applied the review record's findings (`reference/reference.md`, PR #654 head
`265a09277`) in the ticket worktree `.worktrees/auto-018`, branch
`task/auto-018-market-research-job`. R7 was accepted risk and R8 was already
rejected in the review itself; neither required a code change.

- **R1 (blocker — unbalanced check constraint).** Fixed at its source,
  `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs`:
  the second branch of `CK_AiJobs_MarketResearchResult` is now wrapped in its
  own parenthesis —
  `(([ResultKind] IS NULL OR [ResultKind] <> 'MarketResearch') AND … IS NULL)`
  — giving the whole expression 3 opening and 3 closing parentheses (verified
  by direct count). The migration and Designer were deleted and regenerated
  with `dotnet ef migrations add MarketResearchAiJob` against the corrected
  model rather than hand-edited, so the fix flows through automatically; see
  R9 below for the final migration id.
- **R6 (nit — NULL satisfies `>= 0`).** Fixed in the same constraint, same
  file: each `[MarketResearchMileage/RetailValue/TradeValue] >= 0` now has a
  preceding `IS NOT NULL` guard, matching the sibling GUID/date columns.
- **R2 (should-fix — replay breaks after staff confirmation).**
  `EfMarketResearchAiJobCompletionStore.CompleteAsync` now detects a replay
  by querying the job's own `ai_job_draft_ready` `ActionHistory` entry
  (matched on `AggregateType`/`AggregateId`/`EventKind`/`CorrelationId` ==
  the presented operation key) instead of the mutable `LastOperationKey`
  column, which the staff confirmation overwrites. Added
  `MarketResearchCompletionReplaySurvivesStaffConfirmation` (already present
  uncommitted from the prior session) proving a retry after
  `IConfirmAiJob.ExecuteAsync` still replays the original document/valuation.
- **R3 (should-fix — reimplemented taken/lease rules).** `RequireTakenJob`
  now resolves the job's effective state through `AiJobPolicy.EffectiveState`
  and checks the transition with `AiJobPolicy.IsLegalTransition`, matching
  `EfAiJobStore.TransitionAsync`'s own precondition handling, instead of raw
  `nameof`/lease-timestamp comparisons. `EfAiJobStore.AggregateType` was made
  `internal` so the completion store's replay check (R2) can reuse it.
- **R4 (should-fix — copied, incomplete rollback path).** The completion
  transaction's catch block now mirrors
  `EfDocumentCustodyStore.AddAsync`/`EfDocumentRequestStore`'s established
  pattern exactly: a failed `RollbackAsync` and a failed orphan cleanup are
  each captured and surfaced as an `AggregateException` rather than the
  cleanup being skipped when rollback itself throws.
- **R5 (should-fix — missing refusal/compensation tests).** Added the tests
  Step 6's acceptance list required and the review found missing, each
  asserting document/valuation row counts are unchanged on refusal:
  - `AutomationAiJobIngressTests.MarketResearchCompletionEnforcesTheJobsScope`
    (missing `automation.jobs` scope) — already present uncommitted.
  - `AutomationAiJobIngressTests.MarketResearchCompletionRefusesAMissingCaseLeaseWithoutChangingTheJob`
    — already present uncommitted.
  - `AssessmentPersistenceIntegrationTests.MarketResearchCompletionRefusesAnExpiredCaseLeaseWithoutChangingTheJob`
    (new) — acquires a real case edit lease, advances the clock past its
    5-minute duration, and asserts `CaseEditLeaseExpiredException`.
  - `AssessmentPersistenceIntegrationTests.MarketResearchCompletionWithAStaleCaseVersionWritesNothing`
    — already present uncommitted.
  - `AssessmentPersistenceIntegrationTests.MarketResearchCompletionWithAStaleJobVersionWritesNothing`
    (new) — presents a stale `ExpectedJobVersion` and asserts "The AI job
    changed concurrently; reload and retry."
  - `AutomationAiJobIngressTests.MarketResearchCompletionSucceedsWhileAutomationIsSwitchedOff`
    (new) — proves finishing a claimed MarketResearch job is not gated by
    the Administrator switch (only new claims/progress are, matching
    `TheAdministratorSwitchRefusesClaimsAndProgressButNotFinishing`).
  - `AssessmentPersistenceIntegrationTests.MarketResearchCompletionCompensatesAFailedContentWriteByRemovingTheOrphan`
    (new) — a `ThrowingCommandInterceptor` forces the `CaseValuations` insert
    to fail after the document content is already stored; asserts the
    transaction rolls back, the orphaned content is deleted
    (`DeleteCount == 1`), and no rows are left in `CaseValuations` /
    `DocumentOccurrences`.
  - Replay and changed-payload refusal were already covered by
    `MarketResearchCompletesOverHttpWithCaseLeaseDocumentValuationAndActorHistory`
    and its sibling tests.
- **R9 (fix — migration ordering vs a moving `origin/dev`).** `origin/dev`
  moved twice during this round: first to ENG-035/PLAT-070 (already merged
  onto this branch before the round started), then to PLAT-068's
  `20260903225331_StaffAccountSignOff` mid-session. Ran `git fetch origin` +
  `git merge --no-edit origin/dev` (one conflict, in the migration census —
  resolved by keeping both entries in chronological order), deleted the
  hand-edited `20260903195515_MarketResearchAiJob` migration and Designer,
  restored `PegasusDbContextModelSnapshot.cs` to `origin/dev`'s tip
  (`3f0cb45ed`), and regenerated with
  `dotnet ef migrations add MarketResearchAiJob --project
  ./src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj --startup-project
  ./src/Pegasus.Web/Pegasus.Web.csproj`. The new migration is
  **`20260903233954_MarketResearchAiJob`**, sorting after
  `20260903225331_StaffAccountSignOff`. `IntakePersistenceIntegrationTests.cs`'s
  applied-migration census now lists every migration in chronological order
  ending with it. No grant SQL was needed (no new table). A final
  `git fetch origin` confirmed `origin/dev` stayed at `3f0cb45ed` for the
  rest of the round.

### Commands and exit codes (worktree `.worktrees/auto-018`, head `582796b04`)

| Command | Exit |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 — 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 — 1,219 passed, 0 failed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 — 100 passed, 0 failed |
| `./scripts/Test-MigrationGrants.ps1` | 0 — "91 migration files checked, every created table is granted or exempted" |

No routed Razor page or partial changed by this round (Operations'
`CanCompleteByHand` remains a page-model predicate only), so
`Update-TestUiSnapshots.ps1` / `Test-UiCatalogue.ps1` were not run, consistent
with the original plan. `Pegasus.IntegrationTests` was not run locally by
design; GitHub CI runs it sharded on the PR, which is the gate the reviewer
blocks on — the new/changed integration tests above compile cleanly in the
Release build.

### Head SHA and push

New branch head: `582796b04` (pushed to
`origin/task/auto-018-market-research-job`). PR #654 is unchanged (still
targets `dev`); the ticket was not moved and the PR was not merged, per the
stop condition.

### Unresolved / for the reviewer

None from this round's own scope. Two process notes: (1) the worktree already
carried substantial uncommitted work addressing R1–R6 when this round began —
consistent with a prior session's run that reached the fix but did not
finish committing/pushing before being cut off; it was reviewed line-by-line
against each finding rather than redone. (2) `origin/dev` moved a second time
mid-round (PLAT-068); the coordinator flagged it and the migration was
regenerated again on the new tail. No other lane reported moving `dev` after
that.

## CI fix round (2026-09-04)

CI run 33818731230, lane `sql-integration (2)`, failed
`MarketResearchCompletionCompensatesAFailedContentWriteByRemovingTheOrphan`
with `Assert.Throws() Failure: No exception was thrown` — the R5
compensation test had never actually run before CI (the integration suite
is CI-only here).

**Root cause.** `ThrowingCommandInterceptor` overrode
`NonQueryExecuting`/`NonQueryExecutingAsync`, but EF Core 10's SQL Server
provider executes a batched `SaveChangesAsync` via `ExecuteReaderAsync`
(it reads back an affected-row count/`OUTPUT` per statement for
concurrency checking), not `ExecuteNonQueryAsync` — even for plain
inserts with no store-generated values. Confirmed this empirically with a
temporary diagnostic build against real LocalDB: the whole SaveChanges
call for `CompleteMarketResearchAiJob` is one batched command (containing
the `ActionHistory`, `AiJobs`, `CaseDocuments`, `CaseHistory`,
`CaseValuations`, `CaseWorkflowEvents`, `CaseWorkflows`,
`DocumentVersions`, `DocumentOccurrences` statements together), executed
through `ReaderExecutingAsync`. `NonQueryExecuting(Async)` was simply
never called, so the interceptor never fired and no exception was thrown.

**Fix.** Reused the existing `ReaderCommandCounter` convention already in
this file (it hooks `ReaderExecutingAsync` for the same reason). Replaced
the dead `NonQueryExecuting`/`NonQueryExecutingAsync` overrides with a
single `ReaderExecutingAsync` override, gated on
`eventData.CommandSource == CommandSource.SaveChanges` in addition to the
existing command-text match. The `CommandSource` gate was needed because
the interceptor stays registered on the shared `PooledDbContextFactory`
for the rest of the test — without it, the test's own post-failure LINQ
verification queries against `CaseValuations` (e.g.
`context.CaseValuations.CountAsync(...)`) also match the command-text
predicate and get incorrectly failed too (`CommandSource.LinqQuery`,
found and fixed via a second real LocalDB run).

EF Core also wraps a SaveChanges-time provider/interceptor failure in
`DbUpdateException`, with the injected `InvalidOperationException` as its
`InnerException` — this is standard EF behaviour, not something the
production code does. Updated the assertion accordingly: it now expects
`DbUpdateException` and asserts the wrapped `InnerException` is the exact
injected `InvalidOperationException` (by type and message), which keeps
the test pinned to the specific injected failure rather than loosening it
to "any exception." The underlying claim under test — a failed content
write leaves no orphan — is unchanged and still fully asserted
(`InterceptedCount >= 1`, `content.StoreCount == 1`,
`content.DeleteCount == 1`, zero `CaseValuations`/`DocumentOccurrence`
rows, unchanged `AiJobs` state/version).

Also merged `origin/dev` (had advanced by two commits, both scoped to
`.github/workflows/ci.yml` for DELIV-043 — no conflicts, no new
migrations, so `MarketResearchAiJob` still sorts last in
`IntakePersistenceIntegrationTests.cs`'s applied-migrations list; no
regeneration needed).

All other tests added in the R5 review round (the expired-lease
compensation test and the rest of `AssessmentPersistenceIntegrationTests`
filtered by `MarketResearchCompletion`) were run and confirmed passing —
10/10.

**Verification (real LocalDB, not CI):**
- `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — exit 0
- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` — exit 0 (1219/1219 passed)
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MarketResearchCompletion"` — exit 0 (10/10 passed), including the previously-failing compensation test, run for real against SQL LocalDB
- `./scripts/Test-MigrationGrants.ps1` — exit 0 (91 migration files checked, every created table granted or exempted)

New head SHA: `55d59cd80` (merge commit; fix commit `a55d727e2` on top of
`582796b04`, merged with `origin/dev` at `f479a9484`). Pushed to
`task/auto-018-market-research-job`. PR #654 not merged; ticket stage
unchanged.
