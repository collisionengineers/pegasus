## Files — TICK-023 (MCP-01) — retrospective backfill

| Path | Why |
|---|---|
| `src/Pegasus.Web/Mcp/AutomationMcp.cs` | MCP endpoint composition (`/mcp`). |
| `src/Pegasus.Web/Mcp/AutomationTokenEndpoint.cs` | `/connect/token` OAuth2 token exchange. |
| `src/Pegasus.Web/Mcp/AutomationClientRegistry.cs` | Registered Automation Actor client. |
| `src/Pegasus.Web/Mcp/AutomationActorResolver.cs` | Resolves the Automation Actor identity from the token. |
| `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | DI/composition wiring. |
| `src/Pegasus.Web/Pages/Connect/Authorize.cshtml.cs` | `/authorize` consent screen. |
| `infra/modules/platform.bicep:425` | `Features__AutomationMcp=true` production app setting. |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs`, `AutomationConnectorAuthorizationTests.cs` | Ingress/auth regression coverage. |

No source change proposed; reconciliation only.
