# Files — MAIL-013

## Implementation files

| Path | Planned change and reuse |
|---|---|
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | Add a targeted approved-mailbox entry point that reuses the existing validation, approval re-check, lease, delta, retention, cursor and recoverable-failure code. Keep `ExecuteAsync` as the estate fallback; do not duplicate `PollOneAsync`. |
| One focused Core sibling under `src/Pegasus.Core/Intake/` | Define the subscription-maintenance/lifecycle use case and the small storage/Graph/queue ports only where the external boundaries require them. No generic notification framework. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` or a focused `GraphMailboxChangeNotifications.cs` sibling | Reuse the existing credential, Graph base URI, HTTP/error and immutable-ID conventions for create, PATCH renew/reauthorize and recreate. Request basic `created` notifications only. |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` | Add one subscription entity keyed one-to-one to `ApprovedMailbox.Id`; never persist clientState. |
| `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs` | Enforce the one-row relationship, unique Graph subscription id, bounded resource/scope values and useful expiry/maintenance indexes. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` and new focused `EfMailboxChangeSubscriptionStore.cs` | Add active-subscription lookup for Web and maintenance/lifecycle writes for Worker using existing EF concurrency conventions. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/*MailboxChangeNotification*.*` and `PegasusDbContextModelSnapshot.cs` | Add the current schema and least-privilege grants: Web SELECT needed for validation/routing; Worker SELECT/INSERT/UPDATE/DELETE needed for lifecycle ownership. |
| `src/Pegasus.Infrastructure/Transport/AzureQueueWorkEnqueuers.cs` | Extend the shared INTK-042 transport convention with a mailbox-wake sender carrying only canonical mailbox/subscription identifiers. |
| New focused endpoint file under `src/Pegasus.Web/` plus `src/Pegasus.Web/Program.cs` | Map the intentionally anonymous callback, exact validation response, bounded batch parser, constant-time clientState check, active scope lookup, identifier-only queue send and uniform prompt responses. Web must not compose Graph mail reading or mailbox polling. |
| `src/Pegasus.Worker/MailboxFunctions.cs` | Add the mailbox-wake queue trigger and subscription-maintenance timer; retain `InboxPollFunction` only as the five-minute recovery caller. Add an explicit mailbox-wake poison caller using the existing poison convention. |
| `src/Pegasus.Worker/WorkerDependencyInjection.cs` and `src/Pegasus.Infrastructure/DependencyInjection.cs` | Compose the store, Graph subscription adapter, shared queue sender and targeted Core use cases in the correct host. |
| `src/Pegasus.Web/appsettings.Development.json` and `src/Pegasus.Worker/local.settings.example.json` | Add non-secret local/test defaults and schedule/queue names without putting a real clientState value in source. |
| `infra/main.bicep`, `infra/main.parameters.json`, `infra/modules/platform.bicep` | Add `mailbox-wake` queue/configuration, Key Vault-backed clientState reference, Web queue send, Worker queue consume, function census and exact six-hour/five-minute schedules. Preserve Web 1/1 replicas and Worker `alwaysReady: null`. |
| `scripts/Test-AzureDeploymentPlan.ps1`, `scripts/Invoke-ProductionSmoke.ps1` | Extend existing release checks for endpoint/config/function/queue/secret-reference presence without printing the secret or treating technical smoke as operator latency proof. |

## Test files

