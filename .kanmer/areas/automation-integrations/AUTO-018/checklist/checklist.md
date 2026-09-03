# Checklist — AUTO-018 (2026-09-02; corrected 2026-09-03 after plan review)

- [ ] Step 1 — add the closed `MarketResearch` Core contract (kind, Case
  subject, typed completion command/result, completion use case) in
  `src/Pegasus.Core/AiWork/**`; Automation completion ends at `DraftReady`,
  staff-only `Completed` unchanged, existing kinds' completion rules unchanged.
- [ ] Step 1 — Case eligibility reuses `AiJobPolicy.IsEligibleEstimateCaseState`
  (no second eligibility list); every lifecycle state tested, allowed and
  refused.
- [ ] Step 2 — add `AiMarketResearch` to `ValuationSource` in
  `src/Pegasus.Core/Assessment/Valuations.cs`; Automation admitted only for
  that source through the completion use case; Engineer's Value ownership
  unchanged. No guide month, note or adjustment field is added (CASE-029 /
  TICK-083).
- [ ] Step 3 — implement one replay-safe EF completion transaction (document
  custody + valuation row + ledger transition + histories) reusing
  `EfDocumentCustodyStore`, `EfValuationStore`, `EfAiJobStore` and the Case
  mutation guard; register it in `DependencyInjection.cs`.
- [ ] Step 4 — add the named tool `pegasus_ai_job_complete_market_research` in
  `src/Pegasus.Web/Mcp/AiJobMcpTools.cs` (generic `pegasus_ai_job_complete`
  unchanged and still refusing the new kind), reusing
  `AutomationMcpErrors.DecodeContent`/`RequireFileName`/`RequireMediaType`.
- [ ] Step 4 — move the single `MaximumDocumentBytes` constant to
  `AutomationMcpErrors` so both MCP tools read one document-size rule; no
  second copy of the number.
- [ ] Step 4 — the two-scope workflow is documented in the tool description and
  proved: `automation.jobs` for the job tools, `automation.cases` for
  `pegasus_case_edit_begin`; no jobs-only lease mechanism.
- [ ] Shared-lock availability confirmed for `Presentation/OperatorLabels.cs`
  and `Persistence/Migrations/**` before editing either.
- [ ] Step 4 — add exactly one label, `Market research`, to
  `OperatorLabels.AiJobs.Kind`; no `ValuationSource` source label (CASE-029).
- [ ] Step 4b — add `MarketResearch` to `CanCompleteByHand` in
  `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs` so the existing staff
  confirmation reaches the operator; `ReviewAction` and the markup unchanged;
  no review flag, checkbox, dialog or history event (D44).
- [ ] Step 5 — after the migration lock is free, `git merge --no-edit
  origin/dev`, then generate the migration, Designer and model snapshot on the
  merged snapshot (`AiJobs` kind/result checks, `CaseValuations` source check,
  typed result columns); regenerate rather than hand-merge if another lane's
  migration lands first; existing rows retained.
- [ ] Step 5 — migration census updated in
  `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`;
  `scripts/Invoke-AzureDatabaseBootstrap.ps1` touched only if the migration
  carries grant SQL, and in the same diff.
- [ ] Step 6 — Core tests (`AiJobTests.cs` harness, `ValuationTests.cs`
  recording store): catalogue, Case-only subject, eligible/ineligible states,
  actor rules, kill switch, source vocabulary, Engineer's Value isolation.
- [ ] Step 6 — integration tests (`AutomationAiJobIngressTests.cs` via
  `AutomationMcpTestSupport`, `AssessmentPersistenceIntegrationTests.cs`):
  claim/complete, one document + one valuation row, operation-key replay,
  changed-payload refusal, stale version/lost lease leaves nothing, missing
  `automation.jobs` and missing/expired lease refused separately, actor
  attribution in Action History, stopped automation refuses new claims.
- [ ] Step 6 — `OperationsWebTests.cs`: the list renders `Market research`, and
  a `Draft ready` MarketResearch job offers the existing `Complete job` action
  and completes to `Completed` without changing any valuation or assessment
  field.
- [ ] Lane refreshed with `git merge --no-edit origin/dev` (never a rebase).
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0 recorded.
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore` —
  exit 0 recorded.
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "Category!=Corpus&Category!=Browser"` — exit 0 recorded.
- [ ] `./scripts/Test-MigrationGrants.ps1` — exit 0 recorded.
- [ ] Simplification pass run over the branch diff and recorded under the
  dated heading in the plan with dispositions.
- [ ] Post-implementation report written, naming the evidence tier honestly:
  no production creation caller until [[CASE-029]] merges, and the guide-month
  hand-off to CASE-029.
- [ ] PR opened with Kanmer: AUTO-018
