# Files — DELIV-047

## Where the change lands

| Path | Why |
| --- | --- |
| `docs/adr/0037-linux-authorised-release-workstation.md` | Record the durable Linux-x64 release-host choice without replacing the direct-terminal or local-to-production decisions. |
| `docs/adr/README.md` | Index ADR-0037. |
| `AGENTS.md` | Replace the obsolete Windows-only release rule because command/convention changes must update agent instructions. |
| `docs/runbook.md` | Make Linux the sole release workstation and change migration commands to `efbundle`. |
| `docs/current-architecture.md` | Record the implemented release-tooling shape after merge, without claiming production deployment. |
| `docs/operations.md` | Record Linux release readiness and the remaining unauthenticated/unexecuted production boundary. |
| `scripts/Build-ReleaseArtifacts.ps1` | Reuse `PegasusPlatform.ps1`, require Linux x64, remove the configurable migration RID, and emit schema-3 Linux artifacts. |
| `tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs` | Keep the isolated Local deployment-plan fixture complete when validation reads the release build script. |
| `scripts/Test-AzureDeploymentPlan.ps1` | Reject non-schema-3, non-`linux-x64`, non-`efbundle` manifests and assert the build-host contract. |
| `scripts/Invoke-Doctor.ps1` | Include ORAS in the existing tool-health surface. |
| `scripts/PegasusPlatform.ps1` | Add the ORAS Linux repair hint to the existing single repair-hint table. |
| `.agents/skills/pegasus-release/SKILL.md` | State Linux x64 and ORAS as release preconditions while preserving the exact approval and target route. |
| `.agents/skills/pegasus-release/references/database-migration.md` | Execute the Linux `efbundle` named by the approved manifest. |
| `.zcode/skills/pegasus-release/SKILL.md` | Remove the stale duplicate route and forward to the canonical release skill, matching `.codex`. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `docs/adr/0007-direct-terminal-azure-deployment.md` | Preserve direct authorised-terminal order and explicit migration boundary. |
| `docs/adr/0014-local-to-production-deployment.md` | There is no non-production Azure environment; local proof cannot be relabelled production proof. |
| `.agents/skills/pegasus-release/SKILL.md` | Canonical exact-SHA promotion, artifact, approval, upload, provision, Worker, smoke and evidence route. |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1` | Runtime grant and identity reconciliation is already platform-neutral and must not be duplicated. |
| `scripts/Invoke-ProductionSmoke.ps1` | Exact live release verification remains unchanged. |
| `docs/engineering.md` | Exact-SHA `dev` to `main` promotion and `MERGE AUTH GRANTED` remain unchanged. |
| `EPIC-013/context.md` | No cloud write is authorised; existing approval and rollback boundaries remain. |

## Ripple effects

The release artifact manifest schema changes, so local deployment-plan tests and the release skill must agree. ORAS becomes an explicit WSL prerequisite. A final production release still needs fresh Azure authentication, an exact candidate/manifest, `MERGE AUTH GRANTED`, and exact-target Azure/database write approval. After any real deployment, current-state documents must be updated again with live evidence.

## Out of scope

CI redesign, GitHub Actions deployment, a staging environment, Docker as a second production deployer, product features, email evaluation, Azure SQL preview adoption, production promotion/deployment without fresh authority, and changes to `docs/operator-notes.md` or `corpus/**`.
