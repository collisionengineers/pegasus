- `docs/runbook.md` — "Deployment and release", "Durable Worker activation and rollback", "Recovery" / "Production recovery" sections (existing, unchanged)
- `docs/operations.md` — "Production configuration attempt", "Replacement-image attempt" (the real 2026-08-18 rollback event), "Live activation evidence" (existing, unchanged)
- `scripts/Build-ReleaseArtifacts.ps1`, `Test-AzureDeploymentPlan.ps1`, `Invoke-AzureDatabaseBootstrap.ps1`, `Invoke-ProductionAdministratorBootstrap.ps1`, `Invoke-ProductionSmoke.ps1` (existing, unchanged)
- `artifacts/releases/release-13-2325ed4a/` — retained immutable release artifacts (existing, unchanged)

No files changed by this ticket — verification-only backfill against an already-documented and already-exercised procedure.
