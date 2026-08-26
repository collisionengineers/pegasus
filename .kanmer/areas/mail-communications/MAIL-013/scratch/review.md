## Independent review — 2026-08-26 — PR #563 @ c74c3257e87dcbc9052bc5ee9cbfe7e4c0c5b447

Independent reviewer; I did not implement this branch.

### Changes

- `ApprovedMailboxAdministration.cs`, mailbox Core intake/retained-mail records, EF entities/configuration/stores, and dependent Web/MCP/UI callers move inbound operational identity from replaceable Graph coordinates to `ApprovedMailbox.Id`, add activation/scope evidence, and remove `ConfiguredApprovedIntakeMailboxes` and poll-state identity adoption.
- `ApprovedMailboxSubscriptions.cs`, `MailboxChangeNotifications.cs`, `EfApprovedMailboxSubscriptionStore.cs`, and `GraphMailboxChangeSubscriptions.cs` add one SQL subscription record per approved mailbox and exact-Inbox Graph create/PATCH/recreate behavior.
- `GraphMailWebhook.cs` and `Program.cs` add the anonymous 64 KiB callback boundary, validation-token response, clientState/tenant/subscription checks, and unified-queue publication.
- `AzureQueueWorkEnqueuers.cs`, `IntakeFunctions.cs`, and `MailboxFunctions.cs` add a mailbox wake envelope to the existing `intake-work`/poison route, dispatch to targeted `PollApprovedInbox`, and rename the existing timer to five-minute recovery with six-hour due maintenance.
- The EF migration/model snapshot, database bootstrap, Bicep, parameters, smoke/deployment-plan scripts, local settings, architecture/Core/integration tests and affected fixtures are updated for the new identity, subscription, schedule, secret, grants, and function census. No new queue, Function App, timer Function, deployment unit, feature flag, or capacity setting is introduced.

### Comments and disposition

1. **Blocking — migration violates the governing evidence boundary and is unsafe with populated state.** `20260826151807_ApprovedMailboxStableIdentityAndSubscriptions.Up` unconditionally deletes `RetainedMailboxMessages` during ordinary schema deployment. ADR-0024 says a pre-launch operational reset needs separate exact-target approval and may never delete retained business evidence. The delete can also fail when `RetainedMailFolderMoves` references a retained message through its Restrict foreign key. Disposition: filed [[PR-067]], blocking MAIL-013.
2. **Blocking — webhook contract and evidence are incomplete.** The endpoint enqueues any non-empty, unrecognised `lifecycleEvent` as `Created` and skips resource matching for all lifecycle events. The only webhook tests are the validation handshake and one valid `created` item. There is no executable evidence for wrong clientState/tenant/subscription/resource, expired/disabled subscription, malformed or oversized batch, supported and unsupported lifecycle kinds, queue-send failure returning 5xx, or secret non-disclosure, despite the plan/report claiming those cases. Disposition: filed [[PR-068]], blocking MAIL-013; the report must be corrected to match evidence.
3. **Blocking — required CI is absent.** GitHub reports zero check runs for the unchanged head, whereas recent code PRs run repository-check lanes including infrastructure, unit, SQL integration, browser and coverage. Repository workflow forbids merge until CI is green. Disposition: no separate product ticket yet; the corrective synchronize event must trigger CI and the re-review must inspect it.
4. **Non-blocking — successful structural points.** Stable `ApprovedMailbox.Id` is carried through leases, poll/poison/retained state and targeted wake; Web queues identifiers only; Worker retains Graph delta/intake ownership; the configured-mailbox fallback is deleted; the unified queue/function/poison path is reused; timer schedule is five minutes and maintenance query is six-hour due; Graph create/PATCH uses exact Inbox resource and a six-day expiry with 48-hour renewal decision; IaC preserves INTK-043 always-ready scope without adding capacity. Disposition: accepted as implemented, subject to blockers above.
5. **Non-blocking — simplification pass.** The recorded independent pass has concrete applied findings and the diff does not add a second mailbox processor, queue, Function, host, flag, or capacity layer. Disposition: accepted.

### Verdict

**Needs changes.** The reviewed head is unchanged and mergeable, but the migration is not safe or governing-doc compliant, webhook acceptance evidence is materially incomplete, and no CI checks ran. PR #563 was not merged and MAIL-013 remains in Review. Re-run this independent review after [[PR-067]] and [[PR-068]] land on the PR head and all required repository-check lanes are green.
