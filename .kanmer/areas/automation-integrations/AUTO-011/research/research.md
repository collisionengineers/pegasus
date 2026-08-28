# Research — AUTO-011 AI job ledger and automation.jobs tools

Worktree `../pegasus-worktrees/auto-011-ai-job-ledger`, branch
`task/auto-011-ai-job-ledger`, from `origin/dev` at 690ca579.

## Governing text (read)

- `docs/adr/0035-ai-job-ledger.md` — pull ledger, one `automation.jobs`
  scope, closed Core kind catalogue, two creation callers (staff Web,
  scheduler through the Actor `create` tool), results are pointers/drafts,
  AI-09 patterns reused (operation key, expected version, kill switch,
  Automation attribution). Why `ExternalWorkItems` and `AiWorkRequests` do
  not fit is recorded there.
- `docs/frd/frd-11-...md` § AI Job List (lines 206-266): kinds table
  (Estimate needs an Engineer's Value and target %; Unidentified resolution
  needs a U reference; Query response a post-report query on a Case;
  Unidentified-queue pass created by an external scheduler), states
  `Queued → Taken → Draft ready → Completed` + `Failed/Cancelled/Expired`,
  lease semantics, "Every transition carries an operation key and an
  expected version", client transitions attributed to Automation + client
  name, kill switch refuses claims and progress.
- `docs/frd/frd-10-...md` § AI job and estimate tools (lines 57-95): the
  seven `pegasus_ai_job_*` tools, all under `automation.jobs`; list shows
  every queued job and the client's own taken jobs; `automation.mail` must
  carry a consent description. `pegasus_estimate_*` are NOT this ticket
  (ENG-026).
- EPIC-011 `context.md` D5/D6, §1.9 (Send to Claude: direction + target %
  of Engineer's Value, disabled without Engineer's Value), §1.11/§1.12
  (Operations list and Admin active/failed counts read the ledger).

## Verified premises (files read)

| Premise | Evidence |
| --- | --- |
| AI-09 store pattern: Serializable transaction, (CaseId, OperationKey) unique idempotency with SHA-256 request hash, `CaseOperationConflictException` on differing replay, `Version` concurrency token, ActionHistory row per change with `CorrelationId = RequestId` | `src/Pegasus.Infrastructure/Persistence/EfAiWorkRequestStore.cs` (whole file) |
| `AiWorkRequestEntity` lives in `AssessmentEntities.cs` (line 86) and is configured in `AssessmentModelConfiguration.cs` lines 138-172 (check constraint on State names via `SqlLiteral`) | read |
| `ISendToAiControl.IsEnabledAsync` is registered always (`DependencyInjection.cs:311`) and absent row means enabled | `EfSendToAiControlStore` |
| `AiWorkPolicy` shape (static validate + `IsLegalTransition` switch, `RequireStaffSender`) | `src/Pegasus.Core/AiWork/AiWorkOperations.cs` |
| `StaffAccessRight.PerformCasework` is granted to Staff and Automation | `src/Pegasus.Core/Identity/StaffAuthorization.cs:35-36` |
| `ActionActor.Automation(clientId)`; `ActorKind {Staff, SystemWorker, RequestLink, Automation}` | `IdentityContracts.cs:22-79` |
| `AutomationMcp.Scopes` feeds `server.RegisterScopes`, `ScopesSupported`, and the endpoint policy (`AutomationMcpExtensions.cs:48,101,113`) — adding a constant to `Scopes` registers it everywhere | read |
| `AutomationActorResolver.RequireAsync(scope)` does principal → registry kill switch → scope check, each denial a SecurityEvent | `AutomationActorResolver.cs` |
| Tool shape: `[McpServerToolType]` class, `[McpServerTool(Name=..)]`, `resolver.RequireAsync` then `auditor.RecordAsync(context, tool, aggregateId, key, () => AutomationMcpErrors.ExecuteAsync(...))`; `RequireOperationKey` enforces `mcp:` prefix | `UnidentifiedMcpTools.cs`, `AutomationMcpErrors.cs` |
| `AutomationMcpErrors.ExecuteAsync` maps `ArgumentException/InvalidOperationException/InvalidDataException` messages through, `StaffAuthorizationException` to a fixed text | read |
| Consent `ScopeDescriptions` at `Pages/Connect/Authorize.cshtml.cs:26-33`; `automation.mail` is missing; assessment text says "generate EVA bundles" | read |
| Migration + grant precedent: `Persistence/Migrations/20260827143132_EvaApiSubmissions.cs` + `20260827143200_GrantEvaSubmissions.cs` (`IsSqlServer()`, `RequireRuntimeRoles`, GRANT/REVOKE) ; `AiWorkRequests` grant is `SELECT, INSERT, UPDATE` to the Web role (`20260803205759_SendToAiAssessmentToolset.cs:191`) | read |
| `scripts/Test-MigrationGrants.ps1` scans `Persistence/Migrations` for `CreateTable(name: "X")` in Up() and requires a `GRANT ... [X]` in any migration | read |
| `.config/dotnet-tools.json` pins `dotnet-ef` 10.0.10 | read |
| `CaseAssessmentProjection.Field(AssessmentVocabulary.ValueEngineer)` returns `AssessmentFieldValue` with `IsConfirmed` (ConfirmedBy not null); `ICaseAssessmentStore.GetAsync(caseId)` | `AssessmentContracts.cs:48,88-97,123-137,278-280` |
| `ICaseWorkflowQueries.GetAsync(caseId)` → `CaseWorkflowRecord.State`, `Identity.Reference`, `Version`; `CaseLifecycleState.PostReport/PostReportComplete` exist | `CaseWorkflowContracts.cs:11-23,100-111,314-319` |
| `IUnidentifiedStore.GetAsync(id)` / `GetByReferenceAsync(reference)` → `UnidentifiedItem.State (Open/Resolved)`, `Reference` | `Intake/Unidentified/*.cs:21-25,173-187,260-280` |
| MCP integration harness: `AutomationMcpTestSupport` (token per scope string, `PostMcpAsync`, `ReadStructuredContentAsync`, `SeedAcceptedCaseAsync`), `factory.Database.ScalarAsync<int>(sql)` for history assertions, `AutomationClientRegistry.SetEnabledAsync` for the registry kill switch, `ExpectedTools` inventory in `AutomationMcpIngressTests.cs` | read |
| Core test style: plain xUnit facts/theories with in-memory fakes (`tests/Pegasus.Core.Tests/AiWork/AiWorkTests.cs`) | read |

## Assumptions

- `dotnet ef migrations add` with `--project src/Pegasus.Infrastructure
  --startup-project src/Pegasus.Web` produces the migration (runbook only
  says migrations are additive); verified at implementation time by running
  it.
- The Worker never touches `AiJobs` (Pegasus runs no timer, D5), so only
  the Web runtime role is granted; the grant migration says so.
- Expired leases are computed on read (a `Taken` job whose
  `LeaseExpiresAtUtc` has passed reads and lists as `Queued`) rather than by
  a sweeper — ADR-0035 "taken jobs expire back to Queued when their lease
  ends" with no timer available. The expired claim is recorded when the
  next take overwrites it (`ai_job_expired` history row written by that
  take), so nothing is erased.
- `Completed` is a staff act (FRD-11) — this ticket ships the Core command
  (`CompleteAiJob` for the staff path is reached by wave 4); the Actor's
  `pegasus_ai_job_complete` maps to `DraftReady` per FRD-10.
