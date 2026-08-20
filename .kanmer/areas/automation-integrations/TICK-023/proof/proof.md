## Proof — TICK-023 (MCP-01)

Retrospective proof, verified 2026-08-20.

- Ingress files: `src/Pegasus.Web/Mcp/AutomationMcp.cs`, `AutomationTokenEndpoint.cs`, `AutomationClientRegistry.cs`, `AutomationActorResolver.cs`, `AutomationMcpExtensions.cs`, `src/Pegasus.Web/Pages/Connect/Authorize.cshtml.cs` — all present at production SHA `2325ed4a`.
- Composition gate live: `infra/modules/platform.bicep:425` `Features__AutomationMcp=true` present at `2325ed4a`.
- Tests: `AutomationMcpIngressTests` 6/6, `AutomationConnectorAuthorizationTests` 4/4 (2026-08-20).
- Live read-only probe (2026-08-20): `/mcp` → 302, `/connect/token` → 400, `/authorize` → 400 (none 404 — endpoint live).
- `docs/operations.md` records a full live OAuth round trip against `https://claude.ai` at release 10 (2026-08-18).

**Residual (named, not blocking):** no external product session beyond the recorded connector round trip is proven yet; no AI proposal transport activated (both are accepted activation-boundary notes per `docs/capabilities.md` MCP-01).
