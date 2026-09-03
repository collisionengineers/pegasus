# Checklist — AUTO-018 (2026-09-02)

- [ ] Operator answer to the open question applied as the one Core policy line
  (`DraftReady` default or `Completed` for `MarketResearch` only) before any
  ledger-transition code is written.
- [ ] Step 1 — add the closed `MarketResearch` Core contract (kind, Case
  subject, typed completion command/result, completion use case) in
  `src/Pegasus.Core/AiWork/**`; existing kinds' completion rules unchanged.
- [ ] Step 2 — add `AiMarketResearch` and the nullable guide-month field to
  `src/Pegasus.Core/Assessment/Valuations.cs`; Automation admitted only for
  that source through the completion use case; Engineer's Value ownership
  unchanged.
- [ ] Step 3 — implement one replay-safe EF completion transaction (document
  custody + valuation row + ledger transition + histories) reusing
  `EfDocumentCustodyStore`, `EfValuationStore`, `EfAiJobStore` and the Case
  mutation guard; register it in `DependencyInjection.cs`.
- [ ] Step 4 — expose the typed `MarketResearch` completion on the
  `automation.jobs` tool surface in `src/Pegasus.Web/Mcp/AiJobMcpTools.cs`,
  routing only that kind to the new Core port.
- [ ] Shared-lock availability confirmed for `Presentation/OperatorLabels.cs`
  and `Persistence/Migrations/**` before editing either.
- [ ] Step 4 — add exactly two labels (`Market research` kind, `AI market
  research` source) in `src/Pegasus.Web/Presentation/OperatorLabels.cs`.
- [ ] Step 5 — add the serialized migration, Designer and model snapshot
  (`AiJobs` kind/result checks, `CaseValuations` source check + guide month,
  typed result columns); existing rows retained.
- [ ] Step 5 — migration census updated in
  `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`;
  `scripts/Invoke-AzureDatabaseBootstrap.ps1` touched only if the migration
  carries grant SQL.
- [ ] Step 6 — Core tests (`AiJobTests.cs` harness, `ValuationTests.cs`
  recording store): catalogue, Case-only subject, actor rules, kill switch,
  source vocabulary, guide month, Engineer's Value isolation.
- [ ] Step 6 — integration tests (`AutomationAiJobIngressTests.cs` via
  `AutomationMcpTestSupport`, `AssessmentPersistenceIntegrationTests.cs`):
  claim/complete, one document + one valuation row, operation-key replay,
  changed-payload refusal, stale version/lost lease leaves nothing, scope and
  actor attribution in Action History, stopped automation refuses new claims.
- [ ] Step 6 — `OperationsWebTests.cs` renders `Market research` for the new
  kind (only if the existing fake list must be extended); Operations page
  itself unchanged.
- [ ] Lane refreshed with `git merge --no-edit origin/dev` (never a rebase).
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0 recorded.
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore` —
  exit 0 recorded.
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "Category!=Corpus&Category!=Browser"` — exit 0 recorded.
- [ ] `./scripts/Test-MigrationGrants.ps1` — exit 0 recorded.
- [ ] Simplification pass run over the branch diff and recorded under the
  dated heading in the plan with dispositions.
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: AUTO-018
