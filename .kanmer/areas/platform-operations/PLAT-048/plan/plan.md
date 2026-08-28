# PLAT-048 plan

Diff estimate ~1,100 lines; this plan is shorter than the diff.

## Shape

### Service health (`Core/Operations/ServiceHealth.cs`)

- `ServiceHealthArea { Mail, Intake, Custody, Eva, Ai, Automation }`, `ServiceHealthState { Current, Partial, Failed, Running, Configured, ReviewRequired }`, `ServiceHealthDependency { MicrosoftGraph, Worker, Box, EvaApi, AiConnector, AutomationClient }` — closed lists, one owner; labels are PLAT-049's `OperatorLabels`.
- `ServiceHealthRetryTarget(WorkItemId, ExpectedAttemptCount)` = the `RetryExternalWorkCommand` identity, nothing else.
- `ServiceHealthRow(Area, Service, State, LatestEvidenceAtUtc?, Dependency, RetryTarget?)`; `ServiceHealthSnapshot(AsOfUtc, Rows)`.
- New ports (facts with no existing owner, see research): `IServiceHealthQueries.ListSentEvidencePollStatusAsync` → `SentEvidencePollStatus(MailboxAddress, DueAtUtc, LastCompletedAtUtc?, LastFailureCode?)`; `IServiceHealthQueries.GetIntakeDispatchHealthAsync` → `IntakeDispatchHealth(Active, RetryScheduled, Failed, LatestCompletedAtUtc?)`; `IAutomationIngressStatusQueries.IsEnabledAsync`.
- `ServiceHealthPolicy` (static, pure): `PollState(lastCompleted, failureCode, now)` — failure → Failed; never completed → Configured; older than `GetRetainedMailFreshness.StaleAfter` → Partial; else Current. `DispatchState(health)` — Failed>0 → Failed; RetryScheduled>0 → Partial; Active>0 → Running; else Current. `EvaState(activity, failures)` — recent failures → ReviewRequired; pending → Running; never submitted → Configured; else Current. `AiState(enabled, counts)` — disabled → Configured; Failed>0 → ReviewRequired; Active>0 → Running; else Current. `ExternalWorkDependency(kind)` — custody kinds → Box, `submit_case_to_eva` → EvaApi, else Worker.
- `GetServiceHealth` (PerformCasework) composes: one Mail row per `IApprovedMailboxPollStatusQueries` mailbox; one Mail "Sent evidence" row per sent-poll state; one Intake dispatch row; Custody/external work — one row per retryable failed `ExternalWork` item from `GetRequestOperations` (retry target set), or a single Current/Configured row when none failed (evidence = newest `LastActivityAtUtc`); one Eva row (`GetRecentFailuresAsync(now-24h)`, `GetActivityAsync`); one Ai row (`IAiJobQueries.GetCountsAsync` + `ListRecentAsync(1)` + `ISendToAiControl`); one Automation row (`IAutomationIngressStatusQueries` + newest `IAutomationActivityQueries` record). Nothing else — no row for a service that has no composed source.

**Why no `IServiceHealthSources`:** six sources already have a port; a facade would be a second copy of six signatures with no second caller.

### Engineer report (`Core/Reports/EngineerActivityReport.cs`)

- `IEngineerActivityQueries.GetAsync(fromUtc, toUtc, engineerId?)` → `EngineerActivityCounts(EngineerId, ReportsSent, QueriesReceived)`; half-open `[from, to)`.
- `GetEngineerActivityReport` requires `ViewOperationalReports`; rejects `from >= to` and spans over 366 days; resolves names with `ActorDisplayNames.ResolveStaffNamesAsync`; rows ordered by name then id; returns `EngineerActivityReport(FromUtc, ToUtc, Rows)` of `EngineerActivityRow(EngineerId, DisplayName, ReportsSent, QueriesReceived)`.
- `EngineerActivityReportCsv.ToCsv(rows)` — header `Engineer,Queries received,Reports`, RFC-4180 quoting, CRLF; no file I/O.
- `StaffAccessRight.ViewOperationalReports` joins the Administrator group in `StaffAuthorization`.

### Infrastructure

- `EfServiceHealthQueries`: two aggregate queries; dispatch counts group by `IntakeWorkItems.State` using `EfIntakeWorkStore.ParseState` after the read.
- `EfEvaSubmissionQueries`: `GetRecentFailuresAsync(sinceUtc, max)` = non-delivered rows newest first; `GetActivityAsync` = pending `submit_case_to_eva` work count + newest `SubmittedAtUtc`.
- `EfEngineerActivityQueries`: reports = `CaseReportSentEvidence` (CaseId not null, SentAtUtc in period) joined to `CaseWorkflows.AssignedEngineerId`; queries = mailbox `IntakeReceipts` in period whose decision `Family == MailTaxonomy.CategoryName(PostReportEmails)` → `CurrentIntakeAssociations.ReadAsync` → workflow Engineer. Optional Engineer filter applied after the join.
- DI: Infrastructure registers the three Infra adapters and both use cases; Web registers `AutomationIngressStatusQueries` next to `AutomationClientRegistry`.

## Steps

1. Core: `StaffAuthorization` right; `EvaApiContracts` additions; `ServiceHealth.cs`; `EngineerActivityReport.cs`. Build.
2. Infrastructure: three adapters + DI. Web adapter + registration. Build.
3. Core tests (fakes local to the test file, documented estate fixtures: `instructions@collisionengineers.co.uk`, references like `EVA31003`). Integration tests (SqlServer).
4. Merge `origin/dev`, simplification pass, post-implementation report, PR.

## Acceptance

- `dotnet build ./Pegasus.slnx --configuration Release` exit 0.
- Every row carries `LatestEvidenceAtUtc` from a recorded timestamp or null; no state is produced by calling an external system.
- No file outside Owns except the two Web lines and `DependencyInjection.cs`, all reported.
