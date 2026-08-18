# Proof — DELIV-007 (verified on merged `main` `f1e116c6`, 2026-08-18)

- `gh workflow list` → `repository-check`, `source-workspaces` only; `ls .github/workflows` → `ci.yml`, `workspaces.yml`. No scheduled workflow remains.
- `pwsh ./scripts/Test-CiChangeFlags.ps1` → `CI change classification passed.`
- `pwsh ./scripts/Test-DocumentationLinks.ps1` → 222 files resolve.
- `Invoke-QdosAlphaAcceptance.ps1 -SourceRevision <head>` on the clean tree fails closed with `OfflineCandidate is blocked: -CapacityDatasetManifest is required…` (unchanged behaviour of the remaining profile).
- `grep -rn "CiPressure|QdosPressure|qdos-pressure|PerformanceTests" .github scripts docs` → only the retirement note in `docs/runbook.md`.
- PR #402 CI: 10/10 checks pass (one hosted-runner LocalDB timeout on `sql-integration (1)` re-run green); merged 2026-08-18 (`74613fbd`); promoted with release 9.
