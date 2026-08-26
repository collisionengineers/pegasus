# Files

| File | Change | Risk |
| --- | --- | --- |
| `scripts/Invoke-ProductionSmoke.ps1` | Add a pre-provision activation-only mode; keep normal smoke exact. | A loose precheck must still refuse mixed/empty activation state. |
| `scripts/Test-AzureDeploymentPlan.ps1` | Use activation-only mode only from `PreProvision`; statically assert the separation. | Normal smoke must not accidentally weaken. |
| `docs/runbook.md` | State the pre-release pre/post distinction. | Documentation must match executable behavior. |
| `.agents/skills/pegasus-release/SKILL.md`, `.codex/skills/pegasus-release/SKILL.md` | Keep both release skill entry points synchronized. | Divergent release instructions would recreate the problem. |
| `docs/current-architecture.md`, `docs/operations.md` | Refresh only after release 32 is actually deployed and smoked. | Must not claim deployment early. |

## Context

| File | Why read it |
| --- | --- |
| `infra/modules/platform.bicep` | Owns the new exact nine settings and one-minute schedule. |
| `src/Pegasus.Worker/IntakeFunctions.cs` | Owns the renamed timer function. |
| `docs/engineering.md` | Exact-SHA release and evidence boundaries remain unchanged. |

## Out of scope

No dual timer, feature flag, compatibility adapter, additional Azure resource, or permanent support for the old function name.
