# Files — TICK-069

## Where the change lands

This is the expected implementation surface after activation. Exact adapter filenames depend on the accepted provider contract; no placeholder files should be created before that decision.

| Path | Why |
|---|---|
| `docs/frd/frd-02-intake-and-source-identity.md` | Add the accepted WhatsApp-specific behaviour: channel ownership, occurrence/correlation identity, authentication, message/media grouping, acknowledgement, replay, coexistence, custody, failure, and acceptance evidence. This is the canonical behavioural owner. |
| `docs/capabilities.md` | Change EXT-15’s activation/boundary statement only when promotion conditions are accepted; it remains the schedule/registry, not the behavioural specification. |
| `docs/adr/<next-id>-*.md` and `docs/adr/README.md` | Only if activation chooses a durable technical mechanism that requires a decision (for example a public verified webhook boundary). Do not create an ADR for behaviour or merely to authorise the FRD. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | Extend the single Core-owned source/channel and provenance vocabulary only if the accepted contract requires it; keep occurrence identity, receipt, assets, evidence, and decisions transport-neutral. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Reuse `ReceiveIntake` and durable processing. Changes should be limited to accepted channel limits or semantics that cannot already be expressed, without creating a parallel ingestion engine. |
| `src/Pegasus.Infrastructure/<accepted-provider>/...` | New external-boundary adapter for the accepted provider’s client/webhook verification and payload-to-`IntakeSource` mapping. The concrete path cannot be named responsibly before provider selection. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register the accepted adapter behind Core ports in the applicable composition profile; keep it absent when activation/configuration is incomplete. |
| `src/Pegasus.Web/Program.cs` or `src/Pegasus.Worker/Program.cs` | Compose the real caller selected by the accepted direction (public webhook versus polling/client). Only one owning ingress path should be added. |
| `src/Pegasus.Web/<accepted webhook route>` or `src/Pegasus.Worker/<accepted polling function>` | Thin transport caller: authenticate/verify, bound and map the provider payload, call Core intake, and return provider-required acknowledgement. It must not own classification, association, custody, or retry policy. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`, intake entities/configuration, and `src/Pegasus.Infrastructure/Persistence/Migrations/` | Only if the accepted contract needs channel-specific durable metadata not already carried by source identity, receipt, evidence, assets, or work records. Avoid a second store. |
| `tests/Pegasus.Core.Tests/Intake/` | Contract tests for occurrence identity, replay/conflict, limits, grouping, provenance, fail-closed association, and coexistence semantics added to the existing owners. |
| `tests/Pegasus.IntegrationTests/` | Real-caller tests for authentication/signature rejection, acknowledgement-after-durable-commit, duplicate delivery, media mapping, queue processing, status/failure visibility, manual coexistence, custody, and recovery. |
| `docs/current-architecture.md` and `docs/operations.md` | Refresh the as-built and deployed/runtime facts after implementation/deployment. They must not claim activation before caller and live evidence exist. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `AGENTS.md` | Core owns business policy; duplicate implementations are a stop condition; new deployment boundaries need an accepted ADR; external writes need exact-target approval; current-state docs must follow deployment. |
| `docs/operator-notes.md#authoritative-channels-and-formats` | WhatsApp may carry PDF, DOC/DOCX, or typed text; this is protected operator truth. |
| `docs/operator-notes.md#external-systems` | WhatsApp receives images and instructions today, unmatched images use network-drive staging, and no automated ingestion or transfer is yet proven. |
| `docs/operator-notes.md#storage-and-staging-interpretation` | Receipt/staging, Excel holding state, and Box custody are different layers; receipt must not imply association or custody. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Governing requirements for receipt identity, dispatch, provenance, failure, Unidentified, association, grouped images, and pre-case gates. |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | Manual WhatsApp evidence is already supported and does not activate an integration; source formats and custody remain bounded. |
| `docs/capabilities.md` | EXT-15 is Later / 0.5.0 allocation only; EXT-14 is the accepted manual WhatsApp capability. |
| `docs/boundaries.md` | Deferred seams retain stable identity/ports without dormant routes, credentials, flags, stores, or deployment units. |
| `docs/operations.md#deferred-capability-seams` | Names the WhatsApp activation evidence and deliberately absent client/webhook/queue. |
| `docs/current-architecture.md#integration-boundaries-and-deferred-seams` | Current callers and the rule that provider integrations reuse Core actions rather than copying policy into transport hosts. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | The existing single source-channel taxonomy and transport-neutral receipt/evidence/provenance model. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | The existing durable acknowledgement, artifact staging, identity/hash conflict, dispatch, processing, retry, and reconciliation owner. |
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | A mature precedent for mapping an external source into stable occurrence identity and retained metadata without moving business policy into the adapter. |
| `src/Pegasus.Worker/IntakeFunctions.cs` | Worker queue messages carry only stable staged-receipt identifiers and invoke Core processing; payloads do not travel on the queue. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Existing composition conventions and separation between document storage, mailbox adapters, and Core use cases. |
| `tests/Pegasus.Core.Tests/Intake/PollApprovedInboxTests.cs` | Existing source-adapter contract coverage for duplicate delivery, retained artifacts, cursor/receipt identity, and failure handling. |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | Existing end-to-end persistence and processing precedent for an automated inbound channel. |
| `tests/Pegasus.IntegrationTests/AutomationMailIngressTests.cs` | Existing transport ingress that delegates to the shared intake surface; useful for caller/auth/parity patterns, not WhatsApp policy. |

## Ripple effects

- Callers: the chosen webhook or polling host, composition configuration, health/readiness, and any provider acknowledgement surface.
- Persistence: stable provider/channel occurrence identity and any strictly necessary transport metadata; existing intake, work, asset, association, Unidentified, and custody records should remain the owners.
- Tests/fixtures: versioned provider payload and signature fixtures, replay/redelivery, media/grouping, throttling/outage, recovery, and manual-route coexistence. Fixtures must be representative and approved; do not fabricate domain messages or images.
- Operations: exact credentials/scopes, secret ownership, public callback or outbound access, sandbox and production targets, monitoring, rollout/rollback, and live evidence all require approval and must be reflected in current-state docs after deployment.
- Documentation: FRD behaviour first; a thin ADR only for a durable mechanism; capabilities only for allocation/activation; operations/current architecture only for observed state.
- Build/deployment: a public webhook may affect infrastructure and network exposure; a polling client may affect Worker scheduling and provider rate limits. The provider decision determines this ripple and must precede a plan.

## Out of scope

- Selecting a WhatsApp product/provider, account, number, or commercial plan during this research.
- Registering webhooks, creating credentials, contacting a provider, mutating a sandbox, Outlook, Box, Azure, or any external system.
- Implementing a client, endpoint, queue, feature flag, schema, UI placeholder, or dormant integration before activation.
- Replacing the accepted manual WhatsApp evidence path or network-drive coexistence without an explicit operator decision.
- Treating provider receipt as Case creation, association, Box custody, delivery, or acceptance.
- Broad redesign of intake, extraction, image recognition, Box custody, provider API, guided capture, or outbound WhatsApp messaging.
- Creating a new top-level project/store/runtime/deployment unit unless an accepted ADR proves the existing boundaries cannot carry the chosen mechanism.
