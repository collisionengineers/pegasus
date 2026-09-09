# Configuration reference

This file describes setting meanings. Operations owns observed deployed values; the runbook and existing release/wipe skills own operational procedures.

## Configuration and secrets

Configuration ownership is:

| Boundary | Owner |
| --- | --- |
| Web composition and named SQL Server connection | `src/Pegasus.Web/Program.cs` and environment configuration |
| Development profile and launch path | `src/Pegasus.Web/Properties/launchSettings.json` |
| Ignored local state | `artifacts/` |
| Target Azure parameters and topology | `infra/`, `azure.yaml`, and `.azure/deployment-plan.md` |
| Which mailboxes inbound Intake polls, and their exact tenant identities | The `ApprovedMailboxes` allowlist, edited on `/Administration/Mailboxes` ([ADR-0022](../adr/0022-approved-mailbox-identity-and-enablement-database-setting.md)). v1 poll and Sent claims use each persisted mailbox identity, folder, capability and generation; a global Graph coordinate is not the current mailbox authority. Historical deployment settings are recorded in operations. |

Tool availability does not authorize external action.

## Public upload links

`infra/modules/platform.bicep` is the production source for the
`DocumentRequests` environment settings. Its limits version and accepted limits
version must match. The configured public boundary is 100 MiB per file, 20
files and 200 MiB retained aggregate bytes per link; multipart framing remains
bounded separately by the Web host. The configured `AllowedMediaTypes` includes
the document and image formats in FRD-02 plus `video/mp4` and `video/quicktime`.
MP4/MOV support depends on that deployed allowlist as well as the application
validation; changing code alone does not enable public video uploads.

The optional Production Worker OCR adapter reads `DocumentIntelligence:Endpoint`
as an absolute HTTPS URI and reuses the credential selected by
`AzureIdentity:WorkerClientId`. It adds no API-key setting. An absent endpoint
leaves the provider and processor unregistered; an OCR work row then fails
closed through the existing external-work dispatcher. `DevelopmentOffline`
rejects the endpoint setting and composes no OCR provider. The durable OCR
store remains available to both hosts. Configuration, resource permission,
deployment and live provider proof require their separately approved targets;
local composition tests do not establish any of them.

The production Bicep module declares one `FormRecognizer` S0 account with a
custom subdomain, disabled local authentication and a resource-scoped
`Cognitive Services User` assignment for the existing Worker identity only
([ADR-0040](../adr/0040-qualified-document-intelligence-ocr.md)). It supplies the
Worker endpoint; Web receives neither the setting nor that role. The
`DOCUMENT_INTELLIGENCE_ACCOUNT_ID` and `DOCUMENT_INTELLIGENCE_ENDPOINT`
deployment outputs identify the exact account for release readback. Current
target, pricing and activation evidence belong in
[operations](../operations.md).

Use the existing authorized release and preview procedure. Read back the
account's SKU, custom subdomain, `disableLocalAuth`, scoped assignment and
Worker endpoint before the approved qualified-page canary. Retain the OCR
operation/API/model identity and result hash; replay the same operation to
prove retained-output recovery, not a new provider submission. Web denial
needs an actual identity-scoped check, not just an absent setting. If OCR
activation must be rolled back, remove the Worker endpoint through the
approved configuration route while preserving the account and retained
operation evidence. Keep local authentication disabled; never use keys as a
fallback. Infrastructure readback alone does not prove accepted extraction.

Use managed identity and scoped RBAC. General application secrets retain their approved vault owner. The scoped
[ADR-0043](../adr/0043-per-engineer-vendor-credential-protection.md) exception
protects per-Engineer vendor credentials and session material with Data
Protection in existing SQL, with the matching key-ring recovery contract. Never commit secret values, connection strings, readable passwords, generated credentials, or data not approved for public source control.
