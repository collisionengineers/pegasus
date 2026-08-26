# Research — MAIL-013: Graph change-notification wake-up for approved Inbox intake

## Scope and ticket identity

There is no `MAIL-031` item on the Kanmer board or in the repository. The matching existing ticket is `MAIL-013 — Wake approved mailbox intake through Graph change notifications`; this research refreshes that ticket and does not create a duplicate.

## Verified repository and Azure state — 2026-08-26

- Planning baseline is current `origin/dev` at `1a8fda3e`. PR #548 (mailbox Image Intake) and PR #553 (immediate committed-work publication) are merged into that baseline. The earlier plan's wait for INTK-040/INTK-042 is therefore obsolete.
- INTK-042 moved the Azure Queue senders into `Pegasus.Infrastructure/Transport/AzureQueueWorkEnqueuers.cs` and composed them in both Web and Worker. MAIL-013 can reuse that transport convention and the Web queue-sender identity/RBAC; it must not recreate the old Worker-local adapters.
- `PollApprovedInbox.ExecuteAsync` still enumerates the complete approved-mailbox estate. Its single-mailbox implementation is private. A notification-specific caller therefore needs one targeted Core entry point that reuses the same validation, SQL lease, approval re-check, Graph delta, retention, cursor advancement and failure handling.
- The existing Worker timer remains `InboxPollFunction` on `ApprovedInboxPollSchedule`. Both `origin/dev` IaC and the deployed Function App currently use `*/15 * * * * *`.
- Azure read-only inspection confirms the production Web Container App is externally reachable, provisioned, and fixed at `minReplicas: 1`, `maxReplicas: 1`. It is already warm enough to receive the callback; no new host or Web scale change is justified.
- Azure read-only inspection confirms the Worker is running on Flex Consumption, .NET isolated 10, 2 GiB, maximum 20 on-demand instances, with `alwaysReady: null`. That is the platform default of zero always-ready instances.
- The governing contract is already accepted in FRD-08 and ADR-0032: Web validates and enqueues an identifier; Worker alone reads Graph and owns mailbox cursor/intake; SQL holds subscription state; the fallback poll is recovery; scale-to-zero remains unless deployed measurements identify Worker cold start as the remaining constraint.

## Microsoft Learn findings

### Webhook protocol

