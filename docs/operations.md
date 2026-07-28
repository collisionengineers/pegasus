# Operations

## Supported environment and prerequisites

- Platform: Windows
- Shell: PowerShell 7
- Offline baseline: PowerShell 7.6.3, .NET SDK 10.0.302, Node 24/npm 11, Azurite 3.36.0, Functions Core Tools 4.12.1, SQL Server Express LocalDB, and trusted .NET Development HTTPS.
- Azure CLI, Bicep, `azd`, Exchange, Box, Infisical, and cloud/vendor authentication are not local prerequisites.
- Tool checks and approved live-only pins are in the [developer runbook](runbooks/developer-workstation.md).
- Direct local process/state ownership is in [local development](runbooks/local-development.md); evidence profiles are in the [testing runbook](runbooks/testing/README.md).

## Canonical verification

```powershell
dotnet restore ./Pegasus.slnx
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
```

Run focused test projects while iterating, then run the solution commands above
before delivery. Genuine corpus, browser, LocalDB/Azurite/Functions, cloud, and
operator evidence are separate caller-specific gates.

Source workspaces validate independently and are not part of the application
solution:

```powershell
Push-Location ./workspaces/document-extraction; dotnet test ./CollisionDocNet.slnx --configuration Release; Pop-Location
Push-Location ./workspaces/report-renderer; dotnet test ./CollisionRenderer.sln --configuration Release; Pop-Location
npm ci --prefix ./workspaces/ai-centre/services/collision-brain
npm test --prefix ./workspaces/ai-centre/services/collision-brain
Push-Location ./workspaces/ai-centre/skills/tools; python -m unittest test_pack_skill; Pop-Location
```

These checks prove only their imported source snapshots. They do not activate
an application reference, model, skill, external call, or deployment.

## Local run, build, and test

```powershell
npm ci
dotnet restore ./Pegasus.slnx
sqllocaldb start MSSQLLocalDB
dotnet run --project ./src/Pegasus.Web --launch-profile https -- --migrate-development
dotnet test ./Pegasus.slnx --configuration Release --filter "Category!=Corpus"
dotnet run --project ./src/Pegasus.Web --launch-profile https --no-build
```

The explicit command applies migrations and exits; normal Web/Worker startup
never changes schema. The current Development route is
`https://localhost:7139/Intake/Upload`. `DevelopmentOffline` fails outside
Development. The actual local Functions host currently starts without a trigger;
that is host evidence only, not a Worker caller.

## Deploy

The target design is in [the deployment plan](../.azure/deployment-plan.md) and
accepted [ADR-0009](architecture/decisions/ADR-0009-direct-terminal-azure-deployment.md).
It is not executable or production-ready. `azd up` is not the production release
route. Packaging, hashes/provenance, explicit migration, identity resolution,
preview, Web/Worker deployment order, health/smoke evidence, and prior-artifact
recovery must first be implemented and reviewed.

Every external read or mutation requires explicit approval after the exact
targets, scope, and operation are shown. Repository tools and credentials do
not provide authority by themselves.

## Configuration and secrets boundary

- Web composition and database selection: `src/Pegasus.Web/Program.cs` plus environment configuration.
- Local Development flag/path: `src/Pegasus.Web/Properties/launchSettings.json`; generated state stays under ignored `artifacts/`.
- Target Azure parameters/topology: `infra/`, `azure.yaml`, and `.azure/deployment-plan.md`.
- Repository Codex app/tool availability: `.codex/config.toml`; availability never authorizes external action.
- Use managed identity and scoped RBAC. Store unavoidable third-party secrets in Infisical or Key Vault; never commit values, connection strings, readable passwords, or generated secrets.

## Monitoring and diagnosis

Current Web exposes `/health/live` and database-backed `/health/ready`.
Application Insights packages are registered for the Worker, but there is no
Worker caller to observe. The planned release requires correlated Web/Worker
telemetry, dependency readiness, ingestion/processing/Box/matching/chasing/EVA
alerts, authentication anomalies, availability, and cost alerts. Bicep
compilation proves syntax/type consistency only.

The [dated Azure inventory](azure/current-inventory.md) is a 2026-07-23 snapshot
and may be stale. Refresh requires separate authorization immediately before a
cloud decision.

## Recovery

- Local ignored artifacts are disposable Development evidence; preserve `corpus/` unchanged.
- Production releases retain the prior immutable application artifact for redeployment and apply migrations explicitly before application packages.
- Database recovery must prove the 15-minute recovery point and four-hour restoration path before `0.1.0-alpha.1` acceptance and after material persistence/release changes where required.
- Predecessor retirement is separate from Pegasus deployment and requires exact-target approval; never begin by deleting `rg-collisionspike-dev`.

## GitHub work taxonomy

- Repository visibility: public as explicitly authorized on 2026-07-27. The
  full tracked history and documentation, including operator notes and supplied
  reference material, are publicly readable; never commit secrets or material
  that is not approved for public source control.
- Work kinds: Feature, Bug, Task, Decision; each workflow-owned issue receives exactly one `type:*` label.
- Registered project-specific categories: none.
- Delivery board: [Pegasus Delivery](https://github.com/users/collisionengineers/projects/3), user-owned and linked to this repository.
- Status: Triage, Ready, In progress, In review, Done.
- Priority: P0 Critical, P1 High, P2 Normal, P3 Low.
- Horizon: Now, Next, Later. Target releases use milestones when allocated.
- Saved views, charts, auto-add behavior, and private issue-form enforcement require human visual confirmation where API readback cannot prove them.
- Capability rows do not become issues automatically. Issue #3 owns the QDOS alpha delivery plan; issue #6 owns this repository-orientation change.

## Supported platforms and release operations

Repository development and release operations are Windows/PowerShell 7 only.
The application targets .NET 10 for ASP.NET Core and Azure Functions isolated
worker. The 2026-07-27 currency check found .NET 10 active LTS support through
2028-11-14 and Functions 4.x support for .NET 10 isolated; the checked Worker
2.52.0 and Worker SDK 2.0.7 exceed Microsoft's stated minimums. These current
Microsoft facts can drift and must be refreshed for a version/host decision.
