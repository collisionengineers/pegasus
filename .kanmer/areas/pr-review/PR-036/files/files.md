# Files

## Modify

- `src/Pegasus.Infrastructure/Persistence/Migrations/20260820100724_RetainedMailSearchDocuments.cs` — grant/revoke Worker SELECT, INSERT, DELETE only.
- `scripts/Invoke-AzureDatabaseBootstrap.ps1` — mirror the exact three-verb Worker matrix.
- `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` — prove exact Web/Worker grants for `IntakeSearchDocuments`, including absent UPDATE.
- `docs/current-architecture.md` — state the exact Worker projection permissions alongside Web SELECT.

## Overlap and dependencies

- All files are part of [[TICK-053]] / PR #469 except the established SQL permission test file, which is newly added to the shared PR diff.
- No new migration is needed because the feature migration is unmerged. No deployment or external write.