- Graph validates each `notificationUrl` (and a distinct `lifecycleNotificationUrl`, if used) by POSTing a URL-encoded `validationToken`. The endpoint must return the URL-decoded opaque token, `200 OK`, and `text/plain` within ten seconds. Returning an encoded token fails subscription creation.
- Normal notification requests can contain a batch. Graph treats a 2xx response received within three seconds as delivered. If work cannot complete inside that window, Microsoft recommends durably queueing it and returning `202 Accepted`. A queue-send failure should return 5xx so Graph retries rather than losing the wake.
- Graph marks an endpoint slow when more than 10% of responses exceed three seconds in a ten-minute window and delays new notifications by ten minutes. It marks an endpoint drop when more than 15% exceed the ten-second retry timeout and drops notifications for ten minutes. The callback therefore cannot perform a Graph delta read or intake work inline.
- For basic notifications, `clientState` is the authenticity check and has a maximum length of 128 characters. It must equal the value supplied at subscription creation. Pegasus should compare it in constant time, queue no invalid item, avoid detailed error disclosure, and never log the value.
- Source: [Receive change notifications through webhooks](https://learn.microsoft.com/graph/change-notifications-delivery-webhooks) and [subscription resource](https://learn.microsoft.com/graph/api/resources/subscription?view=graph-rest-1.0).

### Outlook subscription shape

- Outlook messages support basic notifications and folder-scoped resources. This ticket needs only `changeType: created` on the exact approved mailbox Inbox resource; `updated` and `deleted` would create unnecessary wakes for read-state, category, move and delete activity.
- Microsoft documents a limit of 1,000 active Outlook subscriptions per mailbox across all applications. Pegasus needs one per enabled approved Inbox, far below that limit.
- A basic Outlook message subscription can live for at most 10,080 minutes (under seven days). Six-hour maintenance with renewal before the last 48 hours provides repeated retry opportunities without adding a second scheduler.
- Basic notifications intentionally omit message content. The queue message can remain a small stable mailbox/subscription wake; Worker obtains the actual changes from the existing folder delta cursor.
- Source: [Outlook change notifications](https://learn.microsoft.com/graph/outlook-change-notifications-overview) and [subscription lifetime](https://learn.microsoft.com/graph/change-notifications-overview#subscription-lifetime).

### Lifecycle and recovery

- Outlook messages support `reauthorizationRequired`, `subscriptionRemoved`, and `missed` lifecycle notifications. The normal and lifecycle URL may be the same endpoint.
- A lifecycle URL cannot be added to an existing subscription by PATCH. A subscription missing that URL must be recreated.
- `missed`: acknowledge, validate, then run delta resynchronisation.
- `subscriptionRemoved`: recreate the subscription, then use delta to fetch changes from the gap.
- `reauthorizationRequired`: renew/reauthorize. Microsoft warns not to issue reauthorize and PATCH renewal for the same subscription within ten minutes; one PATCH with a new expiry both renews and reauthorizes.
- These instructions fit Pegasus's existing delta cursor and idempotent receipt path. Lifecycle handling schedules that same path; it is not another mail processor.
- Source: [Reduce missing subscriptions and change notifications](https://learn.microsoft.com/graph/change-notifications-lifecycle-events) and [message delta](https://learn.microsoft.com/graph/api/message-delta?view=graph-rest-1.0).

### Latency limitation

Microsoft's published Outlook `message` notification latency is **less than one minute average and up to three minutes maximum**. Graph change notifications remove Pegasus's fixed polling wait and usually reduce Graph calls, but Microsoft does not offer a five-second delivery guarantee. Therefore:

- MAIL-013 can target a sub-three-second Pegasus callback acknowledgement and measure callback-to-queue, queue-to-lease, delta-to-receipt, and receipt-to-terminal-processing separately.
- It cannot honestly prove “under five seconds from Exchange receiving the message” as a deterministic contract.
- DELIV-021 must report the Graph-delivery share separately from Pegasus processing. If the Pegasus share misses its target, measure Worker cold start before considering always-ready capacity; do not add it speculatively.
- Source: [Microsoft Graph change-notification latency](https://learn.microsoft.com/graph/change-notifications-overview#latency).

### Azure queue and hosting behaviour

- Azure Functions Storage Queue triggers retry a failed message up to five attempts and then place it on `<queue>-poison`. The existing explicit poison-function convention should be reused for mailbox wakes so terminal failures remain visible.
- Managed identity is preferred for queue access. INTK-042 already established this transport and RBAC pattern.
- Flex Consumption always-ready defaults to zero and adds baseline GB-second plus execution billing without free grants. It reduces cold start, but current evidence does not show cold start is the bottleneck.
- Azure Container Apps `minReplicas >= 1` keeps an instance running. The deployed Web already has exactly one.
- Sources: [Azure Queue trigger](https://learn.microsoft.com/azure/azure-functions/functions-bindings-storage-queue-trigger), [Flex Consumption](https://learn.microsoft.com/azure/azure-functions/flex-consumption-plan), and [Container Apps scaling](https://learn.microsoft.com/azure/container-apps/scale-app).

## Chosen target

1. Keep one narrow anonymous `POST /hooks/microsoft-graph/mail` endpoint on the existing warm Web app.
2. For validation, return the decoded token exactly. For notification batches, enforce small request/body/item limits; validate `clientState`, tenant, active subscription, exact resource scope and enabled approved mailbox; enqueue one identifier-only wake per valid subscription/mailbox; return 202 after durable Azure Queue publication. Invalid items are ignored with no queue and no secret oracle. A publication failure returns 5xx for Graph retry.
3. Add one SQL subscription row per approved Inbox. Store approved-mailbox id, Graph subscription id, exact resource/scope fingerprint, expiry, lifecycle/maintenance state and last failure. Do not store `clientState`.
4. Add one targeted mailbox Core method that delegates to the existing single-mailbox lease/delta path. The queue trigger revalidates the mailbox at claim time. Duplicate notifications, fallback overlap and queue retries are harmless because the existing SQL lease and source receipts remain authoritative.
5. Add one six-hour maintenance timer. Create missing subscriptions, PATCH subscriptions within 48 hours of expiry, use one PATCH for reauthorization plus renewal, and recreate removed/expired/wrong-scope subscriptions before delta recovery.
6. Change the existing Inbox timer to `0 */5 * * * *` as recovery only. Do not add a second business path, new runtime, Event Grid/Event Hubs, resource-data encryption, a generic event bus, or always-ready Functions.
7. Emit non-secret correlated telemetry for callback received/validated/enqueued, queue dequeued, lease claimed/skipped, delta started/completed, receipt accepted and terminal intake. Measure from Graph message `receivedDateTime` where available, while labelling Graph delivery and Pegasus processing separately.

## Main risks and controls

- **Graph delivers later than five seconds:** external platform limitation; measure and report separately rather than disguising it as application time.
- **Anonymous endpoint abuse:** bounded request and batch, exact route, active-subscription/scope checks, constant-time secret comparison, uniform response for invalid content.
- **Lost wake:** 5xx on queue failure, Graph retries, lifecycle recovery, five-minute fallback delta poll.
- **Duplicate/concurrent wake:** one existing mailbox lease and idempotent receipt identities.
- **Expired/removed subscription:** six-hour maintenance plus lifecycle-driven recreate/renew and delta.
- **Secret exposure:** Key Vault-backed configuration only; no SQL, queue, logs, responses or proof.
- **Cost creep:** reuse already-warm Web and existing Flex Worker; no always-ready instance without measured evidence and separate approved cloud change.
- **Sender regression:** reuse MAIL-009's neutral unresolved sender/effective-sender policy; do not introduce a webhook-side sender projection.

## Open questions

None for implementation planning. The five-second end-to-end ambition remains a measurement goal, not a promise Microsoft Graph's documented message-notification SLA can support.

## Addendum — current implementation versus proposed implementation (2026-08-26)

### The boundary that actually changes

MAIL-013 does **not** replace identification, classification, extraction, case matching, allocation, Image Intake, custody, sender resolution, or the durable intake queue. Those stages already have one Core-owned implementation.

It replaces only the first scheduling hop for email:

- **Current:** a 15-second Worker timer discovers that the Inbox changed.
- **Proposed:** Microsoft Graph tells the warm Web app that the Inbox changed; Web queues a mailbox wake; Worker runs the same delta/intake route.

Everything from `ReceiveIntake` onward is intended to remain the same. This distinction matters: MAIL-013 can remove pre-receipt polling delay and idle polling work, but it cannot by itself make slow downstream classification, extraction or case creation faster.

### Current route, traced from `origin/dev` at `1a8fda3e`

1. `InboxPollFunction` fires from `ApprovedInboxPollSchedule`, currently deployed as `*/15 * * * * *`.
2. The Function calls `PollApprovedInbox.ExecuteAsync(50, system-worker actor)`.
3. `PollApprovedInbox` calls `IApprovedIntakeMailboxes.ListPollableAsync`, validates the full returned estate, then visits every mailbox sequentially.
4. `EfApprovedInboxPollStore.ClaimAsync` re-checks approval in a serializable transaction, rejects a mailbox that is not yet due or is already leased, and otherwise grants a one-minute lease.
5. `GraphMailClient` runs the stored folder delta cursor, validates every next/delta URI against the exact Graph mailbox and Inbox folder, requests immutable IDs, and downloads MIME for each admitted message.
6. For each message, `PollApprovedInbox` prepares the provider-neutral source and calls `ReceiveIntake.ExecuteAsync`.
7. Since INTK-042, `ReceiveIntake` commits the staged receipt/work row and immediately asks `DispatchPendingIntakeWork` to publish that exact receipt ID to `intake-work`. A publication failure does not erase the committed receipt; the one-minute recovery sweep can republish it.
8. After intake acceptance, the mailbox route inserts the retained-message projection and advances the mailbox cursor. A failed retain or cursor advance leaves the cursor replayable; unique source/retained identities make replay safe within the current identity model.
9. `IntakeWorkFunction` consumes the receipt ID and calls `ProcessQueuedIntake`, which owns identification, classification, extraction, association/allocation, Image Intake and terminal/retry outcomes.
10. Mail UI reads the retained-message and receipt projections. MAIL-009 already derives a provisional effective sender from retained evidence until the later route decision exists, so it does not need the webhook to invent or update a sender.

Sources: `src/Pegasus.Worker/MailboxFunctions.cs`, `src/Pegasus.Core/Intake/MailboxIntake.cs`, `src/Pegasus.Infrastructure/Persistence/EfApprovedInboxPollStore.cs`, `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs`, `src/Pegasus.Core/Intake/DurableIntake.cs`, `src/Pegasus.Worker/IntakeFunctions.cs`, and `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs`.

### Proposed route

1. The six-hour Worker maintenance caller ensures each enabled approved Inbox has one exact-scope basic `created` Graph subscription.
2. Graph POSTs validation or a normal/lifecycle notification to the existing warm Web app.
3. The Web callback:
   - returns a validation token directly; or
   - validates the bounded batch, constant-time `clientState`, tenant, active subscription, scope and enabled mailbox;
   - publishes only the canonical approved-mailbox/subscription identifiers to `mailbox-wake`;
   - returns 202 after the queue send, or 5xx when a valid wake could not be queued.
4. `MailboxWakeFunction` consumes the wake, resolves and revalidates the approved mailbox, and calls a targeted entry point on the existing `PollApprovedInbox` owner.
5. That targeted entry point uses the same SQL lease, Graph delta, MIME, `ReceiveIntake`, retained-message and cursor code as the current timer route.
6. The existing Inbox timer runs every five minutes as recovery. It still enumerates the estate and enters the same mailbox lease path.
7. Lifecycle `missed`, `subscriptionRemoved` and `reauthorizationRequired` events schedule renewal/recreation and the same delta recovery; they do not process messages in Web.

### Side-by-side comparison

| Concern | Current implementation | Proposed implementation | Consequence |
|---|---|---|---|
| Ordinary trigger | Worker timer every 15 seconds | Graph callback, then Azure Queue wake | Removes the fixed local poll wait but adds dependency on Graph delivery latency. |
| Idle behaviour | Lists the estate and attempts mailbox claims/delta work on every tick even when no mail arrived | No Inbox read until Graph sends a wake; six-hour subscription maintenance plus five-minute recovery remains | Fewer idle Function/Graph operations. |
| Timer frequency | 172,800 schedule occurrences in a 30-day month at 15 seconds | 8,640 recovery occurrences at five minutes | 95% fewer Inbox timer occurrences; this is not a claim about total Worker executions because callbacks and queue wakes are demand-driven. |
| Mailbox targeting | One tick enumerates and sequentially visits the complete approved estate | Ordinary wake names one mailbox; recovery still visits the estate | A busy/changed mailbox no longer makes every ordinary wake inspect every mailbox. |
| Web responsibility | No inbound notification endpoint; Web already publishes committed upload work to Azure Queue | Adds one narrow anonymous Graph protocol endpoint and one mailbox-wake sender | Web remains a transport boundary, not a mail reader or processor. |
| Worker responsibility | Timer, delta/cursor, intake and queue processing | Queue wake plus the same delta/cursor/intake; slower fallback and subscription maintenance | Worker remains sole mailbox and intake owner. |
| Graph reads | Delta query is timer-initiated | Delta query is wake-initiated, with timer/lifecycle recovery | Provider reading semantics stay unchanged. |
| Durable intake | `ReceiveIntake` commits, immediately publishes `intake-work`, then recovery can republish | Exactly the same | MAIL-013 must reuse INTK-042; no second intake queue or publisher. |
| Downstream processing | `IntakeWorkFunction → ProcessQueuedIntake` | Exactly the same | No classification/extraction/case-creation rewrite or speed-up is implied. |
| Duplicate protection | Poll lease, delta cursor, source identity, retained-message uniqueness and queued-work claims | Same controls, now also covering duplicate Graph and Queue delivery | At-least-once notifications do not require a second idempotency system. |
| Failure recovery | 30-second mailbox failure release, next 15-second tick, queue poison/recovery | Graph retry, wake poison, lifecycle delta recovery and five-minute fallback, plus existing intake recovery | More explicit trigger recovery, while processing recovery remains unchanged. |
| Sender/state display | Retained projection can appear before processing; MAIL-009 derives the original/provisional effective sender from retained evidence | Same projection and sender policy, reached sooner when the wake is prompt | The proposed trigger must not create a placeholder desk sender or a second UI state model. |
| Hosting | Warm Web 1/1; Flex Worker scale-to-zero | Same | No new runtime and no speculative always-ready charge. |
| External latency | Predictable local wait of 0–15 seconds before the delta call, plus Graph/delta/processing time | No local schedule wait, but Graph documents message notification delivery at under one minute average and up to three minutes | The proposal improves the controllable Pegasus segment; it cannot guarantee five seconds from Exchange receipt. |

### Manual upload comparison

Manual upload has already received the important post-commit improvement that the email route will reuse:

- Web calls `ReceiveIntake`.
- The durable staged receipt/work commit happens before acknowledgement.
- INTK-042 immediately publishes the receipt ID to `intake-work`.
- `IntakeWorkFunction` runs the same `ProcessQueuedIntake` stages.

Therefore manual upload does not need a Graph wake or the new mailbox-wake queue. Current email differs only because it must first discover a message in an external mailbox. After `ReceiveIntake`, email and upload already converge on the same durable processing route.

### Complexity comparison

The proposal adds a callback, one identifier queue, subscription state and lifecycle maintenance. That is more source code than a timer alone, but each piece is required by the external Graph protocol:

- the callback must finish promptly and cannot hold Graph open through a delta/intake pass;
- the queue is the existing host boundary between warm Web and Worker;
- subscription state is required because Graph subscriptions expire and callbacks identify a subscription;
- lifecycle/renewal work is required to avoid silently losing notifications;
- fallback polling remains because Graph documents missed/dropped notification conditions.

The simplest coherent form is therefore one callback URL for both normal and lifecycle notifications, one mailbox-wake queue, one targeted Core mailbox method, one maintenance caller and the existing timer slowed to recovery. Combining callback, delta and processing would reduce the number of named components but would duplicate ownership, violate Graph's response deadline and make failures less durable.

### Important implementation gap: current mailbox identity is still obsolete

The current code does not yet implement ADR-0024 even though FRD-08 and the proposed subscription design require it:

- `ApprovedMailboxEntity.Id` is the stable internal `Guid`.
- `ApprovedIntakeMailbox` currently omits that `Guid` and carries only Graph mailbox identity, address and folder identity.
- `EfApprovedMailboxStore.ListPollableAsync` maps `MailboxIdentity` into `ApprovedIntakeMailbox.MailboxId`.
- `ApprovedInboxPollStates`, poison rows, retained messages and receipt tokens are consequently keyed by the replaceable Graph mailbox identity.
- `EfApprovedInboxPollStore` contains an adoption path that re-keys state by Graph identity while carrying the old delta cursor; its own code comment records that Graph then rejects the cursor scope and that clearing it risks duplicate receipts.
- `docs/current-architecture.md` and `docs/runbook.md` explicitly describe stable mailbox identity/fresh-start as accepted but not implemented.

The proposed callback intends to queue `ApprovedMailbox.Id`, and its SQL subscription row intends to relate one-to-one to that ID. Simply adding this new stable row while the downstream poll/receipt route remains Graph-keyed would leave two mailbox identities in one flow and extend an acknowledged pre-release defect.

Implication for the next planning pass: implementation must reach one coherent ADR-0024 state before or as MAIL-013 lands. At minimum the pollable-mailbox model, targeted lookup, poll state, cursor-scope fingerprint, activation boundary, poison/retained/source identity and receipt-token derivation must agree on `ApprovedMailbox.Id`. No separate Kanmer ticket currently owns that implementation, so `kanmer-plan` must either include the required migration in MAIL-013 or create and link an explicit blocker before execution. It must not layer Graph subscriptions on top of the obsolete Graph-keyed state or preserve both paths.

Sources: `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs`, `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs`, `src/Pegasus.Infrastructure/Persistence/EfApprovedInboxPollStore.cs`, `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyEntities.cs`, `docs/adr/0024-stable-approved-mailbox-identity-and-explicit-baseline.md`, `docs/current-architecture.md`, and `docs/runbook.md`.

### What the proposed change can and cannot improve

**It can improve:**

- the 0–15 second local discovery delay;
- repeated idle Inbox polling and associated Function/Graph work;
- ordinary targeting from whole-estate to one mailbox;
- trigger-stage observability and lifecycle recovery;
- how quickly the existing retained/intake route starts after a Graph wake arrives.

**It cannot by itself improve:**

- Microsoft Graph's own notification delivery latency;
- MIME download time or Graph throttling;
- identification, classification, extraction, allocation, Image Intake or case-creation execution time after `intake-work` is published;
- UI refresh cadence after backend state commits;
- any remaining sender/state defect inside the existing retained-mail projection.

Those later segments need their own measured traces and focused remediation if they remain slow after MAIL-013.
