# Azure deployment plan

Status: **Target design only — not runnable, not production-ready, and not approved for validation deployment or provisioning.**

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

## Intended authorised-terminal route (not runnable)

The release owner uses an authorised terminal with committed Bicep and `azd`.
This is not a GitHub Actions/OIDC route, and `azd up` must not be used for a
production release because it merges provision, package, and deployment without
the required migration boundary.

The intended order is local validation; one-time Web, Worker, and migration
bundle creation with recorded hashes; approved preview/provision; explicit
immutable migration; Web package deployment; live/ready probes; Worker package
deployment; then smoke evidence. Prior application packages are retained for
redeployment; schema rollback is not a down-migration.

The current scaffold cannot perform that order. `azure.yaml` has no migration
step; `dotnet ef` is not pinned or available; `AZURE_PRINCIPAL_NAME` needs a
preflight; the least-privilege Entra directory-resolution path for `CREATE USER
... FROM EXTERNAL PROVIDER` is unresolved; package paths, target runtimes,
pinned tools/dependencies, hashes/provenance, and build-once/deploy-same-artifact
proof are absent; and `SCM_DO_BUILD_DURING_DEPLOYMENT=true` conflicts with
immutable package deployment. A separate infrastructure implementation must
close these gaps before any command below is treated as executable.

## Target release order (not executable)

This is the ADR-0009 order, not a command runbook. Every cloud action requires
separate exact approval for its target, scope, cost, and data boundary. The
placeholders below cannot be resolved until the listed gaps have a separate
infrastructure implementation.

1. Validate locally and review the dated inventory; an authorised refresh is
   required before relying on any live fact.
2. Create Web, Worker, and migration bundles once from the approved revision,
   recording package paths, target runtimes, tool/dependency versions, hashes,
   and build-once/deploy-same-artifact provenance. **Not implemented.**
3. Preflight the authorised terminal identity, `AZURE_PRINCIPAL_NAME`, and the
   least-privilege Entra resolution needed for `CREATE USER ... FROM EXTERNAL
   PROVIDER`; then preview and provision only the approved new `0.1.0-alpha.1` target, never
   `rg-collisionspike-dev`. **Identity path unresolved.**
4. Apply the explicit immutable migration bundle before application deployment.
   **No migration bundle or `azure.yaml` migration step exists.**
5. Deploy the hashed Web package; record live and ready probe evidence. **Package
   route and remote-build removal are not implemented.**
6. Deploy the hashed Worker package and record smoke evidence. Do not connect
   genuine corpus data or live Outlook, Box, or EVA until each integration
   cutover is separately approved.

## Deployment blockers

- User approval to create chargeable Azure resources has not been given.
- The predecessor is pre-release and its test application data is not migrated into `0.1.0-alpha.1`. Retirement remains a separately approved operation, not a deployment prerequisite.
- Document Intelligence F0 ownership/reuse has not been decided.
- SQL Entra administrator name/object ID must be confirmed at deployment time.
- GitHub Actions/OIDC deployment is a `Not planned` boundary, not a missing scaffold item.
- The direct-terminal packaging, migration, identity, Entra-resolution and
  remote-build-removal work described above is not implemented.
- External integration credentials and rotation sequence are not prepared.

This file must not be changed to `Ready for Validation` merely because Bicep compiles.
