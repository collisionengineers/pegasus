# CollisionSpike v2

CollisionSpike v2 is the clean-room case-management application for Collision Engineers. It is a .NET 10 modular monolith with Core-owned business policy, Infrastructure adapters, and Web/Worker composition roots. The repository is in development and does not claim a released QDOS workflow or Azure deployment.

## Get started on Windows

```powershell
npm ci
dotnet restore ./CollisionSpike.slnx
sqllocaldb start MSSQLLocalDB
dotnet run --project ./src/CollisionSpike.Web --launch-profile https -- --migrate-development
dotnet build ./CollisionSpike.slnx --configuration Release --no-restore
dotnet test ./CollisionSpike.slnx --configuration Release --no-build --filter "Category!=Corpus"
dotnet run --project ./src/CollisionSpike.Web --launch-profile https --no-build
```

The current local route is `https://localhost:7139/Intake/Upload`. See the
[local development runbook](docs/runbooks/local-development.md) for Azurite,
the actual Functions host, isolated runs, state ownership, and proof limits.
See the [implementation handoff](docs/agent-notes/current-implementation-handoff.md)
for the current caller and [V1 gap](docs/product/v1-gap.md) for remaining release work.

Start with the [repository documentation](docs/index.md), [product requirements](docs/product/index.md), [roadmap](docs/roadmap.md),
[architecture](docs/architecture.md), and [operations](docs/operations.md).
Detailed plans, historical decisions, and evidence remain routed from those
owners. Local genuine inputs remain ignored and immutable under `corpus/`.
