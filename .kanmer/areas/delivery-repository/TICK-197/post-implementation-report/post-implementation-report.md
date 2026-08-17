# Post-implementation report — TICK-197

## Summary

Established a path-scoped, credential-free infrastructure validation lane in the repository-check workflow. Infrastructure and validator inputs now activate a Windows job that regression-tests path classification and runs the existing Local deployment-plan validator, including Bicep compilation, without authenticating or touching Azure state.

## Changes

| File | Change | Why |
|---|---|---|
| `.github/workflows/ci.yml` | Exported an infrastructure change signal and added the conditional Local validation job. | Infrastructure-only changes previously skipped executable validation. |
| `scripts/Get-CiChangeFlags.ps1` | Centralized build and infrastructure path classification with fail-safe `ForceAll` behavior. | Keeps workflow routing testable and ensures unreliable diff resolution runs all conditional lanes. |
| `scripts/Test-CiChangeFlags.ps1` | Added positive, negative, dependency, workflow, and fail-safe regression cases. | Proves infrastructure inputs activate the lane while unrelated documentation and UI-only changes do not activate infrastructure validation. |

The pre-existing `minReplicas` validator drift documented by research was already corrected on current `origin/dev` by DELIVE-001, so this branch does not duplicate that edit.

## Governing docs

No product PRD, FRD, or ADR is linked or modified because this is repository CI governance, not product behavior. The implementation supplies the Bicep compilation evidence required by `docs/engineering.md` and preserves `docs/runbook.md`'s boundary: Local validation proves committed invariants and syntax/type consistency only, with no authentication, live readback, provisioning, or deployment.

## Risks / follow-ups

- Hosted Azure CLI/Bicep availability remains a runner dependency; absence or compilation failure is fail-closed.
- TICK-200 owns broader CI wall-clock optimization.
- Review should confirm the dependency census remains complete when Local-mode inputs change in future.

## Verification hand-off

On merged `dev`, run:

- `pwsh ./scripts/Test-CiChangeFlags.ps1` — expect `CI change classification passed.`
- `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — expect `Azure deployment plan validation passed (Local; Worker Disabled settings render 'true').`
- `pwsh ./scripts/Test-DocumentationLinks.ps1` — expect all relative Markdown links to resolve.
- Open a PR changing an `infra/**` file or Local-validator dependency and confirm the `infrastructure` job is scheduled; confirm a UI-only change leaves that job skipped.
