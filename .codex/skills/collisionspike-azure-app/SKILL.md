---
name: collisionspike-azure-app
description: Plan, research, review, scaffold, validate, inventory, or migrate the CollisionSpike v2 Azure application. Use for App Service, Functions, Azure SQL, Storage, Key Vault, Application Insights, Document Intelligence, Bicep, azd, identity, cost, quota, deployment, or retirement work.
---

# CollisionSpike Azure application

This is a hybrid Microsoft skill: stable project decisions live here; current service details must be resolved from Microsoft Learn and Azure MCP at task time.

## Approved baseline

- Region: UK South.
- Workload: about eight office users and roughly 2,000 cases per month.
- Runtime: .NET 10 modular monolith; Linux App Service for Web/API and isolated Azure Functions on Flex Consumption for background work.
- Web plan: F1 development and B1 production. No deployment slot on these tiers.
- Data: Azure SQL Basic development and S0 production; applications use managed identity and Microsoft Entra authentication.
- Integration state: Storage LRS, Key Vault, Log Analytics and Application Insights, Document Intelligence as the scanned-PDF OCR path, Box long-term files, Graph/Outlook and EVA adapters.
- Delivery: Bicep, Azure Developer CLI, GitHub OIDC later.
- Explicit first-scaffold exclusions: Defender, private networking, deployment slots, custom domain, malware scanning, live deployment, and old-resource deletion.

Read [approved-architecture.md](references/approved-architecture.md) before proposing infrastructure.

## Workflow

1. Establish whether the task is read-only research, local scaffold, validation, deployment, or retirement. Never infer deployment authority from a scaffold request.
2. Confirm subscription, tenant, environment, region, and live inventory. Do not retrieve secret values.
3. Query current guidance with Microsoft Learn MCP and Azure MCP best-practice tools. Use [current-guidance.md](references/current-guidance.md).
4. Start from accepted ADRs and `.azure/deployment-plan.md`. Keep live facts timestamped and separate.
5. Prefer managed identity and RBAC and Entra-only Azure SQL. Disable shared keys where identity-based operation is supported.
6. Produce or review Bicep and azd locally. Compile and lint; do not call `azd provision`, `az deployment`, create/update/delete commands, or credential rotation without explicit permission.
7. For migration, create new resources in a separate group, reconcile data, prove traffic, preserve rollback, and retire leaves before shared or data-bearing assets.
8. Immediately before a destructive step, refresh callers, traffic, data counts, dependencies, locks, and exact targets. Obtain explicit authorization.

## Microsoft research behavior

Use `microsoft_docs_search` for concepts and limits, `microsoft_docs_fetch` for authoritative pages, and `microsoft_code_sample_search` for current SDK or configuration examples. If MCP is unavailable, use official Microsoft Learn or Azure GitHub sources and note the fallback. Never freeze preview syntax or current SKU limits without a source date.

## Project-specific safety

- The old estate contains PostgreSQL data, evidence blobs, Durable/queue state, Foundry deployments, a shared ACR repository, capture UI, and shared telemetry. Never begin with resource-group deletion.
- No secret value belongs in an inventory or plan. Record secret names only when necessary.
- F1 has quotas and no slot; B1 is dedicated but does not make deployment automatically zero-risk. Design health checks and reversible deployment separately.
- Cost estimates are estimates. Query current Azure pricing or cost data before a spending recommendation.
