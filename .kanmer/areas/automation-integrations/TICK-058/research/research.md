# Research — TICK-058: principal-scoped provider submission API

## Question

How can a provider submit one principal's instructions through the existing durable intake path without adding a second policy implementation or a provider-facing Processing feature?

## Findings

- FRD-09 fixes the security boundary as the stable Pegasus Principal and requires the provider caller to use the same Core intake and authorization policies as Web and Worker; email domains and external tenants are not identities (`docs/frd/frd-09-provider-and-intermediary-routes.md`).
- ADR-0004 selects separately issued principal-scoped client IDs and opaque secrets and limits the provider surface to idempotent submission and retrieval of its own result (`docs/adr/0004-provider-api-and-staff-mcp-authentication.md`).
- `IIntakeSubmission`/`ReceiveIntake` already retain the source, create the staged receipt and durable work item, and return `ReceivedIntake(StagedReceiptId, IsDuplicate)`; this is the Core owner to reuse (`src/Pegasus.Core/Intake/DurableIntake.cs`).
- `SubmitGroupedIntake` already validates bounded contiguous files, derives child idempotency tokens, and returns staged receipt identifiers; the provider adapter can translate its multipart request into this contract rather than duplicate batching (`src/Pegasus.Core/Intake/GroupedIntake.cs`).
- Processing is intentionally asynchronous: Web persists the submission, Worker dispatches durable work, and the queue-trigger calls `ProcessQueuedIntake` (`src/Pegasus.Web/Pages/Upload.cshtml.cs`, `src/Pegasus.Worker/IntakeFunctions.cs`).
- The local Worker example dispatches pending work every 15 seconds. This can add 0–15 seconds before processing and is separately investigated by [[AUTO-008]]; it does not justify exposing the internal work-state vocabulary.
- The operator retired [[TICK-059]]: API-01 must return the durable receipt immediately and must not wait for, or describe, transient processing.
- No provider endpoint, authentication handler, credential entity, or provider API composition currently exists (`docs/operations.md`; repository search on 2026-08-21).

## Implications

Add one thin provider HTTP adapter in Pegasus.Web. Authenticate the principal-scoped client, translate one bounded multipart submission into the existing grouped intake command, stamp the provider client as actor, and return the opaque staged receipt identifier. Use an idempotency key supplied by the caller; a replay returns the same receipt and a conflicting reuse fails closed. API-03 owns later terminal retrieval.

## Open questions

All product decisions needed for planning are resolved below; rollout to named live providers remains an activation gate.

## Azure architecture refresh — 2026-08-21

### Verified live facts
- Read-only Azure inspection found production in `rg-pegasus-prod`: the public Web is already an Azure Container App with external HTTPS-only ingress, one always-on replica, Azure SQL, separate transport/custody Storage accounts, a queue-triggered Function Worker, managed identities, Key Vault, Application Insights, and Log Analytics. No provider API configuration, custom domain, IP restriction, client certificate requirement, or API gateway is deployed.
- The existing transport Storage account has shared-key access disabled and public blob access disabled. The Web and Worker already use managed identity; provider credentials are application identities, not Azure/Entra identities.
- Microsoft documents Container Apps HTTP scaling as concurrency-based within configured replica limits. The live Web is currently fixed at one replica, so capacity must be measured before changing its maximum: https://learn.microsoft.com/azure/container-apps/scale-app
- Azure API Management can enforce per-key throttling and return 429/Retry-After, but it is a separate service and distributed rate limits are approximate: https://learn.microsoft.com/azure/api-management/rate-limit-by-key-policy

### Design consequence
Host the first real submission route in the existing Web Container App and reuse SQL/outbox, transport Storage Queue, Function Worker, custody storage, telemetry, and managed identities. Do not add API Management, Front Door/WAF, Service Bus, another Function, another store, or Entra app registrations without measured traffic/security requirements. Apply a small application-level per-credential limit at the real endpoint; consider APIM only when named providers, traffic, contract governance, or a WAF/gateway requirement justify the new deployment unit. The endpoint returns only durable acceptance and never outbound files.
