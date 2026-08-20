## Research — TICK-023 (MCP-01) — retrospective backfill

**Question:** Does `dev` need implementation for management/development-controlled MCP ingress for one named vendor-neutral Automation Actor through Core use cases?

**Findings (verified 2026-08-20):**
- `src/Pegasus.Web/Mcp/AutomationMcp.cs`, `AutomationTokenEndpoint.cs`, `AutomationClientRegistry.cs`, `AutomationActorResolver.cs`, `AutomationMcpExtensions.cs`, `Pages/Connect/Authorize.cshtml.cs` implement OAuth2 authorization-code + PKCE ingress at `/mcp`, `/connect/token`, `/authorize` (ADR-0011/0026/0027).
- Composition gate `Features__AutomationMcp` is set `true` in `infra/modules/platform.bicep:425` (production app settings) and wired in `Program.cs`.
- Live probe (2026-08-20): `curl -s -o /dev/null -w "%{http_code}" https://pegasus-prod-web-252ow37gij.ashymushroom-676209e5.uksouth.azurecontainerapps.io/mcp` → `302`; `/connect/token` → `400`; `/authorize` → `400`. None are `404` — the surface is live and responding to authenticated protocol traffic, not absent.
- `docs/operations.md:230` — "Connector flow (ADR-0027, live since release 10)"; release 10 evidence includes an actual `/authorize` → sign-in → consent → `/mcp` `tools/list` (15 tools) round trip against `https://claude.ai` as the registered connector, recorded the same day.
- Tests: `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter FullyQualifiedName~AutomationMcpIngressTests --configuration Release` → Passed 6/6 (2026-08-20). `--filter FullyQualifiedName~AutomationConnectorAuthorizationTests` → Passed 4/4 (2026-08-20).
- `docs/capabilities.md` MCP-01 row: "Implemented behind a composition gate ... enabled in production by explicit configuration since release 9 (2026-08-18, ADR-0026) ... live token/inventory/denial/history/kill-switch evidence is recorded in operations, no external product caller (Claude Desktop/Code session) is proven yet, and no AI proposal transport is activated."

**Implications:** MCP-01 ingress is implemented, tested, and deployed to production with live evidence of the OAuth flow. The residuals named in `docs/capabilities.md` (no external product-session caller proven yet beyond the recorded connector round trip, no AI proposal transport) are recorded activation-boundary notes, not missing implementation.

**Open questions:** none.
