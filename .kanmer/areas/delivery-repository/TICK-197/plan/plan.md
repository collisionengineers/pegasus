# Plan — TICK-197: Establish an infra validation lane or record its deliberate absence

## Approach

Add a narrow infrastructure output to the existing change detector and a credential-free Windows job that invokes the repository-owned `Test-AzureDeploymentPlan.ps1 -Mode Local`. This reuses the established invariant checks and Bicep compilation instead of duplicating policy in workflow YAML. Current `origin/dev` already contains the corrected one-warm-replica assertion, so implementation will preserve that current reality and add regression coverage for path classification rather than re-editing the validator unnecessarily.

## Governing docs

This repository-workflow change has no linked product PRD, FRD, or ADR and introduces no product behavior or architectural boundary. It follows the existing repository governance in `AGENTS.md`, the Bicep compilation evidence requirement in `docs/engineering.md`, and the local-only evidence boundary in `docs/runbook.md`. No governing document is modified.

## Steps

1. Re-check `origin/dev`, active worktrees, and the UI-revamp exclusions; create the requested isolated branch and worktree from current `origin/dev`.
2. Extend CI path detection with an independently exported infrastructure signal covering `infra/**`, `azure.yaml`, the Local validator and every repository script/configuration input it reads, plus the workflow itself.
3. Add a path-scoped Windows infrastructure job that checks out the repository and runs `./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` without credentials, login, `azd`, or cloud operations.
4. Add credential-free automated regression coverage for infrastructure path classification, including positive inputs and an unrelated/UI-only negative input, without touching UI-owned paths.
5. Run the focused classification tests and Local infrastructure validator, inspect the diff for scope and credential boundaries, then run repository-appropriate static verification.

## Verification

Run the path-classification tests with passing and negative cases, then run `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`. Confirm the workflow exposes and consumes the infrastructure output, uses only `contents: read`, and contains no Azure authentication or deployment step. Use a clean diff against `origin/dev` to prove no UI-revamp path changed.

## Risks / open questions

- GitHub expression/path-detection drift could let an infrastructure change bypass the lane; mitigate with a reusable script and direct tests rather than embedding untestable matching logic only in YAML.
- Hosted-runner Azure CLI/Bicep availability may drift; the Local validator fails explicitly if compilation cannot run, while version ownership remains in the runbook and existing tool bootstrap.
- No open questions remain: repository policy requires compilation evidence and forbids treating this lane as live Azure proof.
