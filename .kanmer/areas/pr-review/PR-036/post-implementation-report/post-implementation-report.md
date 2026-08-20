# Post-implementation report

Implemented in `fc6840361c1c19ece9a75d7ea68c713c75d01b75` on PR #469.

The unmerged `IntakeSearchDocuments` migration and bootstrap census now grant Worker only `SELECT, INSERT, DELETE`; UPDATE is absent because the existing receipt writer removes and recreates rows. A fresh migrated-database test proves Web `SELECT` only and the exact three Worker grants. Current architecture states the same caller-backed matrix.

Files: `src/Pegasus.Infrastructure/Persistence/Migrations/20260820100724_RetainedMailSearchDocuments.cs`, `scripts/Invoke-AzureDatabaseBootstrap.ps1`, `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs`, and `docs/current-architecture.md`.

Evidence: exact SQL permission proof passed; `scripts/Test-MigrationGrants.ps1` passed for 59 migrations; `scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` passed; receipt persistence owning class passed within the 39/39 combined run; Release solution build passed with 0 warnings/errors; `git diff --check` passed. No deployment, live database write, or follow-up migration occurred.
