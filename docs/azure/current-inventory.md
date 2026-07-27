# Current Azure inventory

Live read-only snapshot taken 2026-07-23. No resources, settings, roles, deployments, secrets, keys, or data were changed. Secret **names** were inspected where necessary; values were not retrieved.

## Scope

| Item | Value |
|---|---|
| Subscription | Azure subscription 1 |
| Subscription ID | `e6076573-23a5-46a8-acef-7e22d264e5db` |
| Tenant ID | `858cf5b3-aa0a-47a6-9b40-4851fd0afa94` |
| Subscription state | Enabled |
| Visible subscriptions | 1 |
| Resource groups | 5 |
| ARM resources | 56 |
| Primary old-app estate | 53 resources across `rg-collisionspike-dev` and its generated OCR child group |

Neither Pegasus resource group has a resource lock. That is an inventory fact, not deletion approval.

## Resource groups

| Resource group | Region | Count | Classification |
|---|---|---:|---|
| `rg-collisionspike-dev` | UK South | 52 | old application; tags include `app=collisionspike`, `environment=dev`, `managedBy=claude-deploy` |
| `cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117` | UK South | 1 | Azure-managed child group for OCR Function/Container App |
| `DefaultResourceGroup-SUK` | UK South | 1 | shared/default Log Analytics workspace used by API/orchestrator telemetry |
| `VisualStudioOnline-24D3DE18145149ECA713A2C21F0A74B1` | UK South | 1 | Visual Studio account; no Pegasus ownership established |
| `VisualStudioOnline-C54F94A5C4C841719773D424E581EAE4` | UK South | 1 | Visual Studio account; no Pegasus ownership established |

## Complete resource list

The duplicate names `cespk-api-dev`, `cespk-orch-dev`, and `cespkocr-fn-dev-glju3v` represent different ARM resource types, not duplicate rows.

### Application compute and UI (21)

| Name | ARM type | Region/SKU | Live state and ownership |
|---|---|---|---|
| `cespk-api-dev` | `Microsoft.Web/sites` Function App | UK South / Flex Consumption | Running; central data API |
| `ASP-rgcollisionspikedev-007e` | `Microsoft.Web/serverfarms` | UK South / FC1 | Ready; dedicated API plan |
| `cespk-orch-dev` | `Microsoft.Web/sites` Function App | UK South / Flex Consumption | Running; intake/orchestration |
| `ASP-rgcollisionspikedev-bc54` | `Microsoft.Web/serverfarms` | UK South / FC1 | Ready; dedicated orchestrator plan |
| `cespike-parser-dev-x7xt3d5ovhi7y` | `Microsoft.Web/sites` Function App | UK South / Flex Consumption | Running; PDF/parser utility |
| `cespike-parser-plan-dev` | `Microsoft.Web/serverfarms` | UK South / FC1 | Ready; parser plan |
| `cespkenrich-fn-gi62sd` | `Microsoft.Web/sites` Function App | UK South / Flex Consumption | Running; DVLA/DVSA enrichment |
| `cespkenrich-plan-gi62sd` | `Microsoft.Web/serverfarms` | UK South / FC1 | Ready; enrichment plan |
| `cespkeva-fn-ufa3ci` | `Microsoft.Web/sites` Function App | UK South / Flex Consumption | Running; EVA adapter |
| `cespkeva-plan-ufa3ci` | `Microsoft.Web/serverfarms` | UK South / FC1 | Ready; EVA plan |
| `cespkeval-fn-6c6fxd` | `Microsoft.Web/sites` Function App | UK South / Flex Consumption | Running; evaluation function |
| `cespkeval-plan-6c6fxd` | `Microsoft.Web/serverfarms` | UK South / FC1 | Ready; evaluation plan |
| `cespkbox-fn-v76a47` | `Microsoft.Web/sites` Function App | UK South / Flex Consumption | Running; Box adapter/webhook |
| `cespkbox-plan-v76a47` | `Microsoft.Web/serverfarms` | UK South / FC1 | Ready; Box plan |
| `cespkloc-fn-a7tzj2` | `Microsoft.Web/sites` Function App | UK South / Flex Consumption | Running; location/AI helper |
| `cespkloc-plan-a7tzj2` | `Microsoft.Web/serverfarms` | UK South / FC1 | Ready; location plan |
| `cespkocr-fn-dev-glju3v` | `Microsoft.Web/sites` managed Function wrapper | UK South / ACA-hosted | Running; OCR wrapper with system and user-assigned identities |
| `cespkocr-env-dev` | `Microsoft.App/managedEnvironments` | UK South | Succeeded; public managed environment |
| `cespkocr-fn-dev-glju3v` | `Microsoft.App/containerApps` in generated group | UK South | Running; external ingress, port 80, min 0/max 5, 1 CPU/2 GiB |
| `cespk-spa-dev` | `Microsoft.Web/staticSites` | West Europe / Free | Active old main UI; no custom domain |
| `cespk-capture-spa-dev` | `Microsoft.Web/staticSites` | West Europe / Standard | Active capture UI; no custom domain; ownership decision required |

