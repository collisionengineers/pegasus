# Pegasus

Pegasus is Collision Engineers' clean-room case-management and reporting
application. The repository uses .NET 10. `0.1.0-alpha.1` is deployed to the
sole production environment
([operations](docs/operations.md#production-environment)); operator acceptance
remains outstanding, so deployment is not an acceptance claim.

## Get started

Develop on Windows with PowerShell 7 or on Linux with PowerShell 7 — one
platform per workstation, not a mixture.

### On Windows

The repository retains long supplied-reference paths (longest tracked path is
about 122 characters), and build output nests deeper. Enabling Windows
long-path support and configuring Git for Windows before cloning avoids any
checkout-root constraint:

```powershell
git config --global core.longpaths true
npm ci
dotnet restore ./Pegasus.slnx --locked-mode
sqllocaldb start MSSQLLocalDB
dotnet build ./Pegasus.slnx --configuration Release --no-restore
pwsh ./tests/Pegasus.IntegrationTests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
dotnet run --project ./src/Pegasus.Web --configuration Release --launch-profile https --no-build -- --migrate-development
dotnet run --project ./src/Pegasus.Web --configuration Release --launch-profile https --no-build
```

The Playwright step installs the pinned Chromium the `Browser` test lane needs
(CI performs the same step); alternatively,
`pwsh ./scripts/Initialize-LocalDevelopment.ps1` performs initialization
including that browser install.

### On Linux

Long paths need no configuration. The local database is a SQL Server container
rather than LocalDB, so start one and point the application at it:

```powershell
npm ci
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
pwsh ./tests/Pegasus.IntegrationTests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=SqlServer"
$env:ConnectionStrings__Pegasus = 'Server=127.0.0.1,<port>;Database=PegasusDevelopment;User ID=sa;Password=<password>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
dotnet run --project ./src/Pegasus.Web --configuration Release --launch-profile https --no-build -- --migrate-development
dotnet run --project ./src/Pegasus.Web --configuration Release --launch-profile https --no-build
```

The `SqlServer` test lane runs on Linux too once the tests are pointed at that
container; [the runbook](docs/runbook.md#locked-restore-build-and-test) owns
the exact variables. Prefer `pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action
Start` (after `Initialize-LocalDevelopment.ps1` has run once) for the default
Live UI, which manages the database, Azurite, Web, and Functions host. Use
`pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start -UiMode Test` to open
the disposable static UI catalogue without starting Pegasus or its local
dependencies. The catalogue is generated from current Razor renders with
`pwsh ./scripts/Update-TestUiSnapshots.ps1`; its HTML is not edited manually.

The first `dotnet run` applies every committed Development migration and exits;
the second starts Web against the migrated database. Normal Web startup never
applies migrations.

Exact prerequisites, initialization, migration, test profiles, and evidence
limits are in the [runbook](docs/runbook.md); deployed state and dated evidence
are in [operations](docs/operations.md). Current work is tracked on the Kanmer board (`.kanmer/`); start with the
[documentation map](docs/index.md),
[requirements](docs/prd/README.md), [capabilities](docs/capabilities.md),
[architecture](docs/current-architecture.md), and the
[engineering workflow](docs/engineering.md).

`workspaces/` preserves provenance for retired source imports. Their accepted
renderer and document-reader slices now live in the application; the old
imports are not active projects or deployment units. See
[workspace authority and provenance](workspaces/README.md).

Local genuine inputs remain ignored and immutable under `corpus/`. Generated
evaluation and build evidence belongs under ignored `artifacts/`.
