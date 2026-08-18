# Files — AUTO-001

## Change surface

| Path | Planned change / risk |
| --- | --- |
| `src/Pegasus.Web/Mcp/AutomationMcp.cs` | Split safe shared option validation from production-only requirements; preserve disabled-by-default behavior. |
| `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | Replace DevelopmentOffline-only ephemeral crypto and relaxed transport handling with the approved production token signing/encryption and HTTPS policy. Security-critical. |
| `src/Pegasus.Web/Program.cs` | Compose the approved production configuration without exposing routes when the flag or prerequisites are absent. |
| `src/Pegasus.Web/appsettings*.json` / configuration validation tests | Keep no secret or live activation value in tracked files; add fail-closed coverage for malformed/absent production configuration. |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | Extend composition and bearer transport evidence for the production-capable policy while retaining the existing local evidence harness. |
| `infra/main.bicep`, `infra/modules/platform.bicep`, `infra/main.parameters.json` | Add versioned Key Vault secret URI plumbing, Container App secret reference, and non-secret Automation MCP environment values. Security/release-critical. |
| `scripts/Test-AzureDeploymentPlan.ps1`, `scripts/Invoke-ProductionSmoke.ps1` and release scripts (if their existing input census needs it) | Validate the exact feature settings without reading a secret, and add the approved live smoke/rollback evidence. |
| `docs/current-architecture.md`, `docs/operations.md`, `docs/runbook.md` | After approved deployment, record the actual activation state, target, evidence and rollback. `operations.md` also needs its stale live-version observation corrected. |
| `docs/adr/` and `docs/adr/README.md` | A new ADR is likely required for durable production token-key custody and transport policy; do not silently turn a local ephemeral-key decision into production behavior. |

## Context files

| Path | Why it must be read |
| --- | --- |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | Defines the required real-caller, denial, validation, and permanent-history evidence. |
| `docs/adr/0021-automation-actor-direct-write-assessment-contract.md` | Establishes the tool inventory and explicitly reserves production transport/activation work. |
| `src/Pegasus.Web/Mcp/AutomationClientRegistry.cs` | Owns one-client registration and the Administrator kill switch; it must not be bypassed. |
| `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | Contains the current local-only crypto/transport assumptions. |
| `tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs` | Existing real HTTP token, scope, denial, kill-switch, and tool-caller evidence. |
| `infra/modules/platform.bicep` | The exact Web Container App and Key Vault-reference pattern to extend. |
| `docs/runbook.md#live-operation-approval-matrix` | Requires exact-target approval before every cloud or credential write and deployment. |

## Ripple effects

The delivery requires a new signed production Web release and changes ingress exposure. It must retain the Administrator kill switch, update current-state documents after deployment, and record both successful and closed-state rollback evidence. No Core policy, database migration, new MCP tool, or Send to AI transport change is in scope.

## Deliberately out of scope

- Secret generation, retrieval, display, or storage in the repository.
- Any unauthorised Azure, Key Vault, credential, Container App, database, or external-client write.
- Tool inventory expansion, confirmation/approval/dispatch tools, or changes to the Automation Actor's Core authority.
- The linked MCP-06 local caller-evidence work in [[TICK-027]].
