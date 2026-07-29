# Azure deployment plan

Status: **Offline/replay release contract is runnable; Azure activation is fail-closed, not approved for validation deployment or provisioning.**

Last reviewed: 2026-07-23, Europe/London.

## Confirmed context

| Item | Value |
|---|---|
| Subscription | Azure subscription 1 |
| Subscription ID | `e6076573-23a5-46a8-acef-7e22d264e5db` |
| Tenant ID | `858cf5b3-aa0a-47a6-9b40-4851fd0afa94` |
| Target region | UK South (`uksouth`) |
| Workload | about 8 office users, about 2,000 cases/month |
| Architecture | .NET 10 modular monolith, Razor Pages/API plus isolated Functions worker |
| IaC/orchestration | Bicep plus Azure Developer CLI |
| Current policy assignments | none returned at subscription scope on 2026-07-23 |

## Proposed per-environment resources

| Resource | Quantity | Development | Production | Reason |
|---|---:|---|---|---|
| Resource group | 1 | separate `dev` | separate `prod` | independent lifecycle |
| Linux App Service plan | 1 | F1 | B1 | selected low-cost tiers |
| Web App | 1 | .NET 10 | .NET 10 | Razor Pages/API and app-managed user accounts |
| Functions plan | 1 | Flex Consumption FC1 | Flex Consumption FC1 | .NET 10 isolated background work |
| Function App | 1 | .NET 10 isolated | .NET 10 isolated | mailbox/queue composition root |
| User-assigned identities | 2 | Web and Worker | Web and Worker | stable least-privilege identities; Worker identity also owns Flex host/package lifecycle |
| Azure SQL logical server/database | 1/1 | Basic | S0 | application plus ASP.NET Core Identity data |
| Functions transport/deployment storage | 1 | Standard LRS | Standard LRS | host internals, deployment package, ID-only work/poison queues |
| Application custody/protection storage | 1 | Standard LRS | Standard LRS | transient intake, Web authentication ring, Box-link ring; Worker is denied the authentication ring |
| Key Vault | 1 | Standard | Standard | third-party credentials only where managed identity cannot replace them |
| Log Analytics/Application Insights | 1/1 | 30 days | 30 days initially | correlated Web/Worker telemetry with local authentication disabled |

## Identity and secret design

- Web and Worker use distinct user-assigned identities.
- The Worker identity exists before the Function App so Flex deployment and host storage can use identity-based access.
- Storage roles are separated between Functions transport/deployment and application custody/protection accounts. Shared-key access is disabled; roles are container/queue scoped except the Function host roles that Azure Functions requires at account scope.
- Azure SQL uses a Microsoft Entra administrator and Entra-only authentication. Runtime contained users are created by client ID with `SID` and `TYPE = E`; runtime principals receive data access only. A temporary migrator group owns schema changes and has no standing runtime use.
- Private networking is a `Not planned` boundary. The scaffold therefore uses public
  service endpoints and the Azure SQL `AllowAllWindowsAzureIps` firewall rule so
  App Service and Flex can reach SQL. Authentication remains Entra-only. This
  broad network reach is an accepted `0.1.0-alpha.1` trade-off, not a planned future
  private-networking migration.
- App settings contain resource names, endpoints, client IDs, and Application Insights connection metadata. Third-party credentials are referenced from Key Vault and are never generated into Bicep output.

## Quota and availability evidence

The 2026-07-23 live inventory is dated evidence only. It does not approve target resource reuse. Immediately before any separately authorised validation or provisioning, recheck service availability, quota, pricing, role-assignment authority, and the exact approved target names. Alpha includes no Document Intelligence/OCR resource.

Before provisioning, recheck:

- F1 App Service plan availability in UK South;
- Flex Consumption and regional app quota;
- SQL logical-server quota;
- two storage accounts per environment;
- role-assignment authority for the provisioning principal.

## Runnable offline/replay route

The release owner may create reproducible, local-only artifacts from a clean
Git working tree and clean output path. This route resolves a supplied full
revision or unambiguous short prefix to the exact checked-out `HEAD`, uses
source-cleared, cache-only locked .NET and tool restore, publishes the existing
Web and Worker projects once with that revision, generates the existing EF
idempotent migration script, verifies the published Web diagnostic source SHA,
writes deterministic ZIP metadata, and records SHA-256 hashes with the exact
revision. It does not authenticate to Azure, create resources, apply migrations,
or deploy packages.

From the repository root, use the approved green revision as provenance:

```powershell
pwsh ./scripts/Build-ReleaseArtifacts.ps1 `
  -SourceRevision 1c2fa19 `
  -OutputDirectory ./artifacts/release/1c2fa19

pwsh ./scripts/Test-AzureDeploymentPlan.ps1 `
  -ArtifactDirectory ./artifacts/release/1c2fa19
```

`Build-ReleaseArtifacts.ps1` refuses a revision mismatch, a dirty checkout, or
an existing output directory before it can produce a promotable manifest. Its
three deployable inputs are `web.zip`, `worker.zip`, and `migration.zip`;
`release-manifest.json` binds their names, lengths, hashes, runtimes, the
verified Web build diagnostic, and the exact 40-character revision. The test
script re-hashes all inputs, verifies that diagnostic binding, required archive
contents, and fixed ZIP timestamps, then compiles Bicep locally.

The Bicep entrypoint permits only `deploymentMode=offline-replay`. Its resource
group and platform module are conditioned on the unreachable
`approved-live-deployment` value, so parameter validation prevents Azure resource
creation from this revision. This is deliberate fail-closed behavior, not a
deployment switch.

## Azure activation gate

No runnable Azure activation route exists. The concrete gate is separate,
recorded approval that names the exact subscription, resource group, principal,
cost scope, data boundary, and migration/deployment sequence, plus a fresh
authorised-terminal recheck of service availability, quota, pricing,
role-assignment authority, target names, SQL Entra administrator, and external
credential readiness.

Only after that evidence exists may a separate infrastructure change replace the
fail-closed Bicep mode and address the remaining platform gap:
`SCM_DO_BUILD_DURING_DEPLOYMENT=true` must be removed before immutable package
deployment can be authorised. Applying the idempotent migration bundle remains
an explicit pre-application step; schema rollback is not a down-migration.
## Deployment blockers

- User approval to create chargeable Azure resources has not been given.
- The predecessor is pre-release and its test application data is not migrated into `0.1.0-alpha.1`. Retirement remains a separately approved operation, not a deployment prerequisite.
- Document Intelligence F0 ownership/reuse has not been decided.
- SQL Entra administrator name/object ID must be confirmed at deployment time.
- GitHub Actions/OIDC deployment is a `Not planned` boundary, not a missing scaffold item.
- Offline Web, Worker, and migration artifact creation and hash verification are implemented; Azure deployment remains blocked because no activation approval exists, the Entra identity/resolution route is unresolved, and remote-build removal has not been implemented.
- External integration credentials and rotation sequence are not prepared.

This file must not be changed to `Ready for Validation` merely because Bicep compiles.
