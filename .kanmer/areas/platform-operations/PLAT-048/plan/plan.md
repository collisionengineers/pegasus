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

## Simplification pass — 2026-08-28

Lenses: reuse, simplification, efficiency, altitude, over the branch diff against `origin/dev` (658a7984).

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | Reuse: `GetRetainedMailFreshness.StaleAfter` is the only staleness number; `ServiceHealthPolicy.PollState` reads it rather than declaring a second one. | Applied (by construction). |
| 2 | Reuse: Engineer names resolve through `ActorDisplayNames.ResolveStaffNamesAsync`; the EF query returns ids only, so there is one owner of "who is this staff id". | Applied. |
| 3 | Reuse: the Custody rows reuse `GetRequestOperations` (validation included) instead of a second projection over `ExternalWorkItems`; the retry identity is exactly `RetryExternalWorkCommand`'s. | Applied. |
| 4 | Reuse: the query-received association rule is `CurrentIntakeAssociations.ReadAsync`, the same rule the Inbox applies, so a reversed association is excluded for free. | Applied. |
| 5 | Simplification: EVA pending work is counted in `EfEvaSubmissionQueries` from the same `ExternalWorkItems` rows `EfEvaSubmissionWorkStore` claims, so the work store keeps a single write-side responsibility and gains no read method. Deviates from the ticket's "implement in … EfEvaSubmissionWorkStore.cs". | Applied; recorded here and in the report. |
| 6 | Altitude: `IAutomationIngressStatusQueries` exposes one boolean, not `AutomationClientStatus` (client id, scopes, display name) — the health row needs only the switch. | Applied. |
| 7 | Efficiency: all counts are grouped/aggregated at the store; only the dispatch state vocabulary is parsed after the read because `EfIntakeWorkStore` owns the codes and `Enum` parsing has no SQL translation (same pattern as `EfEvaSubmissionQueries.GetLatestAsync`). | Applied. |
| 8 | Efficiency: the intake dispatch health runs two aggregate queries (group-by and max). One query with a conditional aggregate would save a round-trip on a table that is small by design (staged receipts are cleaned up). | Not applied — the two-query form reads as the two facts it is; revisit only if the snapshot proves slow. |
| 9 | Simplification: the Core test's single `Sources` fake implements all eight read ports, so each test names its whole estate in one object rather than eight one-line fakes. | Applied. |
| 10 | Considered a `Running` state for a poll cursor under an active lease. Neither poll status port exposes the lease, and adding it would widen `ApprovedMailboxPollStatus` for one consumer. | Not applied — no invented probe; noted as a candidate for PLAT-049 if the table wants it. |

## Review dispositions — 2026-08-28 (PR #591)

| # | Finding | Disposition |
| --- | --- | --- |
| 1a | Intake dispatch row reported `Current` with no evidence (empty queue, nothing ever completed). | Fixed: `Configured` when `Active == 0` and `LatestCompletedAtUtc` is null; theory row added. |
| 1b | A `Failed` poll row's evidence time is the last *success* (`LastCompletedAtUtc`), not the failure time — the cursor records no failure timestamp. | Accepted as documented behaviour: the row names the newest recorded evidence; the failure code is on the cursor without a time. PLAT-049 labels the column "Latest evidence", not "Failed at". |
| 2a | Reports/queries are attributed to the case's *current* `AssignedEngineerId`, not the Engineer at the time of the send/receipt. | Accepted: the workflow keeps no assignment history to attribute against; a reassigned case moves its history with it. Recorded in the report for PLAT-051. |
| 3a | The snapshot reads `IAutomationActivityQueries` directly, bypassing the `ManageAutomationClients`-gated `ListAutomationActivity`. | Accepted with a code comment: only the newest `OccurredAtUtc` leaves the use case, to a PerformCasework reader; no record content is exposed. |
| 4a | `GetServiceHealth` was registered in Infrastructure, so the Worker carried a registration it could not resolve. | Fixed: registration moved to Web `AddAutomationMcp`, beside the ingress adapter it depends on. |
| 5 | `GetRequestOperations.LimitReached` was discarded. | Fixed: `ServiceHealthSnapshot.ExternalWorkLimitReached` carries it for PLAT-049's partial-data notice. |
| 6 | 15-min `StaleAfter` reuse and 24 h `EvaRecentFailureWindow` are engineering choices. | Recorded; to be named in PLAT-049's plan or `docs/open-decisions.md`. |
| 7 | Three copies of the external-work state words in Infrastructure. | Deferred to a follow-up ticket (fix, EPIC-011). |
| 8 | `OfficeBoundaries` is private; the Reports page needs the same office-day conversion. | Deferred to a follow-up ticket (fix, EPIC-011) blocking PLAT-051's conversion. |
