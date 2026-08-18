# Files — AUTO-001

## Change surface

| Path | Planned change / risk |
| --- | --- |
| `src/Pegasus.Web/Mcp/AutomationMcp.cs` | Permit the existing options model to compose in Production only when complete configuration is supplied; preserve default-off behavior. |
| `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | Replace the explicitly local-only transport assumptions with the production HTTPS configuration while retaining the existing OAuth confidential-client, scopes, tools, and kill switch. Security-critical. |
| `src/Pegasus.Web/Program.cs` | Compose the endpoint only with complete configuration and retain the no-route closed state otherwise. |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | Cover production-capable composition, bearer-only behavior, malformed/absent configuration, and closed-state regression. |
| `infra/main.bicep`, `infra/modules/platform.bicep`, `infra/main.parameters.json` | Pass the versioned Key Vault secret URI, configure the Container App secret reference, and set non-secret feature/client/public-origin values. |
| `scripts/Test-AzureDeploymentPlan.ps1`, `scripts/Invoke-ProductionSmoke.ps1` | Extend the current release checks to validate configuration presence without retrieving a secret and capture the live smoke/rollback. |
| `docs/current-architecture.md`, `docs/operations.md`, `docs/runbook.md` | After approved deployment, record observed activation, Claude Desktop evidence, rollback, and current live revision facts. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | Real caller, authorization/validation failure, and permanent-history evidence required for the existing inventory. |
| `docs/adr/0021-automation-actor-direct-write-assessment-contract.md` | Existing one-client, fifteen-tool composition boundary; no new tool or business-policy change. |
| `src/Pegasus.Web/Mcp/AutomationClientRegistry.cs` | Reuse the client registration and Administrator kill switch; do not create a second client/policy owner. |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | Reuse the HTTP OAuth/MCP test harness and expected inventory. |
| `infra/modules/platform.bicep` | Existing Web Key Vault-reference and Container App environment conventions. |
| `docs/runbook.md#live-operation-approval-matrix` | Exact approval required before any Key Vault or Azure write. |

## Ripple effects

This is one production Web release and an externally configured Claude Desktop remote connector. It does not add Core policy, a database migration, a new MCP tool, OAuth user login, or a Pegasus-side tool allow-list. Claude Desktop controls connector/tool access; Pegasus validates bearer requests, serves the existing inventory, records history, and retains the kill switch.

## Out of scope

- Secret values in source control, ticket documents, logs, or command output.
- Unapproved Azure, Key Vault, Container App, credential, or external-client mutations.
- Changing tool inventory or confirmation/approval/dispatch boundaries.
- [[TICK-027]]’s local assessment-caller evidence.
