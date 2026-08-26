# Research — MAIL-013

## Question

How can approved Inbox mail start promptly without adding another processing path, queue, Function, or speculative capacity?

## Verified current state — 2026-08-26

- Current `origin/dev` is `93060b61`. `InboxPollFunction` runs `ApprovedInboxPollSchedule=*/15 * * * * *`.
- Each tick lists every approved inbound mailbox and processes them sequentially through `PollApprovedInbox`. Every mailbox already has its own SQL lease and Graph delta cursor.
- Email and manual upload converge at `ReceiveIntake`. INTK-042 commits then immediately publishes the intake identifier; identification, classification, extraction, allocation and case creation remain in the shared Worker path.
- The existing retained-mail projection already owns sender display. MAIL-013 must not write a temporary forwarding-desk sender or introduce another UI state.
- ADR-0024 is accepted but not implemented: `ApprovedIntakeMailbox`, poll state, poison rows, retained mail and receipt occurrence identity still use the replaceable Graph mailbox identity. `EfApprovedInboxPollStore` still carries a cursor while re-keying that identity. A Graph subscription cannot safely be added on top of this dual identity.
- INTK-043 is implementing one typed `intake-work` queue, one `UnifiedWorkFunction`, one poison Function and the measured warm-capacity change. MAIL-013 touches the same transport, Worker composition and infrastructure files, so INTK-043 now blocks it. Reusing that route avoids recreating a mailbox-only queue and Function.

## Microsoft Learn constraints

- Graph validates the webhook with a URL-encoded `validationToken`; Pegasus must return the decoded opaque token as `200 text/plain` within ten seconds.
- A normal notification is considered delivered when Graph receives 2xx within three seconds. Microsoft recommends persisting it to a queue and returning `202`; non-2xx delivery is retried.
- `clientState` is the notification authenticity secret and is limited to 128 characters on the subscription resource. It must be compared without logging or returning it.
- Basic Outlook message subscriptions can target an exact Inbox and live for at most 10,080 minutes. Lifecycle events are `missed`, `subscriptionRemoved` and `reauthorizationRequired`; renewal and reauthorization can be one PATCH.
- Microsoft publishes Outlook message notification latency as under one minute average and up to three minutes. Graph removes Pegasus's 0–15 second local scheduling wait but cannot guarantee five seconds from Exchange receipt.

Sources: [webhook delivery](https://learn.microsoft.com/graph/change-notifications-delivery-webhooks), [subscription resource](https://learn.microsoft.com/graph/api/resources/subscription?view=graph-rest-1.0), [lifecycle events](https://learn.microsoft.com/graph/change-notifications-lifecycle-events), [latency](https://learn.microsoft.com/graph/change-notifications-overview#latency).

## Chosen design

1. Implement ADR-0024 in the same inbound-mail seam: use `ApprovedMailbox.Id` everywhere as the durable source key, retain Graph mailbox/folder values only as provider coordinates, record activation time and cursor-scope fingerprint, and remove the old re-key/adoption path. This is required groundwork, not compatibility work.
2. Store one minimal subscription row per enabled approved Inbox: approved-mailbox ID, Graph subscription ID, exact resource, expiry, lifecycle state, last maintenance time and bounded result code. Keep the shared clientState only in protected configuration.
3. Put one anonymous Graph protocol endpoint on the existing Web host. It validates the token or bounded notification batch, resolves the active subscription, and publishes mailbox/subscription identifiers plus the bounded lifecycle kind. It performs no Graph read or intake work.
4. Extend INTK-043's typed `intake-work` envelope with mailbox wake messages. `UnifiedWorkFunction` dispatches them to a targeted `PollApprovedInbox` entry point that reuses the existing lease, delta, retention, receipt and cursor implementation. The unified poison path records a mailbox wake failure; the recovery timer remains authoritative.
5. Rename the existing Inbox timer as recovery, run it every five minutes, and let that same caller perform only subscription maintenance that is due at the six-hour boundary before running the estate fallback. Do not add a maintenance Function.
6. Make no capacity decision in MAIL-013. Preserve the post-INTK-043 host configuration and measure Graph delivery separately from Pegasus callback, queue, delta and processing time.

## Current versus target

| Concern | Current | Target |
|---|---|---|
| Ordinary discovery | 15-second full-estate timer | Graph callback targets one mailbox |
| Worker route | Timer calls mailbox delta; downstream uses queued intake | Unified warm queue calls the same mailbox delta; downstream is unchanged |
| Recovery | Next 15-second tick | Existing timer at five minutes plus Graph retry/lifecycle |
| Queue/Function count | INTK-043 target: one work queue and one poison queue | Same queues and Functions; mailbox is another typed message |
| Mailbox identity | Graph identity keys operational state | `ApprovedMailbox.Id` keys state; Graph values are replaceable scope |
| Subscription maintenance | None | Due work every six hours inside the recovery caller |
| Sender/state | Retained-mail projection | Unchanged |
| Five-second claim | Polling adds up to 15 seconds | Pegasus-controlled stages are measured; Graph delivery is reported separately |

## Failure handling

- Invalid or unknown callback items queue nothing and receive a uniform response.
- If a valid wake cannot be queued, return 5xx so Graph retries.
- Duplicate callback, queue retry and fallback overlap converge on the existing mailbox lease and receipt idempotency.
- Removed/missed/reauthorization events schedule repair and the same delta pass; they do not create a second processor.
- Scope mismatch, Graph 410 or incomplete identity fails that mailbox closed and requires its explicit fresh-start activation.
- Migration establishes one target schema. No dual read/write, old identity alias or cursor-carrying compatibility path remains.

## Scope boundary

Included: stable inbound mailbox identity required by ADR-0024, Graph subscription/callback, unified-queue wake, targeted delta entry, five-minute recovery, due subscription maintenance, telemetry, migration, IaC and tests.

Excluded: identification/classification/extraction changes, manual-upload changes, sender/UI changes, Sent Items, rich notifications, Event Grid/Event Hubs, a new runtime, a new queue, a new mailbox Function, capacity changes, live subscription creation or deployment. DELIV-021 owns approved deployment and live latency/cost/recovery proof.

## Open questions

None. The governing documents and current pre-release state settle the design.
