# Pegasus

Pegasus is Collision Engineers' clean-room case-management and reporting
application. The current repository is a .NET 10 modular monolith in
`0.0.0-development`; `0.1.0-alpha.1` is the first planned QDOS release target,
not an implementation, deployment, or acceptance claim.

## Get started on Windows

This repository retains supplied reference paths up to 235 characters. Before
cloning, enable Windows long-path support and configure Git for Windows with
`git config --global core.longpaths true`; otherwise checkout can fail before
the PowerShell workflow starts.

```powershell
npm ci
dotnet restore ./Pegasus.slnx
sqllocaldb start MSSQLLocalDB
dotnet run --project ./src/Pegasus.Web --launch-profile https -- --migrate-development
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
dotnet run --project ./src/Pegasus.Web --launch-profile https --no-build
```

The current caller is the Development-only intake route at
`https://localhost:7139/Intake/Upload`; it creates a persisted pre-case
receipt/draft, not a case or reference. See the
[local-development runbook](docs/runbooks/local-development.md) and
[current implementation handoff](docs/agent-notes/current-implementation-handoff.md).

Start with [repository documentation](docs/index.md),
[product requirements](docs/product/index.md),
[capability inventory](docs/product/capabilities.md),
[roadmap](docs/roadmap.md), [architecture](docs/architecture.md), and
[operations](docs/operations.md). The
[QDOS alpha gap](docs/product/qdos-alpha-gap.md) records remaining
`0.1.0-alpha.1` work.

`workspaces/` contains independently validated, source-only imports for
document extraction, report rendering, AI strategy, and agent skills. They are
not application callers or projects in `Pegasus.slnx`; see
[workspace authority and provenance](workspaces/README.md).

Local genuine inputs remain ignored and immutable under `corpus/`. Generated
evaluation and build evidence belongs under ignored `artifacts/`.
