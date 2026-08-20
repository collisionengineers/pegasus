## Post-implementation report — TICK-023 (MCP-01)

**Retrospective backfill.** Implemented and deployed before this ticket's pipeline documents existed.

### What exists
- OAuth2 authorization-code + PKCE MCP ingress: `src/Pegasus.Web/Mcp/AutomationMcp.cs`, `AutomationTokenEndpoint.cs`, `AutomationClientRegistry.cs`, `AutomationActorResolver.cs`, `AutomationMcpExtensions.cs`, `Pages/Connect/Authorize.cshtml.cs`.
- Composition gate: `Features__AutomationMcp=true` in `infra/modules/platform.bicep:425`, enabled in production since release 9 (ADR-0026, 2026-08-18).

### Tests
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter FullyQualifiedName~AutomationMcpIngressTests --configuration Release` → Passed 6/6 (2026-08-20).
- `--filter FullyQualifiedName~AutomationConnectorAuthorizationTests` → Passed 4/4 (2026-08-20).

### Live evidence
- 2026-08-20 read-only probe against production: `GET/POST https://pegasus-prod-web-252ow37gij.ashymushroom-676209e5.uksouth.azurecontainerapps.io/mcp` → 302; `/connect/token` → 400; `/authorize` → 400. None 404 — the surface is live, not absent.
- `docs/operations.md` release-10 entry records a full live OAuth round trip against `https://claude.ai` the same day: `/authorize` → sign-in → consent → code+PKCE exchange → access/refresh token → `/mcp` `tools/list` returning 15 tools.

### Deployment
- All listed files present at `2325ed4a` (release 13 SHA); `Features__AutomationMcp=true` present in `platform.bicep` at the same SHA.

### Residual (named, not blocking, per `docs/capabilities.md` MCP-01's own text)
No external product session (Claude Desktop/Code) beyond the recorded connector round trip is proven yet; no AI proposal transport is activated. These are accepted activation-boundary notes in the capability inventory, not implementation gaps.
