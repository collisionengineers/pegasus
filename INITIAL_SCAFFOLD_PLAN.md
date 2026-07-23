# Initial scaffold plan

Status: reviewed and implemented on 2026-07-23. This establishes an executable foundation, not a completed business feature.

## Scope

Build the smallest runnable .NET 10 modular monolith and its deployment skeleton:

1. Pin the .NET SDK and shared compiler settings.
2. Create Core, Infrastructure, Web, and isolated Functions Worker projects.
3. Add one honest Web health/readiness surface and an operator-oriented landing page.
4. Add a Worker host with no pretend business trigger. The first QDOS slice will add a real caller and corpus-backed evidence together.
5. Add unit, integration, and architecture test projects.
6. Add PowerShell doctor, structure, and repository-check entry points.
7. Add Bicep and `azd` files for the accepted F1 development/B1 production shape, but do not deploy.
8. Add one CI workflow running the same repository check used locally.

## Azure shape to prepare

- Development and production are separate environments/resource groups.
- ASP.NET Core Web/API on Linux App Service: F1 development, B1 production.
- Azure Functions isolated worker on Flex Consumption FC1.
- Azure SQL: Basic development, S0 production; Microsoft Entra-only server authentication and managed identity from applications.
- Storage LRS for Functions host state, queues, and temporary intake artifacts; shared-key access disabled.
- Key Vault with RBAC, managed identities, Log Analytics, Application Insights, and Document Intelligence.
- No Defender, private networking, deployment slot, custom domain, malware scanning, or deployment in this scaffold.

## Review

The scaffold plan was vetted against current Microsoft Learn guidance and the Azure MCP best-practice checks. .NET 10 requires the isolated Functions model and Flex Consumption rather than Linux Consumption. The Worker packages must be at least `Microsoft.Azure.Functions.Worker` 2.50.0 and `Microsoft.Azure.Functions.Worker.Sdk` 2.0.5.

Changes made during review:

- Removed a proposed heartbeat/timer function because an unneeded trigger would be dark code.
- Kept one Core shared by Web and Worker rather than creating an intake engine in each host.
- Kept infrastructure deployable only after explicit validation and approval; current Azure resources are inventoried but untouched.
- Added an architecture test and a structure test instead of recreating dozens of repository gates.
- Deferred Identity schema and external adapters until the first real workflow provides their contracts.

## Acceptance checks

- `dotnet restore CollisionSpike.slnx`
- `dotnet build CollisionSpike.slnx --no-restore --configuration Release`
- `dotnet test CollisionSpike.slnx --no-build --configuration Release`
- `az bicep build --file infra/main.bicep`
- `pwsh ./scripts/Test-RepositoryStructure.ps1`
- `pwsh ./scripts/Invoke-RepoCheck.ps1`

Passing these checks proves the scaffold and its boundaries. It does not prove intake, case numbering, integrations, or deployment.
