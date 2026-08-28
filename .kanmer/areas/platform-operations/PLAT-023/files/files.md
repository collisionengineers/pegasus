# Files — PLAT-023

Lane H owns `src/Pegasus.Web/Pages/Operations/**` and the
OperationsWebTests-family files. Shared label/tone maps gain entries only
(no wave-2 lane owns them; CASE-025 set the precedent on OperatorLabels).

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Operations/Index.cshtml` | Rewrite onto the design system: `page-header`, `notice` status, `stack` of `panel` sections (Service health when composed, Attention required, Active upload links), design empty states, partial-data warning notice; remove the AI placeholder section. |
| `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs` | Optional `GetServiceHealth?` dependency + `ServiceHealth` property; delete the dead local `StateLabel` map (moves to OperatorLabels). Handlers unchanged. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Add `RequestOperationState`, `ServiceHealthArea`, `ServiceHealthState`, `ServiceHealthDependency`, `ServiceHealthService` label maps (one label map, no second copy). |
| `src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml` | Add tone keys `active`, `running`, `review required`. |
| `tests/Pegasus.IntegrationTests/OperationsWebTests.cs` | Drop the AI-placeholder assertions (section removed); add a composed-Service-health test (fake `GetServiceHealth` graph) proving the table and its Retry control render and post to the existing RetryExternal handler. |

Untouched: Core (`ServiceHealth.cs`, `RequestOperations.cs`), Program.cs,
`AutomationMcpExtensions.cs`, DI, migrations, `OperatorJourneyTests.cs`
(pinned surfaces preserved), every other lane's page.
