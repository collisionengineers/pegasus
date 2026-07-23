# Approved Azure architecture

## Runtime and ownership

| Resource | Purpose | Owner |
|---|---|---|
| Linux App Service | Razor Pages, API, application-managed user sign-in | Web composition root |
| Functions Flex Consumption | mailbox polling and queued background orchestration | Worker composition root |
| Azure SQL | application and Identity data | Infrastructure adapter |
| Storage | Function host state, queues, temporary intake artifacts | Infrastructure/Worker adapters |
| Key Vault | credentials that managed identity cannot replace | platform boundary |
| Log Analytics and Application Insights | correlated Web/Worker telemetry and health | composition roots |
| Document Intelligence | OCR for scanned PDFs after local embedded-text attempt | document extraction adapter |
| Box | long-term case files and file requests | Box adapter |

Core owns case, reference, workflow, and extraction decisions. Azure services and external systems are adapters; they do not become alternate domain engines.

## Environments

Use separate development and production resource groups with deterministic environment-suffixed names. Production is not a deployment slot. F1 development and B1 production are separate App Service plans. Keep deployment authorization outside source control and use GitHub OIDC when CI/CD is introduced.

## Identity

Use system-assigned managed identities for Web and Worker unless a documented lifecycle requires a user-assigned identity. Assign least-privilege data-plane roles at the narrowest resource. Configure an Entra administrator for the logical SQL server and grant database roles in a post-provision step; do not declare SQL passwords in Bicep.
