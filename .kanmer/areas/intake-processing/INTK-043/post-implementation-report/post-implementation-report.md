# Post-implementation report — INTK-043

## Summary

Replaced the separate intake and external-work queue consumers with one typed `intake-work` route and one always-ready 2 GiB `UnifiedWorkFunction`. Existing Core intake and custody processors, durable claims, recovery and poison reconciliation remain the policy owners. Added low-cardinality processing spans and aligned release validation and governing documents; no cloud state was changed.

## Changes

| File | Change | Why |
|---|---|---|
| `docs/adr/0032-near-real-time-durable-intake-triggering.md` | Marked the scale-to-zero portion superseded | ADR-0033 replaces that technical choice for the critical queue only. |
| `docs/adr/0033-warm-unified-work-queue-for-five-second-intake.md` | Added the accepted unified warm-route decision | Records the required architectural exception and its limits. |
| `docs/adr/README.md` | Indexed ADR-0033 and supersession | Keeps the decision view authoritative. |
| `docs/capabilities.md` | Updated the intake latency capability | Records the unified warm route and measurement target. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Added unified durable-route and latency-attribution behaviour | Defines one shared route without weakening fail-closed intake. |
| `docs/prd/pegasus-product.md` | Added the five-second best-effort outcome | Records the product target and external-provider attribution boundary. |
| `docs/runbook.md` | Updated the incoming seven-function activation, smoke and rollback contract | Makes release instructions agree with the new Worker census while retaining the deployed nine-function fact until release. |
| `infra/modules/platform.bicep` | Removed external queue/RBAC/settings; added one always-ready unified function | Eliminates the second cold queue consumer and obsolete configuration. |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1` | Renamed caller evidence | Keeps permission rationale accurate. |
| `scripts/Invoke-ProductionSmoke.ps1` | Replaced nine old settings with the exact seven-function census | Makes deployment smoke prove the shipped Worker contract. |
| `scripts/Test-AzureDeploymentPlan.ps1` | Validates seven functions and one 2 GiB always-ready unified consumer | Prevents infrastructure, compiled Bicep and smoke drift. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Added claim, artifact/process and allocation stage spans | Attributes Pegasus-controlled latency without source content or policy duplication. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260819115323_UnidentifiedWork.cs` | Renamed caller evidence | Keeps the existing migration's permission comment accurate; schema is unchanged. |
| `src/Pegasus.Infrastructure/Transport/AzureQueueWorkEnqueuers.cs` | Added strict typed messages; both existing enqueuers target one queue | Distinguishes two GUID identifiers without a lookup or parallel transport. |
| `src/Pegasus.Web/Program.cs` | Removed external queue configuration and reuses `intake-work` | Gives both publishers the one production transport. |
| `src/Pegasus.Worker/Functions/ExternalWorkFunctions.cs` | Removed | Deletes the superseded second work and poison consumers. |
| `src/Pegasus.Worker/IntakeFunctions.cs` | Added unified work/poison dispatchers and removed bare-GUID parser | Routes strict typed messages to the existing owning processors. |
| `src/Pegasus.Worker/WorkerAzureClientFactory.cs` | Reduced Worker transport to one queue client | Removes obsolete external queue configuration and provisioning. |
| `src/Pegasus.Worker/WorkerDependencyInjection.cs` | Injects the same queue into both existing enqueuers | Wires the single route without a new business service. |
| `src/Pegasus.Worker/host.json` | Reduced queue idle polling ceiling | Keeps burst consumers responsive while always-ready owns the critical path. |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Proves typed intake/external dispatch and poison routing | Covers the production caller boundary and malformed-message failure. |
| `tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs` | Proves seven functions and always-ready Bicep | Locks the deployment contract. |
| `tests/Pegasus.ArchitectureTests/WorkerAzureClientCompositionTests.cs` | Proves one production/development queue client | Prevents obsolete configuration from returning. |
| `tests/Pegasus.ArchitectureTests/WorkerCompositionTests.cs` | Proves unified Functions construct in both profiles | Confirms production callers are wired. |
| `tests/Pegasus.IntegrationTests/ReadinessEndpointTests.cs` | Removed obsolete external queue startup configuration | Keeps startup coverage aligned with the single queue. |

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: e-mail and manual upload still enter one Core-owned durable intake path; typed transport changes no identification, classification, extraction, allocation or fail-closed policy.
- `docs/frd/frd-05-documents-extraction-and-custody.md`: the existing `IProcessQueuedExternalWork` processor, durable claims, poison reconciliation and recovery remain unchanged; only its transport consumer is consolidated.
- ADR-0033: exactly one 2 GiB always-ready queue consumer is configured, with no second runtime, queue, cache or compatibility path.

## Risks / follow-ups

- Five-second p95 is not proven locally. Deployment must measure supported e-mail and manual-upload cohorts and attribute Outlook/Graph and Box latency separately.
- Existing messages on the pre-release `external-work` queue are intentionally not migrated; repository policy requires one coherent target state rather than a compatibility reader.
- [[MAIL-013]] owns Graph wake-up. [[INTK-001]] owns truthful sender/state projection. This ticket does not duplicate either.
- `docs/current-architecture.md` and `docs/operations.md` retain the Release 32 nine-function deployed snapshot until deployment proves the new state.

## Verification hand-off

On the merged target, run:

- `dotnet restore Pegasus.slnx --locked-mode`
- `dotnet build Pegasus.slnx --configuration Release --no-restore`
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --no-restore` — expect 999/999.
- `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --no-restore` — expect 99/99.
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter "(FullyQualifiedName~LocalIntakeAccessTests|FullyQualifiedName~ReadinessEndpointTests)"` — expect 16/16.
- `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — expect deployment-plan validation and Bicep compilation to pass.
- `git diff --check origin/dev...HEAD`

After a separately authorised deployment, confirm the exact seven-function census, one always-ready `UnifiedWorkFunction`, then measure real supported intake cohorts before claiming the five-second target.
