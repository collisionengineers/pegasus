# Files — AUTO-011

## Owned (created or modified)

- `src/Pegasus.Core/AiWork/AiJobs.cs` — new: kinds, states, record, commands, ports.
- `src/Pegasus.Core/AiWork/AiJobOperations.cs` — new: `AiJobPolicy` + the use cases.
- `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs` — add `AiJobEntity`.
- `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs` — `AiJobs` mapping.
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` — `AiJobs` DbSet.
- `src/Pegasus.Infrastructure/Persistence/EfAiJobStore.cs` — new: store + queries.
- `src/Pegasus.Infrastructure/Persistence/Migrations/<ts>_AiJobs.cs` (+Designer, snapshot) — new table.
- `src/Pegasus.Infrastructure/Persistence/Migrations/<ts>_GrantAiJobs.cs` — Web role grant.
- `src/Pegasus.Infrastructure/DependencyInjection.cs` — registrations beside AI-09.
- `src/Pegasus.Web/Mcp/AiJobMcpTools.cs` — new tool class.
- `src/Pegasus.Web/Mcp/AutomationMcp.cs` — `JobsScope`.
- `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` — `.WithTools<AiJobMcpTools>()`.
- `src/Pegasus.Web/Pages/Connect/Authorize.cshtml.cs` — consent descriptions.
- `tests/Pegasus.Core.Tests/AiWork/AiJobTests.cs` — new.
- `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` — `ExpectedTools` inventory.
- `tests/Pegasus.IntegrationTests/AutomationAiJobIngressTests.cs` — new: store + tools.

## Consumed, not modified

- `src/Pegasus.Core/AiWork/AiWorkContracts.cs` (`ISendToAiControl`), `AiWorkOperations.cs` (pattern).
- `src/Pegasus.Core/Identity/StaffAuthorization.cs`, `IdentityContracts.cs`.
- `src/Pegasus.Core/Assessment/AssessmentContracts.cs` (`ICaseAssessmentStore`, `AssessmentVocabulary.ValueEngineer`).
- `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` (`ICaseWorkflowQueries`).
- `src/Pegasus.Core/Intake/Unidentified/*` (`IUnidentifiedStore`).
- `src/Pegasus.Web/Mcp/AutomationActorResolver.cs`, `AutomationMcpErrors.cs`.
- `tests/Pegasus.IntegrationTests/AutomationMcpTestSupport.cs` (harness; `AllScopes` left as-is — jobs tests request `automation.jobs` explicitly).
- `scripts/Test-MigrationGrants.ps1`.

## Out of scope

`Pages/Cases/Assessment/**`, `Pages/Operations/**`, `Pages/Administration/**`, `site.css/js`, estimate tools (ENG-026), any UI.