| Path | Evidence |
|---|---|
| `tests/Pegasus.Core.Tests/Intake/PollApprovedInboxTests.cs` | Targeted wake reuses the existing single-mailbox lease path; disabled/unapproved/leased mailboxes fail closed; duplicate/fallback overlap is idempotent. |
| New focused Core subscription tests | Create/renew/recreate decisions, 48-hour margin, lifecycle mapping, one PATCH for renew+reauthorize and no competing processor. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Exact `created` resource, basic notification shape, expiry, PATCH/recreate, lifecycle and throttling/error behaviour. |
| New focused Web integration tests beside `MailWorkspaceWebTests.cs` | URL-decoded token with 200/text/plain; bounded batches; valid/invalid clientState; unknown/expired/wrong tenant/resource rejection; 202 only after queue publication; 5xx on publication failure; no auth redirect or secret disclosure. |
| `tests/Pegasus.IntegrationTests/ApprovedMailboxEstateIntegrationTests.cs` | One subscription per enabled approved Inbox, SQL concurrency, disable/re-enable scope and Web/Worker grant boundaries. |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | Only the end-to-end queue wake/delta/fallback/sender assertions that narrower tests cannot prove. Reuse the existing mailbox and MAIL-009 fixtures. |
| `tests/Pegasus.ArchitectureTests/WorkerCompositionTests.cs`, `WorkerAzureClientCompositionTests.cs`, `WorkerActivationReleaseContractTests.cs`, `DependencyDirectionTests.cs` | Worker-only Graph/cursor ownership, shared Infrastructure queue transport, new trigger/poison/maintenance census, unchanged Web warmth and no always-ready Functions. |

## Governing and reference files

- `docs/frd/frd-08-email-mailbox-and-background-processing.md` — binding callback, subscription, lifecycle, recovery and sender behaviour.
- `docs/frd/frd-02-intake-and-source-identity.md` — identifier-only queue, durable receipt and Worker processing ownership.
- `docs/adr/0032-near-real-time-durable-intake-triggering.md` — accepted Graph wake plus slow recovery architecture and scale-to-zero rule.
- `docs/adr/0024-stable-approved-mailbox-identity-and-explicit-baseline.md` — durable mailbox identity and replaceable Graph scope.
- `src/Pegasus.Infrastructure/Transport/AzureQueueWorkEnqueuers.cs` — current shared Web/Worker queue convention delivered by INTK-042.
- `src/Pegasus.Infrastructure/Persistence/EfApprovedInboxPollStore.cs` — current SQL claim/lease/cursor concurrency pattern.
- `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` — MAIL-009 neutral/effective sender policy to preserve.
- `docs/runbook.md` — exact approval boundary for any later Graph subscription or Azure deployment write.

## Deliberately out of scope

No product-code implementation during planning; no cloud/mailbox writes; no resource data; no inline Graph/delta/intake work in Web; no new host, Event Grid, Event Hubs, generic event bus, cache or always-ready Function; no Sent Items subscription; no historical replay; no sender, classification, extraction, allocation, Image Intake or case-creation rewrite. Deployment/current-state documentation and live latency/cost proof remain DELIV-021 work after implementation merges.

## Research addendum — stable mailbox identity ripple

The current-versus-proposed comparison found that ADR-0024's stable identity is not implemented. The next planning pass must expand or prerequisite the following files before a Graph subscription can safely queue `ApprovedMailbox.Id`:

| Path | Additional reason/risk |
|---|---|
| `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` | `ApprovedIntakeMailbox` currently drops the internal `Guid`; it needs one coherent stable identity plus provider coordinates. |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs` | Project the internal ID and provide an exact approved-mailbox lookup for targeted wakes without enumerating the estate. |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedInboxPollStore.cs` | Replace Graph-identity re-key/adoption with stable-ID state, versioned cursor scope and the accepted per-mailbox activation boundary. The current cursor-carrying adoption path is explicitly unsafe. |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs`, `MailboxModelConfiguration.cs`, `PegasusDbContext.cs` | Poll state, poison, retained message and subscription relations must use one stable approved-mailbox key and enforce scope/activation consistency. |
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | Receipt occurrence/token construction, leases and retained metadata must agree on the stable ID while Graph mailbox/folder values remain replaceable read coordinates. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` and receipt persistence | Preserve existing business evidence and uniqueness while removing the replaceable Graph identity as the source key; avoid a dual-read/dual-write compatibility path in this pre-release system. |
| Migration and mailbox integration tests | Establish the intended current schema and disposable pre-release transition allowed by ADR-0024/runbook authority; prove fresh-start filtering, scope mismatch, 410 handling and no duplicate receipt identity. |

No existing Kanmer ticket was found that owns this ADR-0024 implementation. This is a planning dependency, not optional cleanup.
