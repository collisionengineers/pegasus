# Files

| Path | Change and risk |
| --- | --- |
| scripts/Invoke-AzureDatabaseBootstrap.ps1 | Replace the report permissions comment with the exact migration ID. No executable change. |

## Context files

- scripts/Test-AzureDeploymentPlan.ps1: existing GRANT-migration basename census must remain unchanged.
- src/Pegasus.Infrastructure/Persistence/Migrations/20260907210000_ReportInputInvalidationPermissions.cs: all five Up grants already match bootstrap rows.
- docs/adr/0007-direct-terminal-azure-deployment.md: deployment uses the checked repository-owned route.
