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

## Focused re-review — 2026-08-26 — PR #563 @ fab4b06d9fba17df068da6a29fc99e63702e4133

### Scope and authority

This re-review was limited to [[PR-067]] and [[PR-068]] corrections plus confirmation that the prior unaffected review findings remain valid. The user explicitly directed “just merge it” after being told GitHub had registered no CI checks for the corrected head. That direction is recorded as a one-time waiver of the missing GitHub CI run for this PR only; it does not alter repository CI rules generally.

### Corrections checked

- **PR-067 — pass.** The migration no longer deletes poll, poison, retained-message, attachment, or folder-move evidence. It fails closed before mutation if an old Graph mailbox identity has no exact approved-mailbox mapping, maps the three operational tables to the stable GUID, changes/renames the columns in place, rebuilds the affected indexes and foreign keys, and retains exact Web SELECT / Worker SELECT-INSERT-UPDATE subscription grants. The populated migration test carries retained evidence plus a restrictive folder-move dependant through the migration. Local correction evidence: populated migration/grant tests 2/2 within the 81/81 correction matrix.
- **PR-068 — pass.** Every notification now requires clientState, tenant, active subscription and compatible resource before a supported wake kind is accepted. Only `created`, `missed`, `subscriptionRemoved`, and `reauthorizationRequired` are supported; unknown change/lifecycle values enqueue nothing. Tests cover all lifecycle kinds, wrong secret/tenant/resource/change/lifecycle, unknown subscription, malformed and 101-item batches, non-disclosure, successful 202, and retryable 500 on queue failure. Local evidence: webhook contract 12/12 within the 81/81 correction matrix.
- **Prior unaffected findings — remain valid.** The corrected diff is confined to the migration, webhook and their integration/grant tests. Stable mailbox identity, targeted unified queue/function/poison dispatch, Worker-only delta/intake ownership, five-minute recovery, six-hour maintenance, exact-Inbox Graph subscription operations, removal of the configured-mailbox/adoption path, preserved capacity, and the recorded simplification findings are unchanged.

### Evidence and verdict

Reviewed exact corrected head `fab4b06d9fba17df068da6a29fc99e63702e4133`. Local evidence recorded by implementation: clean locked restore and Release build; Core 1001/1001; Architecture 100/100; correction matrix 81/81; Bicep build; local deployment-plan validation; clean diff check. GitHub still showed zero check runs, explicitly waived by the user's informed “just merge it” direction.

**Verdict: pass under the explicit CI waiver.** Both review blockers are satisfied. The exact reviewed head was merged into `dev` as merge commit `834b88e15739c68f9ebf040d981fed020f7b0110`. Deployment/main promotion is not part of this review.
