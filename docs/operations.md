# Operations

## Supported environment and prerequisites

- Platform: Windows
- Shell: PowerShell 7
- Current verified baseline: PowerShell 7.6.3, Git 2.53.0, GitHub CLI 2.88.0, .NET SDK 10.0.302, and Azure CLI 2.88.0.
- Full local verification additionally requires SQL Server Express LocalDB, Azure CLI/Bicep, and the restored .NET dependencies. LocalDB was absent during the 2026-07-27 onboarding baseline check.
- The detailed workstation inventory and install/repair commands remain in the [developer runbook](runbooks/developer-workstation.md); re-verify drift-prone versions before relying on them.

## Canonical verification

```powershell
pwsh ./scripts/Invoke-RepoCheck.ps1
```

The default is `Full`. Documentation-only CI changes call the same command with
`-Mode Docs`; unknown or mixed paths fail safe to `Full`. `Docs` proves the
repository/document/issue-form/change-record structure and links but does not
restore, build, test application callers, compile Bicep, or prove product
behavior. `Full` excludes genuine corpus tests unless
`-RequireCorpusEvidence` is explicitly selected.

## Local run, build, and test

```powershell
pwsh ./scripts/Invoke-Doctor.ps1
pwsh ./scripts/Invoke-RepoCheck.ps1
dotnet run --project ./src/CollisionSpike.Web --launch-profile https
```

The Development route is `https://localhost:7139/Intake/Upload`. It is enabled
only by the checked-in Development launch profile and returns 404 outside that
environment/flag boundary. The Worker currently has no operational trigger.

## Deploy

The target design is in [the deployment plan](../.azure/deployment-plan.md) and
accepted [ADR-0009](architecture/decisions/ADR-0009-direct-terminal-azure-deployment.md).
It is not executable or production-ready. `azd up` is not the production release
route. Packaging, hashes/provenance, explicit migration, identity resolution,
preview, Web/Worker deployment order, health/smoke evidence, and prior-artifact
recovery must first be implemented and reviewed.

Every Azure read or mutation requires `$azure-workflow:operate-azure-repository`;
an apply card and explicit approval are required after the exact scope and
operation are shown. Onboarding performs no Azure query or mutation.

## Configuration and secrets boundary

- Web composition and database selection: `src/CollisionSpike.Web/Program.cs` plus environment configuration.
- Local Development flag/path: `src/CollisionSpike.Web/Properties/launchSettings.json`; generated state stays under ignored `artifacts/`.
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
- Database recovery must prove the 15-minute recovery point and four-hour restoration path before V1 acceptance and after material persistence/release changes where required.
- Predecessor retirement is separate from v2 deployment and requires exact-target approval; never begin by deleting `rg-collisionspike-dev`.

## GitHub work taxonomy

- Work kinds: Feature, Bug, Task, Decision; each workflow-owned issue receives exactly one `type:*` label.
- Registered project-specific categories: none.
- Delivery board: [CollisionSpike v2 Delivery](https://github.com/users/collisionengineers/projects/3), user-owned and linked to this repository.
- Status: Triage, Ready, In progress, In review, Done.
- Priority: P0 Critical, P1 High, P2 Normal, P3 Low.
- Horizon: Now, Next, Later. Target releases use milestones when allocated.
- Saved views, charts, auto-add behavior, and private issue-form enforcement require human visual confirmation where API readback cannot prove them.
- Capability rows do not become issues automatically. The onboarding active set is empty pending a separately confirmed implementation selection.

## Supported platforms and release operations

Repository development and release operations are Windows/PowerShell 7 only.
The application targets .NET 10 for ASP.NET Core and Azure Functions isolated
worker. The 2026-07-27 currency check found .NET 10 active LTS support through
2028-11-14 and Functions 4.x support for .NET 10 isolated; the checked Worker
2.52.0 and Worker SDK 2.0.7 exceed Microsoft's stated minimums. These current
Microsoft facts can drift and must be refreshed for a version/host decision.
