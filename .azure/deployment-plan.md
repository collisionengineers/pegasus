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
| User-assigned identity | 1 | Worker | Worker | Flex package and host storage lifecycle |
| Azure SQL logical server/database | 1/1 | Basic | S0 | application plus ASP.NET Core Identity data |
| Storage account | 1 | Standard LRS | Standard LRS | Functions host/package, queues, temporary intake |
| Key Vault | 1 | Standard | Standard | third-party credentials only where identity cannot replace them |
| Log Analytics/Application Insights | 1/1 | 30 days | 30 days initially | correlated Web/Worker telemetry |
| Document Intelligence | 0 or 1 | disabled until benchmark/old F0 decision | S0 when required | OCR only for scanned/insufficient PDFs |

## Identity and secret design

- Web uses a system-assigned identity.
- Worker uses a user-assigned identity because Flex deployment storage needs an identity before the Function App exists.
- Storage roles are scoped to the new storage account. Shared-key access is disabled.
- Azure SQL uses a Microsoft Entra administrator and Entra-only authentication. Runtime database users receive `db_datareader` and `db_datawriter` in the post-provision script; no SQL administrator password exists in source or parameters.
- Private networking is a `Never` boundary. The scaffold therefore uses public
  service endpoints and the Azure SQL `AllowAllWindowsAzureIps` firewall rule so
  App Service and Flex can reach SQL. Authentication remains Entra-only. This
  broad network reach is an accepted first-MVP trade-off, not a planned future
  private-networking migration.
- App settings contain resource names, endpoints, and Application Insights connection metadata. Third-party credentials are referenced from Infisical or Key Vault and are never generated into Bicep output.

## Quota and availability evidence

The Azure quota extension returned no usable rows for Storage, Web, SQL, or Cognitive Services in this subscription, so this plan uses the live inventory plus Microsoft service-limit documentation. The subscription currently has 10 storage accounts, one Azure SQL alternative is not yet deployed, multiple FC1 plans, and one existing Document Intelligence F0 resource. F0 availability is therefore a known deployment decision: reuse the old resource temporarily, retire/recreate it after cutover, or accept S0 cost.

Immediately before provisioning, recheck:

- F1 App Service plan availability in UK South;
- Flex Consumption and regional app quota;
- SQL logical-server quota;
- storage-account quota;
- Document Intelligence SKU availability;
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
   PROVIDER`; then preview and provision only the approved new v2 target, never
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
- The predecessor is pre-release and its test application data is not migrated into v2. Retirement remains a separately approved operation, not a deployment prerequisite.
- Document Intelligence F0 ownership/reuse has not been decided.
- SQL Entra administrator name/object ID must be confirmed at deployment time.
- GitHub Actions/OIDC deployment is a `Never` boundary, not a missing scaffold item.
- The direct-terminal packaging, migration, identity, Entra-resolution and
  remote-build-removal work described above is not implemented.
- External integration credentials and rotation sequence are not prepared.

This file must not be changed to `Ready for Validation` merely because Bicep compiles.
