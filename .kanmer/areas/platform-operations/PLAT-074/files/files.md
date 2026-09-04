# Files — PLAT-074

## Where the change lands

No repository file changes in this spike. The result is recorded on the Kanmer ticket and the current SQL Server image remains authoritative.

| Path | Why |
| --- | --- |
| None | Private-registry authentication prevented runtime qualification, so adoption changes are not authorized. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `scripts/PegasusPlatform.ps1` | Owns the pinned image, secret file, lifecycle, 2048 MB limit, loopback port and readiness probe; adoption must extend this owner rather than create a parallel launcher. |
| `scripts/Initialize-LocalDevelopment.ps1` | Owns image acquisition and local initialization. |
| `scripts/Invoke-Doctor.ps1` | Owns the installed-image prerequisite and repair guidance. |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | Owns external SQL connection variables, per-test CREATE/DROP and EF migration lifecycle. |
| `tests/Pegasus.IntegrationTests/LocalDbTemplateDatabase.cs` | BACKUP/RESTORE template explicitly returns null for an external datasource. |
| `tests/Pegasus.IntegrationTests/LocalDbTemplateDatabaseTests.cs` | Template-only assertions are explicitly skipped for the external SQL path. |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1` | Owns Azure SQL runtime principals and grants that a future preview run must exercise where locally applicable. |
| `docs/adr/0014-local-to-production-deployment.md` | Local validation remains distinct from production; no non-production Azure environment may be assumed. |
| `EPIC-013/context.md` | Requires immutable digest adoption only after full evidence and retains the current image on non-PASS. |

## Ripple effects

A future authenticated qualification must cover Doctor and initialization, engine identity, EF migrations, database roles and grants, the complete SQL integration test lane, resource behavior under 4 GB allocation, loopback-only networking, secret handling and cleanup. Preview credential rotation also prevents treating an interactive registry login as stable CI infrastructure.

## Out of scope

Preview signup or license acceptance on the operator's behalf, recording registry secrets, Azure/cloud resources, production database access, application changes, CI redesign, accessibility, release conversion and weakening or skipping existing SQL assertions.
