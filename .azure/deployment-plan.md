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
- Azure SQL uses a Microsoft Entra administrator and Entra-only authentication. Migration `20260729176000_AzureSqlRuntimeLeastPrivilege` creates distinct fixed Web and Worker roles. Terminal migration `20260729199000_RuntimeRoleReconciliation` resets their direct DML across the complete application-table census, grants only the exhaustive caller-derived matrix, denies Worker `DELETE` everywhere, and denies Web `DELETE` except on its four required relationship/value workflows. Neither role receives DDL or broad built-in data roles. The post-migration bootstrap creates the fixed managed-identity users from client-ID `SID` plus `TYPE = E`, validates their exact type and membership, and binds each user only to its corresponding role. A temporary migrator group owns schema changes and has no standing runtime use.
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
Git working tree and a new output path. The builder resolves the supplied
revision to the exact checked-out `HEAD`, keeps restore cache-only and locked,
publishes the Web and Worker for `linux-x64`, builds self-contained `win-x64`
migration and bootstrap executables, validates the separately approved
bootstrap manifest, verifies the Web version/source diagnostic, and requires
two byte-identical packaging passes. It does not authenticate to Azure, create
resources, apply migrations, initialize identities, or deploy packages.

From the repository root, use the approved green revision and approved
environment-specific bootstrap manifest as provenance:

```powershell
$SourceRevision = (git rev-parse --verify HEAD).Trim()
$ReleaseDirectory = "./artifacts/release-$SourceRevision"
$BootstrapManifest = "./artifacts/approved/bootstrap-manifest-$SourceRevision.json"

pwsh ./scripts/Build-ReleaseArtifacts.ps1 `
  -SourceRevision $SourceRevision `
  -Configuration Release `
  -ApplicationRuntime linux-x64 `
  -MigrationRuntime win-x64 `
  -BootstrapRuntime win-x64 `
  -BootstrapManifestPath $BootstrapManifest `
  -VerifyReproducible `
  -OutputDirectory $ReleaseDirectory

pwsh ./scripts/Test-AzureDeploymentPlan.ps1 `
  -Mode Local `
  -ArtifactDirectory $ReleaseDirectory
```

`Build-ReleaseArtifacts.ps1` refuses a revision mismatch, a dirty checkout, a
changed bootstrap manifest, or an existing output directory before it can
produce a promotable manifest. The release directory contains exactly
`web-linux-x64.zip`, `worker-linux-x64.zip`,
`migration-bundle-win-x64.zip`, `bootstrap-win-x64.zip`,
`azure-deployment-inputs.zip`, `release-manifest.json`, and
`release-manifest.sha256`. The manifest binds `0.1.0-alpha.1`, terminal
migration `20260729199000_RuntimeRoleReconciliation`, the approved bootstrap
manifest digest, the deterministic source-input tree, runtimes, toolchain,
diagnostic source SHA, and every artifact name, length, and SHA-256. Local
validation independently binds those bytes to the clean checkout, requires the
exact nine packaged Worker triggers and matching disabled settings, and compiles
only the hash-verified packaged Bicep inputs.

The Bicep entrypoint permits only `deploymentMode=offline-replay`. Its resource
group and platform module are conditioned on the unreachable
`approved-live-deployment` value, so parameter validation prevents Azure resource
creation from this revision. This is deliberate fail-closed behavior, not a
deployment switch.

The platform module also provisions every packaged Worker business function with
its exact `AzureWebJobs.<FUNCTION_NAME>.Disabled=true` app setting. Offline
release validation compares those settings with the packaged
`functions.metadata`, so adding a trigger cannot silently make the initial
deployed target live. Any later trigger enablement is a separate, reviewed
app-setting change after its production adapter, evidence boundary, and caller
are approved; one enabled trigger does not enable any other function.

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
- SQL Entra administrator name/object ID must be confirmed at deployment time.
- GitHub Actions/OIDC deployment is a `Not planned` boundary, not a missing scaffold item.
- Offline Web, Worker, and migration artifact creation and hash verification are implemented; Azure deployment remains blocked because no activation approval exists, the Entra identity/resolution route is unresolved, and remote-build removal has not been implemented.
- External integration credentials and rotation sequence are not prepared.

This file must not be changed to `Ready for Validation` merely because Bicep compiles.
