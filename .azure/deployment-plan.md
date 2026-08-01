# Azure deployment plan

Status: **Production-route implementation is active under issue #311. An
approved preview resolved every external input and reached ARM preflight, which
stopped on UK South App Service `Total VMs` quota `0` before any change.
Provisioning, deployment, and retirement have not run and
retain the exact-target gates in the
[production replacement runbook](../azure-production-replacement-plan.md).**

Last reviewed: 2026-08-01, Europe/London.

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

## Proposed production resources

Pegasus has no Azure development, test, integration, or staging environment.
Local development is isolated; this table describes the one production target
only, as decided in [ADR-0014](../docs/adr/0014-local-to-production-deployment.md).

| Resource | Quantity | Production | Reason |
|---|---:|---|---|
| Resource group | 1 | separate `prod` | production lifecycle |
| Linux App Service plan | 1 | B1 | selected low-cost tier |
| Web App | 1 | .NET 10 | Razor Pages/API and app-managed user accounts |
| Functions plan | 1 | Flex Consumption FC1 | .NET 10 isolated background work |
| Function App | 1 | .NET 10 isolated | mailbox/queue composition root |
| User-assigned identities | 2 | Web and Worker | stable least-privilege identities; Worker identity also owns Flex host/package lifecycle |
| Azure SQL logical server/database | 1/1 | S0 | application plus ASP.NET Core Identity data |
| Functions transport/deployment storage | 1 | Standard LRS | host internals, deployment package, ID-only work/poison queues |
| Application custody/protection storage | 1 | Standard LRS | transient intake, Web authentication ring, Box-link ring; Worker is denied the authentication ring |
| Key Vault | 1 | Standard | third-party credentials only where managed identity cannot replace them |
| Log Analytics/Application Insights | 1/1 | 31 days, adaptive sampling, 0.1 GB/day cap | correlated Web/Worker telemetry with local authentication disabled |

## Identity and secret design

- Web and Worker use distinct user-assigned identities.
- The Worker identity exists before the Function App so Flex deployment and host storage can use identity-based access.
- Storage roles are separated between Functions transport/deployment and application custody/protection accounts. Shared-key access is disabled; roles are container/queue scoped except the Function host roles that Azure Functions requires at account scope.
- Azure SQL uses a Microsoft Entra administrator and Entra-only authentication. Migration `20260729176000_AzureSqlRuntimeLeastPrivilege` creates distinct fixed Web and Worker roles. Role-reconciliation migration `20260729199000_RuntimeRoleReconciliation` resets their direct DML across the complete application-table census, grants only the exhaustive caller-derived matrix, denies Worker `DELETE` everywhere, and denies Web `DELETE` except on its four required relationship/value workflows. Neither role receives DDL or broad built-in data roles. `scripts/Invoke-AzureDatabaseBootstrap.ps1` implements the separately gated managed-identity user/role operation and verifies the exhaustive migration-defined matrix; it has not run against Azure. A temporary migrator group owns schema changes and has no standing runtime use.
- Private networking is a `Not planned` boundary. The scaffold therefore uses public
  service endpoints and the Azure SQL `AllowAllWindowsAzureIps` firewall rule so
  App Service and Flex can reach SQL. Authentication remains Entra-only. This
  broad network reach is an accepted `0.1.0-alpha.1` trade-off, not a planned future
  private-networking migration.
- App settings contain resource names, endpoints, client IDs, and Application Insights connection metadata. Third-party credentials are referenced from Key Vault and are never generated into Bicep output. Box uses the retained JWT configuration plus separately retained client secret to obtain short-lived SDK tokens at runtime; no static Box access token is a deployment input.

## Quota and availability evidence

The 2026-07-23 live inventory is dated evidence only. It does not approve target resource reuse. Immediately before any separately authorised validation or provisioning, recheck service availability, quota, pricing, role-assignment authority, and the exact approved target names. Alpha includes no Document Intelligence/OCR resource.

Before provisioning, recheck:

- B1 App Service plan availability in UK South;
- Flex Consumption and regional app quota;
- SQL S0 availability;
- two production storage accounts;
- role-assignment authority for the provisioning principal.

## Production route implementation

Issue #311 owns implementation of the production-only infrastructure,
release-artifact builder, deployment-plan validator, migration operation,
interactive first-Administrator bootstrap, smoke route, archive, and exact-ID
retirement commands. Their presence will establish implementation only; the
local proof commands and artifact manifest establish local verification.

Every Azure action remains separately gated. The controlling runbook fixes the
source revision, immutable artifact provenance, migration sequence, identity
creation, disabled trigger state, validation, rollback, and recovery evidence.

## Azure activation gate

The activation route remains fail-closed unless
`deploymentMode=approved-live-deployment` is supplied. The concrete gate is the
runbook's exact subscription, resource group, principal, cost scope, data
boundary, and migration/deployment sequence, plus a fresh authorised-terminal
recheck of service availability, quota, pricing, role-assignment authority,
target names, SQL Entra administrator, and external credential readiness.

Issue #311 implements that explicit mode and removes remote build before any
preview. Applying the idempotent migration bundle remains an explicit
pre-application step; schema rollback is not a down-migration.
## Deployment blockers

- User approval to create chargeable Azure resources has not been given.
- The predecessor is pre-release and its test application data is not migrated into `0.1.0-alpha.1`. Retirement remains a separately approved operation, not a deployment prerequisite.
- SQL Entra administrator name/object ID must be confirmed at deployment time.
- GitHub Actions/OIDC deployment is a `Not planned` boundary, not a missing scaffold item.
- The release scripts, production adapters, migration operation, and
  first-Administrator bootstrap are implemented. Restore, Release build, 539
  non-corpus tests, Bicep compilation, and local deployment-plan validation
  passed on 2026-07-31. Revision-bound QDOS CI pressure (3/3, no skips) and
  immutable Web, Worker, and migration packaging also passed from a clean
  reviewed commit. These are local proof, not deployment or live acceptance.
- Versioned metadata for the six retained Box/DVLA/DVSA secret references is
  prepared without reading values. Graph denied the approved metadata query,
  and the entitlement-specific DVSA token URL is not yet supplied. These four
  missing inputs stop preview before ARM evaluation.

This file must not be changed to `Ready for Validation` merely because Bicep compiles.
