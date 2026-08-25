# Files — MAIL-013

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/MailboxIntake.cs` or one focused sibling under `Core/Intake/` | Add the targeted approved-mailbox wake and subscription-maintenance use cases while delegating actual mailbox work to the existing lease/delta/retention path. Risk: bypassing approval re-checks, per-mailbox isolation, or idempotency. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` or a focused `GraphMailboxChangeNotifications.cs` sibling | Add the Graph subscription create/renew/delete-if-required HTTP contract and bounded basic/lifecycle notification shapes without adding message resource data. Risk: incorrect resource URLs, expiry handling, immutable-ID preference, throttling or secret leakage. |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` | Add one subscription-state entity keyed to the durable approved-mailbox identity; do not store clientState. |
| `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs` | Enforce one current subscription per approved Inbox, bounded identifiers/resource data, expiry indexes and the approved-mailbox relationship. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` and the focused EF store | Compose subscription lookup, renewal claim/completion/failure and notification resolution against existing SQL. Risk: concurrent renewals or callbacks accepting stale/foreign subscriptions. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/*MailboxChangeNotification*.*` and `PegasusDbContextModelSnapshot.cs` | Establish the current schema and exact Web/Worker SQL permissions. Web needs only subscription lookup needed to validate/route callbacks; Worker owns lifecycle writes. |
| Shared queue adapter introduced by [[INTK-042]] | Reuse its Infrastructure-owned Azure Queue sender/receiver convention for stable mailbox-wake identifiers. Do not create another Worker-only or Web-only queue implementation. Resolve the exact path from origin/dev after [[INTK-042]] merges. |
| `src/Pegasus.Web/Program.cs` and a focused webhook endpoint file under `src/Pegasus.Web/` | Map `POST /hooks/microsoft-graph/mail` outside staff authentication/antiforgery, return the validation token exactly, validate bounded notification payload/clientState, enqueue and return promptly. Risk: exposing a general anonymous endpoint, disclosing failures, or performing Graph/processing work inline. |
| `src/Pegasus.Worker/MailboxFunctions.cs` | Add the mailbox-wake queue trigger and six-hour subscription-maintenance timer; change the existing Inbox timer to the five-minute fallback schedule only. |
| `src/Pegasus.Worker/WorkerDependencyInjection.cs` and `src/Pegasus.Infrastructure/DependencyInjection.cs` | Compose the shared queue, subscription client/store and targeted Core use cases in the correct host; keep the polling/processing services out of Web. |
| `infra/main.bicep`, `infra/main.parameters.json`, and `infra/modules/platform.bicep` | Add the versioned client-state secret input/reference, mailbox-wake queue and settings/schedules. Reuse Web queue-sender RBAC from [[INTK-042]]; keep Web at its existing one warm replica and do not add Functions always-ready capacity. |
| `tests/Pegasus.Core.Tests/Intake/*Mailbox*Tests.cs` | Prove targeted wake reuses the existing mailbox lease path, rejects disabled/unapproved identities, and coexists idempotently with fallback polling/duplicate wake delivery. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Prove exact Graph subscription resource, basic-notification body, expiry, renewal, lifecycle outcomes, throttling/retry and no resource-data request. |
| New focused Web integration test beside `MailWorkspaceWebTests.cs` | Prove token handshake content type/body, bounded parsing, constant-time clientState result, unknown/stale subscription rejection, queue payload shape and prompt 2xx/202 behavior without authentication redirects. |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | After [[INTK-040]] merges, add only the end-to-end wake/fallback concurrency and sender regression assertions that cannot live in narrower tests. Avoid its grouped-image sections. |
| `tests/Pegasus.ArchitectureTests/WorkerCompositionTests.cs`, `WorkerAzureClientCompositionTests.cs`, `WorkerActivationReleaseContractTests.cs`, and `DependencyDirectionTests.cs` | Prove host ownership, queue/secret/schedule deployment contract, no Web poller, no second business implementation, and unchanged warm Web topology. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/frd/frd-02-intake-and-source-identity.md` | Receipt is acknowledged only after durable source/work commit; queue messages carry identifiers, and Worker remains sole processing owner. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | ApprovedMailbox.Id, per-mailbox lease/cursor, fresh-start enablement and delta recovery are binding behavior. |
| `docs/adr/0002-dotnet-modular-monolith-on-azure.md` | Records the old polling choice and expressly permits later webhooks when measured latency proves polling unsuitable; [[INTK-041]] owns the superseding ADR. |
| `docs/adr/0022-approved-mailbox-identity-and-enablement-database-setting.md` and `0024-stable-approved-mailbox-identity-and-explicit-baseline.md` | The database allowlist is authority; Graph coordinates are scope, not durable source identity, and re-enablement must not ingest a backlog. |
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | Existing per-estate validation, approval re-check, lease claim/release, poison handling, retention ordering and cursor advancement must remain the single route. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Existing token credential, Graph HTTP conventions, immutable-ID header, delta cursor parsing and reset behavior to reuse. |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedInboxPollStore.cs` | Existing SQL concurrency pattern and per-mailbox due/lease state; notification work must not create a competing cursor owner. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | MAIL-009's provisional/effective sender rule already prevents the forwarding desk from appearing while route processing is incomplete. |
| `src/Pegasus.Worker/AzureQueueIntakeWorkQueue.cs` | Current stable-ID queue encoding and Azure SDK behavior; [[INTK-042]] will relocate/share this convention before this ticket implements. |
| `src/Pegasus.Web/Program.cs` | Authentication, anonymous endpoint and production forwarded-header ordering; the callback must be intentionally anonymous yet narrower than a staff/API route. |
| `infra/modules/platform.bicep` | Web is already warm and externally reachable; only Worker currently has Queue Data Contributor and current Graph mailbox coordinates exist only on Worker. |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | Existing restart, duplicate delta, poison, source-identity and forwarded-sender fixtures to reuse; [[INTK-040]] concurrently owns other sections. |
| `docs/runbook.md` | Graph/Exchange reads or subscription writes remain live-operation gated; local tests use mocks and must not mutate Outlook. |
| EPIC-006 `context.md` | Mailbox writes require explicit approval and all callers must reuse one canonical Core policy implementation. |

## Ripple effects

The database migration changes schema and principal grants. The Web identity gains the narrow queue-send/configuration capability delivered through [[INTK-042]] and this ticket's Key Vault reference; the Worker identity remains the only Graph subscription and delta-processing caller. Deployment validation must confirm the public notification URL, Graph application permission/RBAC scope, subscription identity and negative mailbox scope without exposing clientState. App Insights spans/metrics should correlate callback, queued wake, lease claim, delta read and intake receipt using non-secret identifiers; the governing observability contract comes from [[INTK-041]].

Current-state documentation (`docs/current-architecture.md`, `docs/operations.md`, and the Graph runbook section) changes only in the later approved deployment/release ticket [[DELIV-021]], when live state can be proved. Generated migrations/snapshots are committed here; no separate store, runtime or deployment unit is introduced.

## Out of scope

No inline message processing in Web; no Graph resource data; no payload or MIME in the wake queue; no mailbox write or local-alpha Outlook mutation; no Sent-mail subscription; no generic event bus; no Functions always-ready instance; no Web scale change; no sender-resolution rewrite; no change to classification, extraction, allocation, Image Intake or case creation; no replay of historical mail; no deployment, subscription creation or other cloud write in this ticket. [[INTK-040]]'s mailbox-image behavior and [[INTK-042]]'s general immediate outbox publication remain in their owning tickets.
