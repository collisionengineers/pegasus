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
