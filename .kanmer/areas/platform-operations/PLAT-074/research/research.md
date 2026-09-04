# Research — PLAT-074: Azure SQL Database container qualification

## Question

Can the private-preview Azure SQL Database container replace Pegasus's pinned SQL Server container for Linux local development without weakening migrations, grants, database lifecycle or integration-test evidence?

## Findings

- Microsoft describes this as the Azure SQL Database engine running locally, not Azure SQL Edge, SQL Server Developer Edition or a serverless database. Engine identity should be `SERVERPROPERTY('EngineEdition') = 5` and edition `SQL Azure`. Sources: https://microsoft.github.io/azure-sql-database-container/ and https://github.com/microsoft/azure-sql-database-container/blob/main/skills/README.md.
- It is a private preview for local development and CI only. The image is in `sqldbpreview-dpgaeqhmgphzd4bk.azurecr.io`; pull-only credentials come only through preview signup, may rotate, and the separate preview license is supplied at signup. It is not a production deployment unit. Sources: https://microsoft.github.io/azure-sql-database-container/prerequisites.html and https://github.com/microsoft/azure-sql-database-container.
- Native Ubuntu/Debian x64 and Docker 24+ are supported. Microsoft specifies 2 CPU cores, 4 GB runtime memory and 10 GB free disk. This host is amd64 with Docker 29.8.0, about 6.7 GiB currently available memory and 939 GiB free disk; only one database-heavy workload should run at once.
- The current documented image reference is `sqldbpreview-dpgaeqhmgphzd4bk.azurecr.io/azure-sql/db-dev:latest`. Adoption requires resolving `latest` to an immutable digest after an authenticated pull. Source: https://microsoft.github.io/azure-sql-database-container/getting-started.html.
- The Docker credential store currently has no entry for the preview registry. `docker pull sqldbpreview-dpgaeqhmgphzd4bk.azurecr.io/azure-sql/db-dev:latest` failed with `authentication required`; correlation id `b14698c2-1c56-4087-b79c-4b81486a9a3c`. No password was requested, supplied or recorded.
- Microsoft lists active parity gaps: some cloud restrictions are not enforced locally, defaults can differ, database creation is a two-step master connection, GUI compatibility is incomplete, and BACKUP/RESTORE are unsupported. Source: https://microsoft.github.io/azure-sql-database-container/known-limitations.html.
- Pegasus's external SQL test path already creates a database from `master`, applies EF migrations per database, and deliberately disables its BACKUP/RESTORE template. Therefore lack of BACKUP/RESTORE does not automatically disqualify the engine, but the full external SQL suite must prove the slower migration path.
- `scripts/PegasusPlatform.ps1` is currently coupled to the SQL Server Developer image through `MSSQL_PID=Developer`, a 2048 MB memory limit and a readiness probe that reads `MSSQL_SA_PASSWORD` inside the container. The preview documents a 4 GB runtime allocation; adoption would require a separately reviewed platform change rather than a tag-only swap.
- Pegasus migrations include database roles and object-level GRANT statements intended for Azure SQL. Actual compatibility cannot be claimed until the preview engine runs the migration stream and the SQL integration suite.
- The current pinned SQL Server image remains installed and its split integration evidence passed under PLAT-073. It is the safe fallback while preview access is unavailable.

## Implications

The Azure SQL Database container is strategically useful because it offers materially better Azure SQL engine parity than the current SQL Server Developer image. It is not presently adoptable on this workstation: the required private-registry credential is absent, so the image digest, engine identity, startup contract, migrations, grants and tests cannot be observed.

This spike therefore records an INCONCLUSIVE qualification and retains the existing immutable SQL Server image without changing code or weakening tests. Once an operator signs in interactively to the preview registry under the applicable preview license, repeat the pull, record the digest, run the engine-identity query, apply all migrations and grants, and execute the complete SQL integration lane with per-run cleanup. Only a complete PASS should authorize an adoption ticket.

## Open questions

None for this disposition. Preview access is an external prerequisite for a future qualification attempt, not a fact this ticket can safely assume.
