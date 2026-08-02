# Azure deployment plan

Status: **Pegasus `0.1.0-alpha.1` is deployed to the sole Azure production
environment in `rg-pegasus-prod` using Azure Container Apps Consumption, Flex
Consumption, and a separate production Basic ACR. The predecessor test estate
has been retired through the exact manifest; only its two adopted Key Vaults
remain in `rg-collisionspike-dev`. The isolated recovery exercise remains a
mandatory gate before a second production release. The exact executed
evidence and hashes are in the retired runbook (git history,
`azure-production-replacement-plan.md`).**

Last reviewed: 2026-08-02, Europe/London.

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

## Deployed production resources

Pegasus has no Azure development, test, integration, or staging environment.
Local development is isolated; this table describes the one production target
only, as decided in [ADR-0014](../docs/adr/0014-local-to-production-deployment.md).

| Resource | Quantity | Production | Reason |
|---|---:|---|---|
| Resource group | 1 | separate `prod` | production lifecycle |
| Container Apps environment | 1 | Consumption, UK South | serverless Web hosting with scale-to-zero |
| Web Container App | 1 | .NET 10, Linux/AMD64, 0.5 vCPU, 1 GiB, replicas 0–1 | Razor Pages/API and app-managed user accounts |
| Azure Container Registry | 1 | Basic, admin disabled | private custody of the digest-pinned Web OCI image |
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
- Azure SQL uses a Microsoft Entra administrator and Entra-only authentication. Migration `20260729176000_AzureSqlRuntimeLeastPrivilege` creates distinct fixed Web and Worker roles. Role-reconciliation migration `20260729199000_RuntimeRoleReconciliation` resets their direct DML across the complete application-table census, grants only the exhaustive caller-derived matrix, denies Worker `DELETE` everywhere, and denies Web `DELETE` except on its four required relationship/value workflows. Neither role receives DDL or broad built-in data roles. `scripts/Invoke-AzureDatabaseBootstrap.ps1` ran against production and verified the exhaustive migration-defined matrix. A temporary migrator group owns schema changes and has no standing runtime use.
- Private networking is a `Not planned` boundary. The scaffold therefore uses public
  service endpoints and the Azure SQL `AllowAllWindowsAzureIps` firewall rule so
  Container Apps and Flex can reach SQL. Authentication remains Entra-only. This
  broad network reach is an accepted `0.1.0-alpha.1` trade-off, not a planned future
  private-networking migration.
- App settings contain resource names, endpoints, client IDs, and Application Insights connection metadata. Third-party credentials are referenced from Key Vault and are never generated into Bicep output. Box uses the retained JWT configuration plus separately retained client secret to obtain short-lived SDK tokens at runtime; no static Box access token is a deployment input.

## Quota and availability evidence

The 2026-07-23 live inventory is dated evidence only. It does not approve target resource reuse. Immediately before any separately authorised validation or provisioning, recheck service availability, quota, pricing, role-assignment authority, and the exact approved target names. Alpha includes no Document Intelligence/OCR resource.

Before provisioning, recheck:

- Container Apps Consumption environment availability in UK South;
- Basic ACR name availability and managed-identity `AcrPull` support;
- Flex Consumption and regional app quota;
- SQL S0 availability;
- two production storage accounts;
- role-assignment authority for the provisioning principal.

## Executed production route

Issue #311 owns the production-only infrastructure, release-artifact builder,
deployment-plan validator, migration operation, interactive first-Administrator
bootstrap, smoke route, archive, and exact-ID retirement commands. The deployed
Web source revision is `94997dd036a48cde23fce0f960b159a2b4a921c0`; its active
image digest is
`sha256:da11059f89e42d74d93ea7ed732d6b7ed8faca7ceb106ecb68875e5d5d8eda75`.
The Web live/ready probes returned HTTP 200, the Administrator password-change
route was exercised, and the Worker is running with all nine functions enabled.

The executed route retained the runbook's source revision, immutable artifact
provenance, migration sequence, identity creation, trigger activation,
validation, and retirement boundaries. Deployment does not prove the still
outstanding recovery exercise.

## Azure activation and retirement evidence

The activation route was executed with
`deploymentMode=approved-live-deployment` against the exact subscription and
resource group. The fail-closed mode remains part of the reusable route.

Remote build was not used. The idempotent migration bundle was applied before
application activation; schema rollback is not a down-migration.

The final retirement archive manifest SHA-256 is
`D0A2D03A09D54142F3337B0A186131133DB9D8B19180048AB71544D88522808A`.
The executable retirement manifest SHA-256 is
`3CC3F1224239E9F30B687302EE813AB081950DC4B020AF84368BDD8AC2D40CBF`.
All eight resource batches completed, all 30 delete-classified role assignments
are absent, and all seven retain-classified assignments remain. The old group
contains exactly `cespkboxkvv76a47` and `cespkenrichkvgi62sd`; the managed OCR
child group is absent.

## Remaining release gates

- GitHub Actions/OIDC deployment is a `Not planned` boundary, not a missing
  production component.
- All six approved retained-vault references report `Resolved`. Graph Inbox and
  Sent Items processing is live-verified. Reference resolution is not evidence
  for every Box, DVLA, or DVSA outcome.
- The first production release may operate under the accepted recovery
  exception. Before any second production deployment, the isolated recovery
  exercise in the production replacement runbook must prove RPO at most 15
  minutes and RTO at most four hours.
- The B1 quota request `b9df19cc-54b2-4876-9c4c-1eb9ba99076a` and later P0v4
  aggregate-quota preview remain superseded historical evidence. Do not retry
  either fixed App Service route.
