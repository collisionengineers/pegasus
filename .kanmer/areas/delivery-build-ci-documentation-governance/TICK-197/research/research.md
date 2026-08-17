# Research — TICK-197: infrastructure validation lane

## Question

Does repository policy require an infrastructure CI lane, and can the existing infrastructure validation be run in CI without credentials, cloud writes, or overlap with the active UI revamp?

## Findings

- Repository engineering policy already requires Bicep compilation in the static/build/architecture evidence tier (`docs/engineering.md`, “Required evidence tiers”). The ticket’s “lane or deliberate absence” choice is therefore resolved in favor of establishing a lane unless that policy is separately changed.
- The current `.github/workflows/ci.yml` has documentation, reference-data, unit, SQL integration, browser, and QDOS pressure jobs, but no infrastructure job and no call to `scripts/Test-AzureDeploymentPlan.ps1`.
- The workflow’s change detector excludes `infra/**`, `azure.yaml`, and `scripts/Test-AzureDeploymentPlan.ps1`. An infrastructure-only change can therefore skip every build-oriented validation lane.
- `scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` is the existing repository-owned validator. Its unconditional path reads source/configuration, checks fail-closed deployment invariants, and invokes `az bicep build --file infra/main.bicep --stdout`. Credential-, environment-, manifest-, live-readback-, and cloud-dependent behavior is confined to `Artifact`, `PreUpload`, `PreMigration`, and `PreProvision` modes.
- A live local execution on 2026-08-17 failed with “The Web Container App must scale only from zero to one replica.” `infra/modules/platform.bicep` now specifies `minReplicas: 1` and `maxReplicas: 1` (commit `b3b4d88f`), while the validator still expects zero-to-one. The new lane cannot be green until this pre-existing validator drift is corrected.
- `docs/runbook.md` pins Bicep CLI 0.45.15 for supported live tooling and says Bicep compilation proves syntax/type consistency only. The CI lane must preserve that evidence claim and must not authenticate, run `azd`, deploy, or claim live Azure proof.
- `docs/runbook.md` explicitly records GitHub Actions/OIDC deployment as “Not planned.” A validation lane is compatible with this boundary because it compiles and checks committed infrastructure without provisioning.
- EPIC-001 excludes `src/Pegasus.Web/**`, UI browser/snapshot tests, `design/**`, and `.stitch/**`. The likely workflow-and-validator change stays entirely outside those surfaces.
- Existing same-machine claims are KANMER-001/KANMER-002 documentation cleanup plus unrelated application worktrees. The likely implementation files (`.github/workflows/ci.yml` and `scripts/Test-AzureDeploymentPlan.ps1`) are clean and are not named by the active UI-revamp working-tree changes. Documentation edits should be avoided unless planning finds an actual stale canonical claim, because KANMER-001/KANMER-002 own overlapping documentation cleanup.
- The ticket’s `feature` profile and `docs_todo` are metadata inherited from the old import. Repository process belongs in AGENTS.md/engineering governance, not a new product PRD/FRD/ADR; implementation should not invent a governing product document merely to satisfy the imported metadata.

## Implications

Establish a path-scoped, credential-free infrastructure validation job in the existing repository-check workflow. Reuse `Test-AzureDeploymentPlan.ps1 -Mode Local` rather than duplicating its assertions, correct the stale replica expectation exposed by the live run, and ensure changes to the workflow, validator, `infra/**`, and `azure.yaml` activate the lane. The lane’s evidence is limited to committed-plan assertions plus Bicep syntax/type compilation.

## Open questions

None. Repository policy resolves the lane choice, and no user-only product or cloud-authority decision is required for local CI validation.
