# Files — PLAT-036

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Web/appsettings.json` | Add the single Production-default `Microsoft.EntityFrameworkCore.Database.Command: Warning` filter. Risk: an incorrect category or level would either save nothing or hide EF warnings; keep Warning and above. |
| `tests/Pegasus.ArchitectureTests/ApplicationTelemetryVolumeContractTests.cs` | New focused file-level contract proving the shipped Web configuration retains the EF command filter. Risk is low; parse the JSON rather than matching whitespace. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `infra/modules/platform.bicep` | The managed environment deliberately sends `ContainerAppConsoleLogs` and system logs to the workspace; preserve that emergency diagnostic route. It also shows no repository-owned daily quota setting. |
| `src/Pegasus.Web/Health/DatabaseReadinessHealthCheck.cs` | Readiness intentionally checks connectivity and pending migrations; the ticket suppresses successful SQL logging, not readiness correctness. |
| `src/Pegasus.Web/Program.cs` | Maps the readiness endpoint and registers Application Insights. Do not add another telemetry pipeline or change health behavior. |
| `src/Pegasus.Worker/host.json` | Worker request sampling and queue polling settings are separate. Near-real-time intake tickets own timer reduction; this ticket should not duplicate it. |
| `docs/current-architecture.md` | Records the present capped state and distinguishes implemented, deployed, and accepted evidence. It changes only after deployment reality changes. |
| `docs/operations.md` | Owns dated production quota and monitoring evidence; [[DELIV-021]] must refresh it after deployment and observation. |
| `docs/runbook.md` | Requires deployed evidence for ingestion, sampling, KQL, retention, and alert delivery; local verification cannot close the operational claim. |
| `tests/Pegasus.IntegrationTests/ReadinessEndpointTests.cs` | Protects readiness behavior. No production code change should alter these tests or the health contract. |

## Ripple effects

- Build/package output includes the modified `appsettings.json`; the architecture test should prove the source configuration, while the normal Release build proves it remains valid JSON and is copied.
- [[DELIV-021]] must deploy the exact reviewed commit, query daily ingestion by table after a normalized working day, confirm the workspace never enters a capped status, and prove the existing 5xx and exception alert paths still receive signal.
- If the linked intake tickets reduce Worker timers, their lower request/dependency volume is additive evidence, not a dependency for this targeted Web log fix.
- Current-state documents change with the deployment ticket, not during this pre-deployment implementation.

## Out of scope

- Raising `dailyQuotaGb` or making any Azure write.
- Disabling `ContainerAppConsoleLogs` or the diagnostic setting.
- Changing readiness checks or probe frequency.
- Adding a custom telemetry processor, a second sampling policy, or application-specific filtering code.
- Duplicating the Worker polling changes owned by the near-real-time intake tickets.
- Claiming full-day coverage before [[DELIV-021]] measures the deployed result.
