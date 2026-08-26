# Files — PR-066

## Implementation

| Path | Change and risk |
| --- | --- |
| `infra/modules/platform.bicep` | Change only `alwaysReady[].name` to `function:UnifiedWorkFunction`; risk is deployment scale-group mismatch. |
| `tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs` | Require the prefixed designation while leaving the function census unchanged. |
| `scripts/Test-AzureDeploymentPlan.ps1` | Require the prefixed designation in compiled deployment-plan validation. |

## Context files

| Path | Why read |
| --- | --- |
| `src/Pegasus.Worker/IntakeFunctions.cs` | Confirms the runtime Function name remains `UnifiedWorkFunction`. |
| `docs/adr/0033-warm-unified-work-queue-for-five-second-intake.md` on PR #560 | Defines the one warm unified consumer; no broader architecture change belongs here. |
| INTK-043 plan and post-implementation report | Establish that PR #560 owns the wider unified-route change and PR-066 is only its correction. |

## Out of scope

No queue/function rename, new scale group, compatibility path, deployment, or unrelated PR #560 cleanup.
