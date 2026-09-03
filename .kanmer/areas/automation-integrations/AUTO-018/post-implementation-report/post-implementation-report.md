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
