# Checklist — AUTO-018 (2026-09-02; corrected 2026-09-03 after plan review)

- [x] Step 1 — add the closed `MarketResearch` Core contract (kind, Case
  subject, typed completion command/result, completion use case) in
  `src/Pegasus.Core/AiWork/**`; Automation completion ends at `DraftReady`,
  staff-only `Completed` unchanged, existing kinds' completion rules unchanged.
- [x] Step 1 — Case eligibility reuses `AiJobPolicy.IsEligibleEstimateCaseState`
  (no second eligibility list); every lifecycle state tested, allowed and
  refused.
- [x] Step 2 — add `AiMarketResearch` to `ValuationSource` in
  `src/Pegasus.Core/Assessment/Valuations.cs`; Automation admitted only for
  that source through the completion use case; Engineer's Value ownership
  unchanged. No guide month, note or adjustment field is added (CASE-029 /
  TICK-083).
- [x] Step 3 — implement one replay-safe EF completion transaction (document
  custody + valuation row + ledger transition + histories) reusing
  `EfDocumentCustodyStore`, `EfValuationStore`, `EfAiJobStore` and the Case
  mutation guard; register it in `DependencyInjection.cs`.
- [x] Step 4 — add the named tool `pegasus_ai_job_complete_market_research` in
  `src/Pegasus.Web/Mcp/AiJobMcpTools.cs` (generic `pegasus_ai_job_complete`
  unchanged and still refusing the new kind), reusing
  `AutomationMcpErrors.DecodeContent`/`RequireFileName`/`RequireMediaType`.
- [x] Step 4 — move the single `MaximumDocumentBytes` constant to
  `AutomationMcpErrors` so both MCP tools read one document-size rule; no
  second copy of the number.
- [x] Step 4 — the two-scope workflow is documented in the tool description and
  proved: `automation.jobs` for the job tools, `automation.cases` for
  `pegasus_case_edit_begin`; no jobs-only lease mechanism.
- [x] Shared-lock availability confirmed for `Presentation/OperatorLabels.cs`
  and `Persistence/Migrations/**` before editing either.
- [x] Step 4 — add exactly one label, `Market research`, to
  `OperatorLabels.AiJobs.Kind`; no `ValuationSource` source label (CASE-029).
- [x] Step 4b — add `MarketResearch` to `CanCompleteByHand` in
  `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs` so the existing staff
  confirmation reaches the operator; `ReviewAction` and the markup unchanged;
  no review flag, checkbox, dialog or history event (D44).
- [x] Step 5 — after the migration lock is free, `git merge --no-edit
  origin/dev`, then generate the migration, Designer and model snapshot on the
  merged snapshot (`AiJobs` kind/result checks, `CaseValuations` source check,
  typed result columns); regenerate rather than hand-merge if another lane's
  migration lands first; existing rows retained.
- [x] Step 5 — migration census updated in
  `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`;
  `scripts/Invoke-AzureDatabaseBootstrap.ps1` touched only if the migration
  carries grant SQL, and in the same diff. (No grant-SQL change needed — no
  new table was introduced.)
- [x] Step 6 — Core tests (`AiJobTests.cs` harness, `ValuationTests.cs`
  recording store): catalogue, Case-only subject, eligible/ineligible states,
  actor rules, kill switch, source vocabulary, Engineer's Value isolation.
- [x] Step 6 — integration tests (`AutomationAiJobIngressTests.cs` via
  `AutomationMcpTestSupport`, `AssessmentPersistenceIntegrationTests.cs`):
  claim/complete, one document + one valuation row, operation-key replay,
  changed-payload refusal, stale version/lost lease leaves nothing, missing
  `automation.jobs` and missing/expired lease refused separately, actor
  attribution in Action History, stopped automation refuses new claims.
  (Written and compiled in the Release build; not executed locally — see the
  commands note below.)
- [x] Step 6 — `OperationsWebTests.cs`: the list renders `Market research`, and
  a `Draft ready` MarketResearch job offers the existing `Complete job` action
  and completes to `Completed` without changing any valuation or assessment
  field. (Written and compiled; not executed locally — see the commands note
  below.)
- [x] Lane refreshed with `git merge --no-edit origin/dev` (never a rebase) —
  fast-forwarded to `659cec77`.
- [x] `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0 recorded (run
  independently by Codex and again by the Claude orchestrator).
- [x] `dotnet build ./Pegasus.slnx --configuration Release --no-restore` —
  exit 0 recorded, 0 warnings, 0 errors (run independently by Codex and again
  by the Claude orchestrator).
- [x] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "Category!=Corpus&Category!=Browser"` — **not run locally, by design.** Per
  the orchestrator's execution instructions, only the fast local checks ran
  here: `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj
  --configuration Release --no-build` (1,198 passed, 0 failed) and `dotnet
  test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj
  --configuration Release --no-build` (100 passed, 0 failed) — both run
  independently by Codex and again by the Claude orchestrator, exit 0 both
  times. The full filter (which includes Pegasus.IntegrationTests, ~26
  minutes locally) is left to GitHub CI's sharded run on the PR, which is the
  gate the reviewer blocks on.
- [x] `./scripts/Test-MigrationGrants.ps1` — exit 0 recorded ("88 migration
  files checked, every created table is granted or exempted"; run
  independently by Codex and again by the Claude orchestrator).
- [x] Simplification pass run over the branch diff and recorded under the
  dated heading in the plan with dispositions.
- [x] Post-implementation report written, naming the evidence tier honestly:
  no production creation caller until [[CASE-029]] merges, and the guide-month
  hand-off to CASE-029.
- [ ] PR opened with Kanmer: AUTO-018
