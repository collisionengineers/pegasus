# Plan

## Governing docs

- `docs/runbook.md`: retain exact target, immutable artifact, activation and post-deployment smoke requirements while making the pre-provision check proportional.
- `docs/engineering.md`: preserve exact-SHA promotion and evidence boundaries.
- `docs/operations.md`: record only what release 32 actually proves.

## Steps

1. Reuse `Invoke-ProductionSmoke.ps1` and add one explicit activation-only switch for pre-provision. It must require at least one deployed activation setting and uniform expected values; the existing exact-name comparison remains the default.
2. Make `Test-AzureDeploymentPlan.ps1` pass that switch only in `PreProvision`, and extend its static checks so post-deployment smoke remains exact. Update both existing release-skill copies and the runbook wording.
3. Run local deployment-plan validation and focused script checks, perform the required simplification review, commit, open a PR, obtain independent review/green CI, merge, promote the new exact SHA under the supplied merge authority, rebuild immutable artifacts, and resume release 32. After smoke, update current-state docs through the normal release evidence PR.

## Proof

Pre-provision passes against release 31's old enabled timer name; post-deployment smoke passes only after release 32 exposes the new exact census and one-minute schedule.

## Simplification pass — 2026-08-26

- Reuse: kept the existing production smoke and deployment-plan entry points; no new script or policy owner.
- Simplicity: one switch changes only whether names are compared before provisioning. Empty inventories and inconsistent activation still fail.
- Efficiency: the same single Azure settings read serves both checks; no additional cloud call or retry.
- Altitude: the strict release contract remains the default and post-deployment gate. No compatibility path or retained legacy implementation was added.
- Findings: no further behaviour-preserving simplification identified.
