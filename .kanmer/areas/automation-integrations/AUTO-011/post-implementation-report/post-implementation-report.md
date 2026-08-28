# Post-implementation report — AUTO-011

Branch `task/auto-011-ai-job-ledger` (from `origin/dev` 690ca579; merged
`origin/dev` again before the PR — already up to date). Solution builds
Release with 0 warnings; `pwsh ./scripts/Test-MigrationGrants.ps1` passes
(78 files). Tests were **not run** by the implementer — the orchestrator
runs the wave loop.

## What shipped

**Core (`src/Pegasus.Core/AiWork/AiJobs.cs`, `AiJobOperations.cs`)**

- `AiJobKind {Estimate, UnidentifiedResolution, QueryResponse, UnidentifiedQueuePass}`,
  `AiJobState {Queued, Taken, DraftReady, Completed, Failed, Cancelled, Expired}`,
  `AiJobSubjectKind {Case, Unidentified, Queue}`, `AiJobResultKind {Estimate, ProposedResolution, DraftReply}`,
  `AiJobStates.IsTerminal`, `AiJobRecord`, `AiJobResult`, `AiJobCounts`.
- `AiJobPolicy`: `IsLegalTransition`, `EffectiveState` (lapsed lease → Queued;
  untaken past expiry → Expired), `SubjectKindFor`, `ResultKindFor`,
  `ValidateNew` / `ValidateTransition` / `ValidateResult`, `LeaseDuration`
  30 min, `DefaultExpiry` 24 h, eligibility predicates.
- Commands `CreateAiJobCommand`, `NewAiJob`, `AiJobTransition`, `Take/Release/ReportProgress/Complete/Fail/Cancel/ConfirmAiJobCommand`.
- Ports `IAiJobStore` (Create/Get/Transition), `IAiJobQueries`
  (`ListOpenAsync`, `ListForSubjectAsync`, `ListRecentAsync(max)`,
  `GetCountsAsync` → Active/Failed).
- Use cases `CreateAiJob` (kill switch via `ISendToAiControl`; staff
  `PerformCasework`; Automation only for the queue pass — D5; Estimate needs
  a With Engineer case and a confirmed `assessment.values.engineer`, target
  1–100; QueryResponse needs PostReport/PostReportComplete; UnidentifiedResolution
  needs an open item by id or U-reference), `WorkAiJob` (take/release/progress/
  complete→DraftReady/fail; kill switch on take and progress), `CancelAiJob`,
  `ConfirmAiJob` (DraftReady→Completed, staff).

**Infrastructure**

- `AiJobEntity` (in `AssessmentEntities.cs`), mapping in
  `AssessmentModelConfiguration.cs` (check constraints on State/Kind/
  SubjectKind/ResultKind/target %; indexes `(State, LeaseExpiresAtUtc)`,
  `SubjectId`, `CreatedAtUtc`, unique `OperationKey`), `AiJobs` DbSet.
- `EfAiJobStore : IAiJobStore, IAiJobQueries` — Serializable transactions,
  SHA-256 request hash replay, version check, per-client hold check,
  expired-lease bookkeeping, ActionHistory aggregate `ai_job` with events
  `ai_job_created/taken/progress/released/draft_ready/completed/failed/cancelled/expired`
  (`AfterJson` carries `CaseId` when the subject is a Case).
- Migrations: `20260828084601_AiJobs` (table) and `20260828084644_GrantAiJobs`
  (`SELECT, INSERT, UPDATE` to `pegasus_web_runtime_role`; Worker gets none — D5).
- DI: `EfAiJobStore` → `IAiJobStore` + `IAiJobQueries`; `ICreateAiJob`,
  `IWorkAiJob`, `ICancelAiJob`, `IConfirmAiJob`.

**MCP (`src/Pegasus.Web/Mcp`)**

- `AutomationMcp.JobsScope = "automation.jobs"` in `Scopes` (registered
  scope, resource metadata, endpoint policy).
- `AiJobMcpTools`: `pegasus_ai_job_list` (queued + this client's held jobs,
  optional kind filter), `pegasus_ai_job_create` (UnidentifiedQueuePass only),
  `pegasus_ai_job_take`, `pegasus_ai_job_progress`, `pegasus_ai_job_complete`
  (→ DraftReady with result kind/reference/text), `pegasus_ai_job_fail`,
  `pegasus_ai_job_release`; all under `automation.jobs`; registered in
  `AutomationMcpExtensions`.
