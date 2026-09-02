# Files — AUTO-018 (2026-09-02, gpt-5.6-terra high, wrapper-checked)

## Planned change set

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Core/AiWork/AiJobs.cs` | change | Add `MarketResearch`, its typed completion/result contract, document pointer and market figures. | `AiJobRecord`, `AiJobResult`, commands |
| `src/Pegasus.Core/AiWork/AiJobOperations.cs` | change | Define Case subject, creation validation, kill-switch behaviour, and the per-kind completion rule. | `CreateAiJob`, `WorkAiJob`, `AiJobPolicy` |
| `src/Pegasus.Core/Assessment/Valuations.cs` | change | Add AI market research source, guide month, and narrowly allow Automation only for that source. | `ValuationDetails`, `ValuationPolicy` |
| `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs` | change | Persist market-research completion fields and valuation guide month. | `AiJobEntity`, `CaseValuationEntity` |
| `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs` | change | Extend enum checks, field mapping, bounds, and precision. | Existing enum-derived checks |
| `src/Pegasus.Infrastructure/Persistence/EfAiJobStore.cs` | change | Persist and replay the typed market result and its ledger history. | Existing serializable transition store |
| `src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs` | change | Map guide month and retain the new source without touching Engineer's Value. | Existing valuation transaction/history |
| `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs` | change if the Core completion composes custody here | Preserve one custody transaction/rollback boundary for completion evidence. | `IAddCaseDocument` |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | change if a dedicated completion use case/store is introduced | Compose the one new Core operation. | Existing scoped registrations |
| `src/Pegasus.Web/Mcp/AiJobMcpTools.cs` | change | Accept the MarketResearch completion payload and invoke the one Core operation. | Actor resolver and MCP auditor |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change — shared lock | Add only "Market research" and "AI market research" labels. | `OperatorLabels.AiJobs.Kind` |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_MarketResearchAiJob.cs` | create — shared lock | Alter `AiJobs` and `CaseValuations` schema/check constraints; preserve Web grants. | AUTO-011 and ENG-027 migration conventions |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_MarketResearchAiJob.Designer.cs` | create — shared lock | EF migration metadata. | Generated EF convention |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | change — shared lock | Match the evolved model. | EF snapshot convention |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1` | change if the migration carries grant SQL | Keep release permission-matrix verification exhaustive. | Existing AiJobs/CaseValuations blocks |
| `tests/Pegasus.Core.Tests/AiWork/AiJobTests.cs` | change | Cover catalogue, result shape, state, kill switch, and actor restrictions. | Existing `Harness` |
| `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs` | change | Cover source, guide month, and non-Engineer-value behaviour. | Existing recording store |
| `tests/Pegasus.IntegrationTests/AutomationAiJobIngressTests.cs` | change | Prove claim/complete, document/row outcome, replay, scope, validation, and ActionHistory. | `AutomationMcpTestSupport` |
| `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` | change | Prove SQL persistence, lease/version handling, valuation history, and DI. | Existing valuation harness |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | change | Add the new migration to the exact applied-migration census. | Existing migration list |
| `tests/Pegasus.IntegrationTests/OperationsWebTests.cs` | change if Operations action/state changes | Prove the existing list safely renders the new kind. | `NoAiJobs` and page tests |

## Files this ticket must not touch

- `src/Pegasus.Web/Pages/Cases/Shared/_CaseValuation.cshtml` — CASE-029 owns
  the new Valuation partial and the button caller.
- `src/Pegasus.Web/Pages/Cases/Details.cshtml` and
  `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` — CASE-029 owns the Case
  workspace section integration.
- `src/Pegasus.Web/wwwroot/css/site.css`,
  `src/Pegasus.Web/wwwroot/js/site.js`, and `docs/design/test-ui/**` —
  CASE-029 owns the Valuation UI and snapshot work.
- TICK-083-owned valuation adjustments, rationale, revaluation-history types,
  persistence, and UI — AUTO-018 may add only source evidence needed for the
  AI row.
- Existing ENG-027 valuation semantics beyond the minimal new source and guide
  month — do not alter Engineer's Value ownership, edit behaviour, or existing
  source meanings.
- `Presentation/OperatorLabels.cs` and `Persistence/Migrations/**` must not be
  edited until their shared-lock capacity is explicitly available; AUTO-018
  needs both once serialized.
