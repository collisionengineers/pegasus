# Pegasus

Pegasus is Collision Engineers' clean-room case-management and reporting
application. The repository uses .NET 10 and remains in development;
`0.1.0-alpha.1` is an allocated release target, not an implementation,
deployment, or acceptance claim.

## Get started on Windows

The repository retains long supplied-reference paths. Enable Windows long-path
support and configure Git for Windows before cloning:

```powershell
git config --global core.longpaths true
npm ci
dotnet restore ./Pegasus.slnx --locked-mode
sqllocaldb start MSSQLLocalDB
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
dotnet run --project ./src/Pegasus.Web --launch-profile https --no-build
```

Exact prerequisites, initialization, migration, test profiles, and evidence
limits are in [operations](docs/operations.md). Start with the
[documentation map](docs/index.md), [requirements](docs/requirements.md),
[capabilities](docs/capabilities.md), [architecture](docs/architecture.md), and
[engineering workflow](docs/engineering.md).

`workspaces/` contains independently maintained and buildable source imports.
They are not Pegasus callers, runtime acceptance, projects in `Pegasus.slnx`,
or deployment units. See [workspace authority and provenance](workspaces/README.md).

Local genuine inputs remain ignored and immutable under `corpus/`. Generated
evaluation and build evidence belongs under ignored `artifacts/`.
