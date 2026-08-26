# Files — MAIL-013

## Implementation files

| Path | Change and existing code reused |
|---|---|
| `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` | Carry stable `ApprovedMailbox.Id`, activation time and provider coordinates through the approved estate; keep one administration policy owner. |
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | Add targeted execution by stable mailbox ID and reuse the existing validation, lease, delta, retention, receipt, cursor and failure path. |
| `src/Pegasus.Core/Intake/MailboxChangeNotifications.cs` (new) | Hold only the subscription/wake records, ports and create-renew-recreate decisions required by Graph. |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyEntities.cs`, `AdministrationPolicyModelConfiguration.cs`, `EfApprovedMailboxStore.cs` | Persist activation time; project stable ID; add exact enabled-mailbox lookup. |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs`, `MailboxModelConfiguration.cs`, `PegasusDbContext.cs`, `EfApprovedInboxPollStore.cs`, `EfApprovedMailboxPollStatusQueries.cs`, `EfRetainedMailboxMessageStore.cs` | Re-key inbound operational state to the stable mailbox ID, add scope fingerprint/subscription state, and remove Graph-identity adoption. |
| `src/Pegasus.Infrastructure/Persistence/EfMailboxChangeSubscriptionStore.cs` (new) | Resolve active callbacks and claim due subscription maintenance using existing EF concurrency patterns. |
| `src/Pegasus.Infrastructure/Email/GraphMailboxChangeSubscriptions.cs` (new) | Reuse the existing Graph credential/HTTP conventions for exact-Inbox create, PATCH renewal/reauthorization and recreate. |
| `src/Pegasus.Infrastructure/Transport/AzureQueueWorkEnqueuers.cs` | Extend INTK-043's typed `intake-work` envelope and sender with mailbox/subscription identifiers; add no queue client. |
| `src/Pegasus.Web/GraphMailWebhook.cs` (new), `src/Pegasus.Web/Program.cs` | Map the anonymous validation/notification endpoint, bounded parser, constant-time clientState check, active-subscription resolution and prompt enqueue response. |
| `src/Pegasus.Worker/IntakeFunctions.cs`, `MailboxFunctions.cs`, `WorkerDependencyInjection.cs` | Dispatch mailbox messages in `UnifiedWorkFunction`; extend unified poison handling; turn the existing Inbox timer into fallback plus due maintenance. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/*`, `PegasusDbContextModelSnapshot.cs` | Establish the single stable-ID/subscription schema and exact Web/Worker SQL grants; no compatibility schema. |
| `src/Pegasus.Web/appsettings.Development.json`, `src/Pegasus.Worker/local.settings.example.json` | Add bounded non-secret settings and the five-minute recovery schedule. |
| `infra/main.bicep`, `infra/main.parameters.json`, `infra/modules/platform.bicep` | Add Key Vault-backed clientState/callback settings and permissions; reuse `intake-work`; preserve the post-INTK-043 capacity configuration. |
| `scripts/Test-AzureDeploymentPlan.ps1`, `scripts/Invoke-ProductionSmoke.ps1` | Check the exact endpoint, settings, function census, secret reference and schedule without printing secrets or claiming live latency. |

## Tests

| Path | Evidence |
|---|---|
| `tests/Pegasus.Core.Tests/Intake/PollApprovedInboxTests.cs` | Targeted and fallback calls share one lease/delta path; disable, overlap and retry fail safely. |
| `tests/Pegasus.Core.Tests/Intake/MailboxChangeNotificationTests.cs` (new) | Minimal create/renew/recreate and lifecycle decisions, including the 48-hour renewal margin. |
| `tests/Pegasus.IntegrationTests/ApprovedMailboxEstateIntegrationTests.cs`, `ApprovedMailboxIdentityMigrationTests.cs` | Stable ID, activation cutoff, scope mismatch/410, no cursor adoption and one coherent schema. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Exact basic `created` subscription, expiry, PATCH, recreate and bounded failure mapping. |
| `tests/Pegasus.IntegrationTests/GraphMailWebhookTests.cs` (new) | Validation token, batch limits, clientState/scope rejection, 202-after-send, 5xx-on-send-failure and no secret disclosure. |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | Wake, lifecycle and five-minute fallback converge without duplicate receipt or sender regression. |
| `tests/Pegasus.ArchitectureTests/WorkerCompositionTests.cs`, `WorkerAzureClientCompositionTests.cs`, `WorkerActivationReleaseContractTests.cs`, `DependencyDirectionTests.cs` | One unified queue/poison route, no mailbox queue/function, correct ownership and exact deployment census. |
| `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | Web lookup-only and Worker maintenance/operational grants. |

## Context files

| Path | Constraint |
|---|---|
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Binding wake, lifecycle, fallback, fresh-start and neutral-sender behaviour. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Commit-before-publish, identifier queue and Worker-only processing. |
| `docs/adr/0024-stable-approved-mailbox-identity-and-explicit-baseline.md` | Stable ID and per-mailbox activation; forbids cursor-carrying re-key. |
| `docs/adr/0032-near-real-time-durable-intake-triggering.md` | Web validates/enqueues; Worker owns Graph/delta/intake; polling is recovery. |
| INTK-043 branch at `6c42d53d` | Existing typed `intake-work`, `UnifiedWorkFunction`, poison route and capacity change to reuse after merge. |
| `docs/runbook.md` | Live Graph, SQL and Azure writes remain separately approved. |

## Deliberately out of scope

No product implementation during preparation; no cloud/mailbox write; no extra queue, Function, host, event bus, rich notification, compatibility path, feature flag or capacity change; no rewrite of downstream intake, manual upload, sender projection or UI.