OCR container details: revision `cespkocr-fn-dev-glju3v--0000006`; image is pinned from `cespkocracraeee76.azurecr.io/ce-ocr` by digest. The Azure-managed child group must not be deleted independently before the parent lifecycle is understood.

### Storage and database (11)

| Name | ARM type/SKU | Known use or data |
|---|---|---|
| `cespikestx7xt3d` | StorageV2 Standard LRS | parser Function package and host state |
| `cespkapistdev01` | StorageV2 Standard LRS | API package and host state |
| `cespkboxstv76a47` | StorageV2 Standard LRS | Box Function package and host state |
| `cespkenrichstgi62sd` | StorageV2 Standard LRS | enrichment package and host state |
| `cespkevalst6c6fxd` | StorageV2 Standard LRS | evaluation package and host state |
| `cespkevastufa3ci` | StorageV2 Standard LRS | EVA package and host state |
| `cespkevidstdev01` | StorageV2 Standard LRS | **data-bearing:** `evidence` container has 4 blobs, 17,927,237 bytes |
| `cespklocsta7tzj2` | StorageV2 Standard LRS | location Function package and host state |
| `cespkocrstglju3v` | StorageV2 Standard LRS | OCR host state |
| `cespkorchstdev01` | StorageV2 Standard LRS | **data/transient work:** Durable state, queues, large messages, intake work |
| `cespk-pg-dev` | PostgreSQL Flexible Server 16 / `Standard_B1ms`, 32 GiB | **data-bearing:** custom `collisionspike` database; server Ready |

All ten storage accounts enforce HTTPS and TLS 1.2 and disallow anonymous blob access. Shared-key access is disabled except on `cespkevidstdev01`. Network default action is Allow.

`cespkorchstdev01` queues:

- `cespkorchdev-control-00` through `cespkorchdev-control-03`
- `cespkorchdev-workitems`
- `eva-shadow-submit`
- `evidence-backfill`
- `intake-messages` and `intake-messages-poison`
- `outlook-move`
- `sent-messages` and `sent-messages-poison`

Its Durable tables are `cespkorchdevHistory`, `cespkorchdevInstances`, and `cespkorchdevPartitions`. Relevant containers include Function package/host/secret containers, app lease, and large-message storage.

PostgreSQL details:

- databases: `collisionspike`, `postgres`, `azure_sys`, `azure_maintenance`;
- backup retention 7 days; geo-redundant backup disabled; HA disabled;
- public network enabled;
- two named developer public-IP rules plus `AllowAzureServices` (`0.0.0.0`).

The PostgreSQL server and evidence/orchestrator storage are hard resource-group deletion blockers.

### Key Vaults (5)

| Name | Secret-name categories observed | State |
|---|---|---|
| `cespk-pg-kv-dev` | Graph, PostgreSQL, Maps/Vision, capture, parser/OCR/enrichment/location/Box Function credentials | in active dependency graph |
| `cespkboxkvv76a47` | Box client/config/webhook credentials | Box dependency |
| `cespkenrichkvgi62sd` | DVLA/DVSA credentials | enrichment dependency |
| `cespkevakvufa3ci` | no current secret names | retirement candidate after version/reference check |
| `cespklockva7tzj2` | Maps and Vision credentials | location dependency |

All use RBAC authorization, soft delete, and public network access. Only `cespklockva7tzj2` has purge protection enabled. Rotate or revoke reusable third-party credentials after cutover rather than copying them indefinitely.

### AI, document, maps, and registry (7)

| Name | ARM type/SKU | Contents and coupling |
|---|---|---|
| `cespkdocintel-dev` | Cognitive Services `FormRecognizer` F0 | Document Intelligence; OCR host has related settings and managed-identity role |
| `cespkvision-dev` | Computer Vision F0 | location helper dependency |
| `cespkmaps-dev` | Azure Maps G2, North Europe | API/location helper dependency |
| `digital-3339-resource` | Azure AI Services / Foundry S0 | API, orchestrator, and location identities have OpenAI User roles |
| `digital-3339-resource/digital-3339` | Foundry project | old runtime/evaluation ownership unresolved |
| `cespkocracraeee76` | Azure Container Registry Basic | contains `ce-ocr` and unrelated/potentially shared `valuationbot-mcp` tags `v3.0.0`-`v3.0.3` |
| `cespkocr-acrpull-id` | user-assigned managed identity | intended OCR image-pull identity |

