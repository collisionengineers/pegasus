# Azure deployment plan

Status: **Planning — local scaffold only. Not approved for validation deployment or provisioning.**

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
- Because private networking was explicitly deferred, the scaffold uses public service endpoints and the Azure SQL `AllowAllWindowsAzureIps` firewall rule so App Service and Flex can reach SQL. Authentication remains Entra-only. This broad network reach is a documented first-stage trade-off to revisit before any security-hardening phase.
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

## Local preparation and validation sequence

1. `pwsh ./scripts/Invoke-Doctor.ps1`
2. `pwsh ./scripts/Invoke-RepoCheck.ps1`
3. Review `docs/azure/current-inventory.md` and refresh it if the date has changed materially.
4. Run `azd env new dev` and populate only non-secret environment values when a validation deployment is explicitly approved.
5. Run `azd provision --preview` or the current supported preview/what-if path and review every resource/role change.
6. Provision a new v2 resource group; never deploy over `rg-collisionspike-dev`.
7. Run the SQL post-provision grant, application health tests, and a non-sensitive smoke path.
8. Do not connect genuine corpus data or live Outlook/Box/EVA until each integration cutover is approved.

## Deployment blockers

- User approval to create chargeable Azure resources has not been given.
- The predecessor is pre-release and its test application data is not migrated into v2. Retirement remains a separately approved operation, not a deployment prerequisite.
- Document Intelligence F0 ownership/reuse has not been decided.
- SQL Entra administrator name/object ID must be confirmed at deployment time.
- GitHub OIDC environment and release workflow are not created in this scaffold.
- External integration credentials and rotation sequence are not prepared.

This file must not be changed to `Ready for Validation` merely because Bicep compiles.