- Consent descriptions (`Authorize.cshtml.cs`) for `automation.jobs` and
  `automation.mail`; `automation.assessment` text no longer says "generate
  EVA bundles".

**Tests**

- `tests/Pegasus.Core.Tests/AiWork/AiJobTests.cs` — transition graph,
  effective state, D5 actor rule, estimate preconditions, actor rules per
  transition, kill switch on create/take/progress (fail still allowed),
  30-minute lease, Estimate/QueryResponse/Unidentified preconditions.
- `tests/Pegasus.IntegrationTests/AutomationAiJobIngressTests.cs`
  (SqlServer) — store replay/version/other-client/lease expiry/counts/
  history; scope refusal; create→list→take→progress→complete round trip
  over HTTP with attribution rows; Administrator switch refuses take and
  progress but not fail.
- `AutomationMcpIngressTests.ExpectedTools` gained the seven tools.

## What wave 4 consumes

- Operations AI Job List: `IAiJobQueries.ListOpenAsync` + `ListRecentAsync`
  (filter terminal jobs to the current day in the page), `ICreateAiJob`
  for "Send Unidentified to AI" (`AiJobKind.UnidentifiedResolution`,
  `SubjectReference` = U-reference), `ICancelAiJob`, `IConfirmAiJob` for
  "Complete job".
- Administration Automation & AI: `IAiJobQueries.GetCountsAsync`.
- Assessment "Send to Claude": `ICreateAiJob` with `AiJobKind.Estimate`,
  `SubjectId` = case id, `TargetPercentOfEngineerValue`.
- ENG-026 estimate tools: cite `AiJobRecord.JobId` and check `TakenBy`
  against the calling client.

## Open questions

- `Completed` for Estimate jobs is expected to be recorded when an Engineer
  accepts the draft (`Use estimate`); that caller is ENG-026/ENG-028 and
  should call `IConfirmAiJob`.
- Query-response jobs carry the message reference in `Instruction`
  (SubjectKind is the Case). If wave 4 wants a typed message subject, that is
  a small additive change to `AiJobSubjectKind`.

## Review round — 2026-08-28

Fixes applied on the branch (BOMs stripped; lapsed lease always reads
Queued; repeat take refused; consent text corrected). Recorded decisions:

- `EngineerValueAtSend` is returned under `automation.jobs` because the
  Estimate kind needs its basis figure to draft to the target percentage.
- Registered but not yet called by production code in this PR — **not
  claimed as delivered**: `ICancelAiJob` and `IConfirmAiJob` (Operations
  "Cancel" / "Complete job", PLAT-049; Engineer "Use estimate", ENG-028),
  `IAiJobQueries.ListForSubjectAsync` / `ListRecentAsync` (PLAT-049
  Operations list), `GetCountsAsync` (AUTO-010 Automation & AI counts).
  Wave 4 supplies the callers and their activation evidence.

## Wave-loop round — 2026-08-28 (cb788581 → 4b8f69c4)

- `IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema`
  census extended with `20260828084601_AiJobs` and `20260828084644_GrantAiJobs`.
- `pegasus_ai_job_complete` / `pegasus_ai_job_release`: `resultReference`,
  `resultText` and `reason` were nullable but had no default, so the MCP
  schema marked them required and a call omitting one was refused before
  the Core result-kind rule ran. They now default to `null` (the operation
  key precedes them). The FRD-11 result shape (kind + reference and/or
  text, PascalCase kind names) is unchanged; the assertion was not relaxed.

## CI round — 2026-08-28 (bootstrap census)

`scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` requires every
grant-carrying migration to be accounted for in the bootstrap's expected
permission matrix. `scripts/Invoke-AzureDatabaseBootstrap.ps1` now carries
the `20260828084644_GrantAiJobs` block: `pegasus_web_runtime_role` G
SELECT/INSERT/UPDATE on `AiJobs`; DELETE denied via the baseline matrix;
no Worker grant (AGENTS.md rule 16 — schema, grants and bootstrap census on
one diff). Local run: "Azure deployment plan validation passed (Local)".
This file is an addition to the owned list; it is the census the grant
migration requires, not new scope.
