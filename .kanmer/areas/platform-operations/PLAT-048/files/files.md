# PLAT-048 files

Diff estimate: ~1,100 lines added, ~5 changed, across 12 files (4 new Core/Infra/Web, 4 new tests, 4 edited).

## New

| Path | Purpose |
| --- | --- |
| `src/Pegasus.Core/Operations/ServiceHealth.cs` | Row/state/dependency vocabulary, `ServiceHealthPolicy` (pure mapping), `IServiceHealthQueries`, `IAutomationIngressStatusQueries`, `GetServiceHealth` use case |
| `src/Pegasus.Core/Reports/EngineerActivityReport.cs` | `IEngineerActivityQueries`, rows, `EngineerActivityReportCsv.ToCsv`, `GetEngineerActivityReport` use case |
| `src/Pegasus.Infrastructure/Persistence/EfServiceHealthQueries.cs` | `IServiceHealthQueries` over `ApprovedSentPollStates` and `IntakeWorkItems` |
| `src/Pegasus.Infrastructure/Persistence/EfEngineerActivityQueries.cs` | `IEngineerActivityQueries` over `CaseReportSentEvidence`, `IntakeReceipts` + classification decision, `CurrentIntakeAssociations`, `CaseWorkflows` |
| `src/Pegasus.Web/Mcp/AutomationIngressStatusQueries.cs` | Web adapter for `IAutomationIngressStatusQueries` over `AutomationClientRegistry.IsEnabledAsync` (outside Owns — the kill switch lives in Web) |
| `tests/Pegasus.Core.Tests/Operations/ServiceHealthTests.cs` | State mapping + composition rules |
| `tests/Pegasus.Core.Tests/Reports/EngineerActivityReportTests.cs` | Rights, period validation, name resolution, CSV shape |
| `tests/Pegasus.IntegrationTests/ServiceHealthPersistenceTests.cs` | SqlServer: sent-poll status, dispatch counts, EVA failures/pending |
| `tests/Pegasus.IntegrationTests/EngineerActivityReportPersistenceTests.cs` | SqlServer: reports/queries per Engineer, reversed association excluded, period bounds |

## Edited

| Path | Change |
| --- | --- |
| `src/Pegasus.Core/Eva/EvaApiContracts.cs` | `IEvaSubmissionQueries.GetRecentFailuresAsync`, `GetActivityAsync`; `EvaSubmissionFailure`, `EvaSubmissionActivity` records |
| `src/Pegasus.Infrastructure/Persistence/EfEvaSubmissionQueries.cs` | Implement the two additions |
| `src/Pegasus.Core/Identity/StaffAuthorization.cs` | `ViewOperationalReports` (Administrator) |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register `IServiceHealthQueries`, `GetServiceHealth`, `IEngineerActivityQueries`, `GetEngineerActivityReport` |
| `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | Register the Web adapter (outside Owns; one line) |

Not touched: `EfEvaSubmissionWorkStore.cs` (pending counts read the same `ExternalWorkItems` rows from `EfEvaSubmissionQueries`, so the work store keeps its single write-side job), UI, migrations, `OperatorLabels.cs`.