Foundry deployments, all reporting Succeeded:

- `gpt-5`
- `text-embedding-3-large`
- `eval-gpt-5-nano`, `eval-gpt-54-nano`
- `eval-gpt-5-mini`, `eval-gpt-54-mini`
- `eval-phi-4-mini`
- `eval-deepseek-v4`
- `eval-llama-4-mav`
- `eval-llama-33-70b`
- `eval-cohere-command-a`

These deployments are usage-priced children of the account. The Foundry account may be useful for later local-evaluation experiments, and the ACR contains a ValuationBot repository. Neither parent is safe to delete as generic old-app cleanup.

### Observability (10)

| Name | ARM type/configuration | Linkage |
|---|---|---|
| `cespike-parser-ai-dev` | Application Insights, 90 days | `cespike-parser-law-dev` |
| `cespkocr-ai-dev` | Application Insights, 90 days | `cespkocr-law-dev` |
| `cespk-api-dev` | Application Insights, 90 days | shared default workspace |
| `cespk-orch-dev` | Application Insights, 90 days | shared default workspace |
| `digital-3339-resource-appinsights` | Application Insights, 90 days | `digital-3339-resource-logs` |
| `cespike-parser-law-dev` | Log Analytics PerGB2018, 30 days, no daily cap | parser telemetry |
| `cespkocr-law-dev` | Log Analytics PerGB2018, 30 days, no daily cap | OCR telemetry |
| `digital-3339-resource-logs` | Log Analytics PerGB2018, 30 days, no daily cap | Foundry telemetry |
| `DefaultWorkspace-e6076573-23a5-46a8-acef-7e22d264e5db-SUK` | Log Analytics PerGB2018, 30 days, no daily cap | shared/default; API/orchestrator coupled |
| `Application Insights Smart Detection` | action group with two ARM-role receivers | linked monitoring notification path |

All inspected Insights/workspaces allow public ingestion and query access. Deployment history includes multiple failed `Failure-Anomalies-Alert-Rule-Deployment-*` operations, so monitoring configuration is not a known-good baseline.

### Other subscription resources (2)

| Resource group | Name | Type | Decision |
|---|---|---|---|
| `VisualStudioOnline-24D3DE18145149ECA713A2C21F0A74B1` | `digital0320` | Microsoft Visual Studio account | retain; no application coupling shown |
| `VisualStudioOnline-C54F94A5C4C841719773D424E581EAE4` | `ce2026` | Microsoft Visual Studio account | retain; no application coupling shown |

## Confirmed dependency graph

- API identity: Key Vault Secrets User on `cespk-pg-kv-dev`; Blob Data Owner on `cespkapistdev01`; Blob Data Contributor on `cespkevidstdev01`; Queue Message Sender on `cespkorchstdev01`; OpenAI User on `digital-3339-resource`.
- Orchestrator identity: Key Vault Secrets User on `cespk-pg-kv-dev`; Blob/Queue/Table data roles on `cespkorchstdev01`; Blob Data Contributor on `cespkevidstdev01`; OpenAI User on `digital-3339-resource`.
- OCR identity: Cognitive Services User on `cespkdocintel-dev`; image pulled from `cespkocracraeee76`.
- Location identity: OpenAI User on `digital-3339-resource`.
- Box identity: Blob Data Reader on `cespkevidstdev01`.
- ACR has an `AcrPull` assignment to a service principal that does not match the reported user-assigned identity principal. Treat it as possibly stale/orphaned and verify before removal.
- API/orchestrator setting **names** show dependencies on PostgreSQL, evidence storage, Box, EVA, Microsoft Graph, parser/OCR/enrichment/location, Maps/Vision, and Foundry.
- No slot-specific app settings were found.
- Eight conventional Function Apps have public network access, HTTPS-only, and `clientCertMode=Required`. The ACA-hosted OCR wrapper reports networking separately.

## Evidence method and limits

The snapshot used Azure MCP subscription/group/resource listing plus read-only Azure CLI/Resource Graph queries for Functions, settings names, identities, roles, storage containers/queues/tables/blob aggregates, PostgreSQL metadata, Key Vault secret names, Cognitive deployments, Maps, Static Web Apps, App Insights, Log Analytics, ACR, Container Apps, locks, and deployment history.

No function keys, storage keys, connection strings, database passwords, model keys, secret values, full blob content, queue-message content, or PostgreSQL rows were retrieved. Live state can change after this timestamp; refresh immediately before cutover or retirement.
