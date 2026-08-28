# PLAT-048 post-implementation report — 2026-08-28

Branch `task/plat-048-service-health-report` from `origin/dev` 658a7984; `origin/dev` merged before the PR (already up to date). `dotnet build ./Pegasus.slnx --configuration Release`: succeeded, 0 warnings. Tests were written, not run (wave loop rule); the orchestrator runs `Category!=Corpus` and the SqlServer set.

## What PLAT-049 / PLAT-051 consume

- `GetServiceHealth.ExecuteAsync(actor)` (PerformCasework) → `ServiceHealthSnapshot(AsOfUtc, Rows)`; `ServiceHealthRow(Area, Service, State, LatestEvidenceAtUtc?, Dependency, RetryTarget?)`. `Area`/`State`/`Dependency` are closed enums — labels belong in `OperatorLabels.cs` (PLAT-049). `Service` is the mailbox address for poll rows (`"Sent evidence · <address>"` for the Sent-items cursor), the `ExternalWorkKinds` code for a failed work row, or a `ServiceHealthPolicy.*Service` constant. `RetryTarget` is non-null only on a Custody row and is exactly `RetryExternalWorkCommand(WorkItemId, ExpectedAttemptCount, …)` — the Retry column posts that command; "View" for everything else.
- `GetEngineerActivityReport.ExecuteAsync(actor, fromUtc, toUtc, engineerId?)` (new right `ViewOperationalReports`, Administrator) → rows `(EngineerId, DisplayName, ReportsSent, QueriesReceived)` ordered by name; `EngineerActivityReportCsv.ToCsv(rows)` for Export (the page owns the response and file name). Period is half-open `[from, to)` and at most 366 days; the page converts office-local dates to UTC (see `GetOperationsSnapshot.OfficeBoundaries` for the office zone convention).
- Registered in `Infrastructure/DependencyInjection.cs`; `IAutomationIngressStatusQueries` is registered by Web's `AddAutomationMcp` — `GetServiceHealth` therefore resolves in Web only, which is where both consumers live.

## State mapping (recorded facts only)

| Row | Current | Partial | Failed | Running | Configured | ReviewRequired |
| --- | --- | --- | --- | --- | --- | --- |
| Mailbox / Sent-items poll | completed within 15 min | completed > 15 min ago | `LastFailureCode` set | — | never completed | — |
| Intake dispatch | nothing queued | any retry-scheduled | any failed | any active | — | — |
| External work | no failure, no pending | — | per retryable failure | any pending | no items | — |
| EVA submissions | attempts, none recent failed | — | — | pending work | never submitted | failure in last 24 h |
| AI jobs | jobs, none failed/active | — | — | active jobs | switch off, or no job yet | any failed job |
| Automation ingress | enabled | — | — | — | disabled | — |

## Deviations from the brief

- Pending EVA counts are read by `EfEvaSubmissionQueries` (same `ExternalWorkItems` rows, same state words) rather than added to `EfEvaSubmissionWorkStore`, which stays write-side only.
- No `IServiceHealthSources` facade; direct composition over the owning ports (research decision 1).
- Two files outside Owns: `src/Pegasus.Web/Mcp/AutomationIngressStatusQueries.cs` (new adapter) and one line in `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs`, because the kill switch is Web-only.

## Open questions

1. `ServiceHealthPolicy.EvaRecentFailureWindow` (24 h) and the reuse of `StaleAfter` (15 min) for both poll rows are engineering choices with no operator statement; PLAT-049 review may want them in `docs/open-decisions.md` alongside the existing staleness entry.
2. The Engineer query counts by `IntakeReceipts.ReceivedAtUtc` and `CaseReportSentEvidence.SentAtUtc`; neither column is indexed. Volumes are alpha-scale; if the Reports page proves slow, an index migration is a follow-up ticket (none added here).
3. An Engineer with no activity in the period is absent from the rows. If the Reports table should list every Engineer account with zeros, PLAT-051 can union `IStaffAccountQueries` Engineer-role accounts in the page — the use case does not, so the report stays a query over recorded activity.
