# Post-implementation report — AUTO-001

## Summary

The Automation MCP composition gate may now be enabled by explicit production
configuration (ADR-0026). The source change removes only the former
DevelopmentOffline-only startup guard in `AutomationMcpOptions.TryCreate`;
OAuth client-credentials validation, per-area scopes, rate limit, permanent
history and the Administrator kill switch are untouched. The bicep declares the
Key Vault secret reference and the four Container App settings so `azd
provision` owns the enabled state. Activation itself ships with release 9
([[DELIV-008]]) from the promoted `main`; live evidence is captured there.

Branch `task/auto-001-activate-mcp-gate` head `db3f57db` (contains `origin/dev`
`6cf9b166`).

## Changes

| File | Change | Why |
| --- | --- | --- |
| `docs/adr/0026-enable-automation-mcp-by-explicit-deployment-configuration.md` (new), `docs/adr/README.md`, `docs/adr/0021-…md` (status superseded) | record the decision | durable boundary change needs an ADR |
| `src/Pegasus.Web/Mcp/AutomationMcp.cs`, `src/Pegasus.Web/Program.cs`, `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | drop the `developmentOfflineProfile` parameter/throw; comments | the guard was the only obstacle to a configured production activation |
| `infra/main.bicep`, `infra/main.parameters.json`, `infra/modules/platform.bicep` | `automationMcpClientSecretUri` param → `automation-mcp-client-secret` Key Vault ref; `Features__AutomationMcp=true`, `AutomationMcp__ClientId`, `AutomationMcp__ClientSecret` (secretRef), `AutomationMcp__PublicOrigin` on the Web app | infrastructure owns the enabled state; the earlier manual edit is superseded |
| `docs/operations.md` | dated record of the two failed activation attempts and the closed rollback | operations owns dated evidence |
| `docs/current-architecture.md` | gate sentence now points at operations for deployed state | snapshot stays stateless |

## Governing docs

- **FRD-10** — unchanged; the Automation Actor boundary, tool inventory and
  real-caller evidence bar are preserved. Live evidence for the deployed
  ingress is owed by this ticket's proof after release 9.
- **ADR-0026** (new, accepted) supersedes ADR-0021's DevelopmentOffline-only
  clause; ADR-0021's direct-write inventory continues through ADR-0026.
- No PRD/capabilities change.

## Verification (merged branch, Release)

- `dotnet build ./Pegasus.slnx -c Release` — 0 warnings, 0 errors.
- `Pegasus.ArchitectureTests` — 96/96.
- Integration `AutomationMcpIngressTests|AutomationDocumentIngressTests|AutomationAssessmentIngressTests` — 15/15 (LocalDB).
- `Test-AzureDeploymentPlan.ps1 -Mode Local` — pass; `Test-DocumentationLinks.ps1` — 222 files, all resolve.

## Risks / follow-ups

- The live app currently carries the MCP settings with the flag `false` from a
  manual `az containerapp update`; release 9's `azd provision` replaces that
  configuration wholesale — expected and desired.
- The azd env must carry `AUTOMATION_MCP_CLIENT_SECRET_URI` (versioned URI of
  the existing Key Vault secret) before provision, or the parameter renders empty.
- Ephemeral OpenIddict keys: tokens do not survive a revision restart (single
  always-on replica) — accepted by ADR-0026; connector re-authenticates.
- Exercising write tools with success in production creates real records;
  live evidence will use read/list tools for success and denial/validation
  paths for write tools unless the operator approves creating a test case.

## Verification hand-off

After release 9 on the deployed estate: `POST /connect/token` (client
credentials `pegasus-automation`, secret from Key Vault, scope
`automation.cases`) → 200 with token; `POST /mcp` `tools/list` → 15 tools;
a `tools/call` on a read tool → success + `ActionHistory` row (SQL readback);
call without token → 401; call outside scope → `automation_scope_denied`
security event; Administrator kill switch → routes closed (`/mcp` 302/404),
re-enable → open. Record in proof; refresh operations.md.
