# Checklist — DELIV-054

- [x] Step 1 — Replace the two glob-based ZIP calls with root-preserving ZipFile construction.
- [x] Step 2 — Require Worker .azurefunctions and Web .playwright roots in existing artifact validation.
- [x] Step 3 — Replace placeholder ZIP fixtures with valid hidden-directory ZIP archives and root-free negative cases.
- [x] Step 4 — Hand off the scoped change for parent-arranged independent verification; do not run it locally.

## Progress notes

2026-09-08: Completed Steps 1–3 in
`DELIV-054-hidden-runtime-zips`. Static diff and whitespace inspection were
clean.

2026-09-08: The independent host verifier recorded PASS for the frozen
three-file target in `scratch/execution.md`: all three PowerShell parser
checks exited 0; `pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1`
exited 0; and `pwsh -NoProfile -File
./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` exited 0. No package
build occurred; immutable actual-release packaging remains D6 work.
