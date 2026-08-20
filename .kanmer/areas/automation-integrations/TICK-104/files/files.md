# Files — MCP-07

| Path | Change |
| --- | --- |
| `src/Pegasus.Core/AiWork/AiWorkContracts.cs` | Connector settings record, validation rules (one owner: loopback origin, token length, timeout bounds), and the store port beside `ISendToAiControl` |
| `src/Pegasus.Web/AiWork/SendToAi.cs` | `SendToAiOptions.TryCreate` delegates its bounds to the Core rules (no second validation list) |
| `src/Pegasus.Web/AiWork/ChannelAiHandOffTransport.cs` | Resolve effective base URL/token/timeout per call: administration values override composition options |
| `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs` | New nullable columns on the `SendToAiControlEntity` singleton |
| `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs` | Column mapping |
| `src/Pegasus.Infrastructure/Persistence/EfAiWorkRequestStore.cs` | Connector settings store on the same singleton row: DataProtection-protected token, attributed history |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register the port |
| `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | `Microsoft.AspNetCore.DataProtection.Abstractions` |
| `src/Pegasus.Infrastructure/Persistence/Migrations/` (new) | Add-columns migration; no new table, no new grants (`SendToAiControl` grants exist) |
| `src/Pegasus.Web/Pages/Administration/Automation/Index.cshtml(.cs)` | Connector section: status display, base URL/timeout form, token entry/rotation form |
| `tests/Pegasus.Core.Tests/AiWork/AiWorkTests.cs` | Validation rules tests |
| `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs` | Administration override reaches the transport; token round-trip protected; history attribution |

## Out of scope

No outbound call to the configured URL (no validation ping — no caller exists and none is invented). No change to the `Features:SendToAi` composition gate, the DevelopmentOffline restriction, or the loopback rule (ADR-0021 decisions). No new page family, store, or deployment unit. Token value is never displayed, logged, or echoed after entry.
