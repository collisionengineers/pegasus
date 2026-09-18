# FRD-26: Mailbox allowlist, activation, wake-up and recovery

> Owner capabilities: INT-02, INT-05 to INT-07, INT-33 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- The Administrator-managed allowlist decides which mailboxes Pegasus reads.
  Deployment configuration does not.
- Each mailbox has its own lease, cursor and fresh-start activation time.
  Mail received before that time is never retained.
- Disabling a mailbox deletes nothing. Re-enabling starts a new cycle, so
  mail received while disabled never becomes a backlog.
- An authorised intake-data wipe records a receive-time cutoff. Nothing can
  lower it or bring cleared mail back.
- Graph notifications only wake the Worker. The Worker alone reads mail, and
  a five-minute fallback poll recovers anything a notification missed.

## Purpose

Mail intake must be near real time, durable and limited to mailboxes the
business has approved. This document owns which mailboxes the Worker reads,
when each one starts, what an intake-data wipe does to the mailbox boundary,
and how Graph notifications, subscription maintenance and the fallback poll
keep each mailbox current. Message identity, classification and automatic
Case association are in
[FRD-08](frd-08-email-mailbox-and-background-processing.md). Intake
receipts and source occurrence identity are in
[FRD-02](frd-02-intake-and-source-identity.md#source-occurrence-and-dispatch-identity).
Who may manage the allowlist is in
[FRD-04](frd-04-parties-accounts-and-access.md#staff-role-access-matrix).
The mail screens are in [FRD-20](frd-20-mailbox-workspace.md).

## Behaviour

### Mailbox allowlist, activation and wipe

The approved mailbox allowlist, not deployment configuration, decides which
mailboxes an Outlook/Graph route reads. `ApprovedMailbox.Id` is the durable
identity. The Graph mailbox and folder coordinates are replaceable cursor
scope. Each mailbox holds its own lease and its own cursor, so one mailbox's
failure or backlog never affects another.

The accepted intake target is four business mailboxes:
`instructions@collisionengineers.co.uk` (the first caller, INT-02),
`desk@collisionengineers.co.uk` (INT-05),
`engineers@collisionengineers.co.uk` (INT-06) and
`info@collisionengineers.co.uk` (INT-07). Each is processed only while it is
on the Administrator-managed allowlist and activated under the rules of this
section. The addresses are data, not code.

Each mailbox has its own fresh-start activation cycle. Enabling starts a new
cycle at a recorded UTC activation time. Mail received before that time
moves the cursor forward but is not retained, quarantined, passed to intake
or allocated. Disabling a mailbox stops polling at the next tick and deletes
nothing: retained messages, receipts, assets, quarantined items and Case
associations all stay visible. Re-enabling starts a new fresh-start cycle
rather than resuming the old cursor, so mail received while disabled never
becomes a backlog.

The global Worker switch, individual function switches and per-mailbox
switches are separate. Sent-evidence polling stays off unless separately
approved. Approving a mailbox in Pegasus grants no Exchange access; the
Microsoft 365 tenant must separately admit the application to that mailbox.
Until it does, polling that mailbox fails and says so. An approved intake
mailbox becomes pollable only after an address check has stored its mailbox
identity and activation time. Before that, the administration list reports
it as not activated, not as awaiting a first poll, because no poll is
pending.

**Wipe.** An explicitly authorised intake-data wipe records one UTC
receive-time cutoff in the existing Inbox poll state, in the same
transaction that clears the SQL data. It does not change mailbox identity,
approval, onboarding time or subscriptions. The effective start is the later
of activation and that cutoff, including for a mailbox never yet polled.
Clearing occurrence identities, rebinding cursor scope, resetting an expired
Graph delta token or receiving an old queued notification must never lower
the cutoff or bring cleared mail back. Mail received at or after the cutoff
stays eligible, and forwarding an old email creates a newly received message
that is evaluated normally. An ordinary deployment or Worker restart never
moves this boundary. Wipes run only with the Worker stopped and application
writes excluded for maintenance.

### Mailbox wake-up and recovery

Each enabled approved Inbox has one Microsoft Graph basic change-notification
subscription. Its record uses the approved-mailbox identity and stores the
Graph subscription id, resource scope, expiry, lifecycle state and last
maintenance result in SQL. The shared client-state secret is protected
configuration. It is never stored in the subscription row, logged, sent to
a browser or put on a queue.

`POST /hooks/microsoft-graph/mail` on the Web host is a wake-up endpoint,
not a mail reader. For Graph validation it returns the validation token as
plain text within the protocol deadline. For a notification it checks the
clientState and the known active subscription, queues only the subscription
and approved-mailbox identifiers, and acknowledges promptly. It does not
download, classify, extract, associate, allocate or change a message.
Unknown, expired, malformed or wrongly scoped notifications fail closed
without queuing work or revealing the secret.

The Worker alone owns the mailbox lease, the cursor or delta read,
retention, the shared intake call and the retry outcome. A creation
notification with an immutable message ID fetches and processes that exact
message only, still subject to the receive-time cutoff. It does not scan the
Inbox or move the recovery cursor. Duplicate notifications are safe. A
notification may name a message the mail source cannot show yet, because
the tenant accepted it before replicating it to the folder the delta reads.
That empty read is not a failure: the cursor stays where it was, the message
is still ahead of the next delta sweep, and the mailbox-scoped source
identity that both routes derive keeps exactly one occurrence however many
notifications arrive. Lifecycle `missed`, `subscriptionRemoved` and
reauthorization events schedule the same delta resynchronisation; they are
not another processing route.

Subscription maintenance runs every six hours and renews an enabled Inbox
before it comes within 48 hours of expiry. A failure is visible per mailbox
and leaves the five-minute per-mailbox fallback poll running. That fallback
moves the same cursor and is recovery only: it creates no second receipt and
does not bypass the fresh-start boundary. Disabling a mailbox stops
notification work at claim time as well as its next fallback poll.

Before activation, an Outlook/Graph route must:

- use an approved test or live mailbox and an exact operation;
- keep message, conversation, folder, attachment, sender and recipient, and
  received and sent identity;
- keep a durable cursor or checkpoint and process each occurrence
  idempotently;
- separate read and intake scopes from draft, send and administrative
  scopes;
- queue only stable work identifiers, never full source payloads;
- record poison, retry, dead-letter and operator recovery behaviour;
- prove the real Worker timer or queue caller;
- obtain exact Sent-item and reply-chain evidence wherever delivery is part
  of a completion gate.

## States and transitions

| Thing | States |
| --- | --- |
| An approved mailbox | not activated; enabled with a UTC activation time; disabled (nothing deleted); re-enabled with a new cycle |
| A Graph subscription | active; within 48 hours of expiry and due for renewal; `missed`, `subscriptionRemoved` or reauthorization pending resynchronisation |

The states of a retained message are owned by
[FRD-08](frd-08-email-mailbox-and-background-processing.md#states-and-transitions).

## Edge cases and fail-closed behaviour

- Mail received before activation or before a wipe cutoff: cursor moves,
  nothing retained.
- A notification for a message the delta cannot show yet: not a failure;
  the next sweep picks it up.
- A tenant that has not admitted the application: that mailbox alone fails
  and says so.
- An unknown, expired, malformed or wrongly scoped notification: refused
  without queuing work or revealing the secret.

## Acceptance evidence

- Persistence tests for activation, disable, re-enable and wipe cutoff
  behaviour on the cursor.
- A real Graph subscription validation and notification against the
  approved test mailbox, proving the Worker processes the exact message and
  the endpoint touches no mail.
- Deployment and live acceptance are separate evidence tiers
  ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `INT-02`, `INT-05`–`INT-07`, `INT-33` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md) (intake
  receipts, source occurrence identity),
  [FRD-04](frd-04-parties-accounts-and-access.md) (who manages the
  allowlist),
  [FRD-08](frd-08-email-mailbox-and-background-processing.md) (mail
  identity, classification, Case association),
  [FRD-20](frd-20-mailbox-workspace.md) (mail screens),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md) (sending
  and Sent evidence).
- Technical constraints:
  [ADR-0044](../adr/0044-mail-occurrence-and-business-identity.md) (mail
  identity separation).
