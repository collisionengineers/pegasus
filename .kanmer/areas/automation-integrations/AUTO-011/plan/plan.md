# Plan — AUTO-011

Diff estimate: ~1,900 lines added (Core ~450, Infrastructure ~500 incl.
migration, Web ~350, tests ~600), ~15 lines changed in existing files.

## 1. Core (`AiJobs.cs`, `AiJobOperations.cs`)

- Enums `AiJobKind`, `AiJobState`, `AiJobSubjectKind {Case, Unidentified, Queue}`, `AiJobResultKind {Estimate, ProposedResolution, DraftReply}`;
  `AiJobStates.IsTerminal`; `AiJobPolicy.IsLegalTransition` (Queued→Taken/Cancelled/Expired; Taken→Queued(release/expiry)/DraftReady/Failed/Cancelled; DraftReady→Completed/Cancelled).
- `AiJobRecord` per the brief; `AiJobPolicy.EffectiveState(record, now)` reads a Taken job past its lease as Queued (reuse: single owner of the lease rule, called by store list/get mapping and the commands).
- Commands: `CreateAiJobCommand`, `TakeAiJobCommand`, `ReleaseAiJobCommand`, `ReportAiJobProgressCommand`, `CompleteAiJobCommand` (DraftReady with result), `FailAiJobCommand`, `CancelAiJobCommand`, `ConfirmAiJobCommand` (DraftReady→Completed staff). Ports `IAiJobStore` (`CreateAsync`, `GetAsync`, `TransitionAsync(AiJobTransition)`), `IAiJobQueries` (`ListOpenAsync`, `ListForSubjectAsync`, `ListRecentAsync(max)`, `GetCountsAsync`).
- Use cases: `CreateAiJob` (kill switch via `ISendToAiControl`, actor rule D5, kind preconditions through `ICaseAssessmentStore`, `ICaseWorkflowQueries`, `IUnidentifiedStore`), `WorkAiJob` (take/release/progress/complete/fail; automation-only; kill switch on take/progress; lease `AiJobPolicy.LeaseDuration = 30 min`), `CancelAiJob` (staff, reason), `ConfirmAiJob` (staff). Validation mirrors `AiWorkPolicy` (instruction ≤ 500, reasons ≤ 500, note ≤ 500, result reference ≤ 200 / text ≤ 4000).

## 2. Infrastructure

- `AiJobEntity` beside `AiWorkRequestEntity`; mapping in `AssessmentModelConfiguration` with State/Kind/SubjectKind check constraints (same `SqlLiteral` helper), indexes `(State, LeaseExpiresAtUtc)`, `SubjectId`, unique `OperationKey`, `(CreatedAtUtc)`.
- `EfAiJobStore : IAiJobStore, IAiJobQueries` — copy of the `EfAiWorkRequestStore` mechanics (Serializable tx, request hash replay, version check, `AddHistory` with aggregate `ai_job`, `CorrelationId = JobId`, `AfterJson` carrying `CaseId` when the subject is a Case). Transition idempotency: `(JobId, OperationKey)` replay returns the record; a transition to the state already held returns the record.
- Migration `AiJobs` via `dotnet ef migrations add`; grant migration `GrantAiJobs` (Web role `SELECT, INSERT, UPDATE`; Worker none — D5). `pwsh ./scripts/Test-MigrationGrants.ps1`.
- DI: `IAiJobStore`, `IAiJobQueries` → `EfAiJobStore`; `ICreateAiJob`, `IWorkAiJob`, `ICancelAiJob`, `IConfirmAiJob` beside the AI-09 lines.

## 3. MCP

- `AutomationMcp.JobsScope = "automation.jobs"` in `Scopes`.
- `AiJobMcpTools` (list/create/take/progress/complete/fail/release), each `resolver.RequireAsync(JobsScope)`; `create` refuses any kind but `UnidentifiedQueuePass` (D5); mutations `RequireOperationKey`; list = queued + this client's taken (FRD-10). `RecordAsync` for create/take/complete/fail/release, `RecordDenialAsync` for list/progress (mechanics).
- Register in `AutomationMcpExtensions`; consent descriptions for jobs and mail; assessment text corrected.

## 4. Tests

- Core: transition graph, lease expiry reads as Queued, D5 actor rule, kind preconditions, kill switch on create/take/progress, cancel needs reason.
- Integration (SqlServer): store create/replay/version conflict/history; tools list/take/complete happy path with history rows, scope refusal, registry kill switch refusal, Send-to-AI switch refusal on take.

## Sequencing

Commits: Core → Infrastructure+migration+grant → MCP → tests; merge `origin/dev`; simplification pass; report; PR.
