# PLAT-048 research — 2026-08-28

Wave 3 of [[EPIC-011]]; consumed by [[PLAT-049]] (Operations Service health table) and [[PLAT-051]] (Admin Service health + Reports). Read models only; no schema change.

## Governing text

- `docs/frd/frd-12-operator-experience.md` § Operations: Service health table = area, service, state, latest evidence, dependency, retry. § Administration: Reports = Engineer Report (`MI-01`), per Engineer and period, queries received and reports.
- `docs/capabilities.md` MI-01 row: a query is a retained message classified post-report and associated with the Engineer's Cases (operator decision D12 in EPIC-011 context).
- `docs/frd/frd-11-...` § AI Job List: states; `Failed` is terminal and not re-queued automatically (a person decides).
- Ticket verification line: every health row names its evidence time; no probe is invented.

## Read-only checks run (all in the worktree at 658a7984)

| Premise | Check | Result |
| --- | --- | --- |
| Mailbox poll status port exists | `Core/Identity/ApprovedMailboxAdministration.cs:83` | `IApprovedMailboxPollStatusQueries.ListAsync` → `ApprovedMailboxPollStatus(ApprovedMailboxId, MailboxAddress, DueAtUtc, LastCompletedAtUtc?, LastFailureCode?)`; EF impl `EfApprovedMailboxPollStatusQueries` reads `ApprovedInboxPollStates`. Verified. |
| Staleness rule already exists | `Core/Intake/RetainedMail.cs:659` | `GetRetainedMailFreshness.StaleAfter = 15 min` — reused, not re-declared. Verified. |
| Sent-evidence poll status is exposed by a Core port | grep `ApprovedSentPollState` in Core | **No port.** `ApprovedSentPollStateEntity` (MailboxAddress, DueAtUtc, LastCompletedAtUtc, LastFailureCode) is only read by the poll store. `ISentEvidencePollOutcomeQueries` is outcome-only and has a Core-test fake (`GetTriageDisplayNameTests.NoPollOutcomes`), so extending it would touch a file outside Owns. A new read is needed. |
| Intake dispatch counts are exposed | `Core/Intake/DurableIntake.cs:116` | `IQueuedIntakeStatusQueries.GetAsync(stagedReceiptId)` is per-receipt only; `IIntakeReceiptQueries.GetCountsAsync` counts Needs sorting / Blocked intake (decisions, not dispatch). State codes owned by `EfIntakeWorkStore.ToCode/ParseState` (`pending, dispatching, dispatched, processing, retry_scheduled, completed, failed`). A new aggregate read is needed. |
| External-work failures with retry identity | `Core/Operations/RequestOperations.cs` | `GetRequestOperations` (PerformCasework) returns `RequestOperationProjection` with `Kind == ExternalWork`, `ExternalKind` = the `ExternalWorkKinds` code, `AttemptCount`, `FailureCode`, `CanRetry`, `LastActivityAtUtc`; `RetryExternalWorkCommand(WorkItemId, ExpectedAttemptCount, …)` is the retry identity. Verified; reused as-is. |
| EVA submissions | `Core/Eva/EvaApiContracts.cs:221`, `Persistence/EfEvaSubmissionQueries.cs` | `IEvaSubmissionQueries.GetLatestAsync(caseId)` only; no fakes in tests (grep). `EvaSubmissions` rows carry `Outcome` text, `IsDelivered`, `SubmittedAtUtc`, `FailureCode`. Pending automatic work lives in `ExternalWorkItems` with `Kind == submit_case_to_eva` and state `pending/dispatching/queued/processing` (`EfEvaSubmissionWorkStore`). Additions go on `IEvaSubmissionQueries` (owned file). |
| AI jobs counts and kill switch | `Core/AiWork/AiJobs.cs:191`, `AiWorkContracts.cs:138` | `IAiJobQueries.GetCountsAsync` → `AiJobCounts(Active, Failed)`; `ListRecentAsync(1)` gives the newest job (`CreatedAtUtc`, `ClosedAtUtc`) for the evidence time; `ISendToAiControl.IsEnabledAsync` is the Send-to-AI switch (`EfSendToAiControlStore`). Verified; reused. |
| Automation ingress kill switch is reachable from Core | `Web/Mcp/AutomationClientRegistry.cs` | Web-only (OpenIddict); `GetStatusAsync` requires `ManageAutomationClients`, `IsEnabledAsync(clientId)` is unguarded. No Core port exists (grep `IAutomationClient` in Core: none). A Core port + Web adapter is needed. Latest automation evidence time: `IAutomationActivityQueries.ListAsync(page 1, size 1)` newest record `OccurredAtUtc` (port is unguarded; the use case guards). |
| Reports sent per Engineer | `Persistence/EfDashboardQueries.cs`, `CaseWorkflowEntities.cs` | Case-linked Sent evidence = `CaseReportSentEvidence` where `CaseId != null`, time = `SentAtUtc`; Engineer = `CaseWorkflows.AssignedEngineerId` (staff id). Verified. |
| Queries received per Engineer (D12) | `EfRetainedMailboxMessageStore.cs:640-700`, `MailboxEntities.cs:156`, `CurrentIntakeAssociations.cs` | Retained message ↔ `IntakeReceipts` by `SourceChannel == "mailbox"` and `ExternalReceiptToken`; family persisted on `IntakeMailClassificationDecisions.Family` as the category name — `MailTaxonomy.CategoryName(PostReportEmails) == "post-report-emails"`; case association resolved by `CurrentIntakeAssociations.ReadAsync` (manual active wins, then `CaseIntakeLinks`; reversed manual associations excluded). Receipt time = `IntakeReceipts.ReceivedAtUtc`. Verified. |
| Staff names | `Core/Actors/ActorDisplayNames.cs` | `ResolveStaffNamesAsync(IStaffAccountQueries, ids)` → username map; `UnknownStaff` fallback. Reused. |
| Rights | `Core/Identity/StaffAuthorization.cs` | Administrator-only rights are the `ManageX` group; add `ViewOperationalReports` there. |
| DI home for Core use cases | `Infrastructure/DependencyInjection.cs:241-245` | `GetRequestOperations`, `IGetOperationsSnapshot` are registered by Infrastructure; Web's `AutomationMcpExtensions.cs:29` registers `AutomationClientRegistry`. No `ValidateOnBuild` in either composition root (grep). |
| Integration test harness | `tests/Pegasus.IntegrationTests/EvaSubmissionPersistenceTests.cs` | `[Trait("Category","SqlServer")]`, `LocalDbTestDatabase.CreateAsync()`, `SeedCaseAsync` pattern (Organization, PrincipalSequenceLineage, Principal, IntakeReceipt, Case, CaseWorkflow). Copied, not shared (the helper is private per test file today). |

## Assumed (not checked live)

- No production data check is needed: read models only, no deploy.
- Index need: the Engineer query filters `IntakeReceipts` by `ReceivedAtUtc` + `SourceChannel` and joins the decision by PK; `CaseReportSentEvidence` by `SentAtUtc`. Volumes are small (alpha); if `CaseReportSentEvidence.SentAtUtc` proves slow an index would be a follow-up migration — reported, not added here.

## Decisions taken in this research

1. **Direct composition, no `IServiceHealthSources`.** Six existing ports already own their facts; wrapping them would be a second list of the same things. Only the three facts with no owner get one new Core read port (`IServiceHealthQueries`: sent-evidence poll status, intake dispatch counts) plus the two EVA additions on the owning `IEvaSubmissionQueries`, and one boundary port for the Web-only kill switch (`IAutomationIngressStatusQueries`).
2. **State vocabulary is closed** (`Current / Partial / Failed / Running / Configured / ReviewRequired`) and every state is derived from a recorded fact, never a probe.
3. **Engineer display names are resolved in Core** through `ActorDisplayNames` (the one owner), so the EF query returns counts keyed by Engineer id only.
