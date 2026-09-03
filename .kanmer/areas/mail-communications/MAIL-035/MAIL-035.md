---
id: MAIL-035
type: ticket
title: Ingest the notified Inbox message directly on a Graph change-notification wake
status: backlog
area: mail-communications
order: 690
assignee: ''
profile: feature
labels:
  - graph
  - intake-latency
  - azure-diagnostics
groups:
  - EPIC-011
links:
  - MAIL-033
  - MAIL-029
  - MAIL-025
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-09-02T14:56:37.210Z'
updated: '2026-09-03T15:15:28.290Z'
---

## What

When a Microsoft Graph `created` change notification wakes the Worker, fetch **that message** by the id carried in the notification's `resource` and ingest it through the existing receipt path, instead of relying only on an immediate delta read. The delta cursor and the five-minute `InboxRecoveryFunction` timer stay as the completeness sweep; nothing about cursor ownership changes.

## Why

Live evidence, 2026-09-02 (release 38, after the seventh intake-data wipe). Three QDOS test emails; all UTC:

| Message | Graph `receivedDateTime` | Wake enqueued | Delta polls that returned nothing | Poll that returned it | Delay |
| --- | --- | --- | --- | --- | --- |
| EREF9 (15.2 MB) | 14:00:36 | 14:00:36 | 14:00:37, 14:04:19, 14:05:02 (timer), 14:05:15 | 14:10:00 (timer) | 9m 28s |
| EREF8 (0.9 MB) | 14:04:17 | 14:04:17 | 14:04:19, 14:05:02, 14:05:15 | 14:10:00 (timer) | 5m 46s |
| EREF24 (7.7 MB) | 14:05:12 | 14:05:13 | — | 14:05:15 (wake) | 3s |

`AppDependencies` shows each wake issuing one delta GET (200, 75–90 ms) and no `/$value` MIME fetch: the delta page held nothing new. The subscription was Active, the webhook returned 202 within a second, the queue was empty, and `ApprovedInboxPollStates` had no failure code or stuck lease. Microsoft documents the behaviour: "Expect varying delays between the time a resource instance changes, and the time the tracked change is reflected in a delta query response … Retry the `@odata.deltaLink` after some time" (learn.microsoft.com/graph/delta-query-overview#limitations). The wake path (`GraphMailWebhook.HandleAsync` → `intake-work` → `UnifiedWorkFunction` → `PollApprovedInbox.ExecuteMailboxAsync`) does one delta read and never retries, so a lagging message waits for the timer, which can itself miss it once more.

[[MAIL-033]] is not the cause: its sparse-entry skip has produced no `InvalidDataException` since 12:45Z, and the 14:10:00 timer re-listed EREF24 with a second MIME fetch that `ReceiveIntake` deduplicated on `ExternalReceiptToken` — the dedupe this ticket relies on already works in production.

## Approach

- `src/Pegasus.Web/GraphMailWebhook.cs` — parse the message id from `notification.Resource` (`Users/{id}/Messages/{messageId}`; `MatchesSubscribedMailbox` already checks that prefix) and pass it to `IMailboxWakeEnqueuer`; a `created` notification without an id is ignored as today.
- `src/Pegasus.Core/Intake` — extend the `IMailboxWakeEnqueuer` contract and the `Created` wake with an optional message id; extend `IApprovedInboxSource` with a single-message read returning the same `ApprovedInboxMessage` shape (MIME + `RetainedMetadata`) that `ReadAsync` builds.
- `src/Pegasus.Infrastructure/Transport/AzureQueueWorkEnqueuers.cs` — `UnifiedWorkQueueMessage.FormatMailbox` / `TryParseMailbox` carry the id.
- `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` — implement the single-message read with the existing `client.ReadMimeAsync` plus a `messages/{id}?$select=…` metadata GET using the same `$select` list as the delta URI; skip when `parentFolderId` is not the approved Inbox.
- `src/Pegasus.Core/Intake/MailboxIntake.cs` — add `PollApprovedInbox.ExecuteNotifiedMessageAsync(approvedMailboxId, messageId, actor)` reusing the per-message body of `PollOneAsync` (activation check, retain, `receiveIntake`, `retainedMessageStore`) **without** `AdvanceAsync`/`CompleteAsync`; then fall through to `ExecuteMailboxAsync` so a burst of notifications still drains.
- `src/Pegasus.Worker/IntakeFunctions.cs` — call the new entry point when the wake carries an id.
- Verify notifications and delta agree on immutable ids (`Prefer: IdType="ImmutableId"`); dedupe does not depend on it, the direct GET does.
- Tests: `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` (Kiota fake) for the single-message read; Core tests for the notified path and for a later delta re-listing producing no second receipt.
- FRD-08 behaviour note (notified fetch + delta sweep); `docs/current-architecture.md` after deploy.

Rejected alternative: re-enqueue the wake with growing `visibilityTimeout` (45 s / 90 s / 180 s) when it handles zero messages — smaller diff, but the observed lag exceeds that ladder, so the timer would still decide the worst case.

## Verification

- [ ] A `created` wake stages the notified message with `IntakeStagedReceipts.Actor = system-worker:approved-inbox-notification` and `StagedAtUtc − ReceivedAtUtc` under 10 s, and `AppDependencies` shows a `/$value` fetch on the wake.
- [ ] The next timer poll adds no second receipt for the same `ExternalReceiptToken`.
- [ ] A notification whose message is no longer in the Inbox (moved/deleted) ingests nothing and does not fail the wake.
- [ ] Canonical restore/build/test pass; no local/test profile calls Graph.
