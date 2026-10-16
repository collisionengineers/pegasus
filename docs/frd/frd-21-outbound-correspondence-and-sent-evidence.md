# FRD-21: Outbound correspondence and Sent evidence

> Owner capabilities: MAIL-12, MAIL-14 to MAIL-16, MAIL-25 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Pegasus only believes a report was sent when it finds the exact Sent item
  in an approved Outlook mailbox. A draft, an export or a staff note is not
  proof.
- Staff send Reply, Reply all, Forward, Send chaser and Send report from an
  Outgoing approved mailbox, never from their own address. There is no
  Compose and no default sender. Only signed-in staff with the casework right,
  or the Automation Actor under its `automation.send` scope, can send.
- Every send is one durable operation. Graph saying "accepted" means
  submitted, not sent. The Sent item that Graph later writes is the evidence.
- A report sent outside Pegasus is detected from the Sent mailbox, not
  asserted.
  If the match is unclear, the item waits for staff to link it.
- Staff have no Delete or Flag control. The one automatic move is the answered
  instruction going to Deleted Items after its report send is confirmed.
  It is recoverable.

## Purpose

Collision Engineers must be able to prove which report went to which
Principal, and when. This document sets the rules for that evidence and for
the mail staff send from Pegasus. Inbound mail and classification live in
[FRD-08](frd-08-email-mailbox-and-background-processing.md). The screens
for browsing mail live in [FRD-20](frd-20-mailbox-workspace.md).

## Behaviour

### Outbound correspondence evidence

Report-sent evidence links one exact, immutable Outlook Sent item to exactly
one Case. The mailbox must be on the Administrator's allowlist. On an
Inspection + Audit Case the evidence belongs to one report: the Inspection's
stays with the Inspection when Create audit runs, an Inspection report sent
afterwards links to the Inspection's own work from the Inspection view, and
the Audit report's must be of that report and sent after the Audit was
created. Automatic matching reads which report Pegasus's own send carried
and links the Sent item to that work; a send of the Inspection report after
Create audit never moves the Case's state (operator, 1 October 2026), and one
before Create audit records its Sent item on the Case and leaves it With
Engineer (operator, 13 October 2026). A send
that carried both reports links its one Sent item to both works, each by the
rules above, and unlinking the Audit's leaves the Inspection's (operator,
12 October 2026)
([FRD-13](frd-13-case-lifecycle-and-workflow.md#create-audit)).

The record keeps:

- the mailbox and Sent-folder scope;
- the immutable item identity and the conversation or reply-chain identity;
- Outlook's own `sentDateTime` as the authoritative sent time;
- the separate time Pegasus found the item and the time it was linked;
- who linked it, a person or the automatic matcher;
- the Case relationship and a reason where one is required;
- recipient and attachment evidence where available.

The message body is never stored in action history.

When automatic matching finds nothing, finds more than one candidate, runs
late, sees a duplicate or hits a conflict, the item stays unconfirmed. Any
authorised staff member may then link the exact item with a reason. Any staff
role may unlink or relink it, again with a reason. Earlier and current links
both stay in permanent history, and dependent events and counts are
recalculated from that history. Once confirmed, the event stays final even if
Outlook later moves or deletes the item.

Confirmation proves one thing: the exact item existed in the approved Sent
scope when it was confirmed. It does not prove the recipient received or
read it, that the content was right, or that the Case is complete. Preparing,
viewing, copying or acknowledging a chaser or any other message is not
evidence of sending. A staff note that says "sent" stays an assertion unless
the exact external evidence is retained.

A management event is recorded after each report or fee-note send. This is
`MAIL-17`'s management-event clause, a ruling awaiting delivery (operator
ruling, 15 October 2026).

**Tidying the answered instruction.** A report send that replied to a retained
instruction is due for one move once its Sent item is confirmed. The Worker
moves that instruction to the Deleted Items folder of the mailbox it sits in,
within about five minutes, through the same recovery run as the Inbox poll.
It records what happened on the send: moved, already in Deleted Items, no
longer in the mailbox, or failed. A move writes the Case history event
`instruction_moved_to_deleted_items`; each failure writes
`instruction_move_failed` with a failure code, and after three failures
Pegasus stops and leaves the instruction where it is. A mailbox asking Pegasus
to slow down is not a failure: the run stops and the move is tried again later.
A mailbox that is no longer Approved is left alone. The tidy never moves
anything but that instruction, never deletes it permanently, never runs for a
send that is not a report, a send that is not confirmed Sent, or a report
sent without a retained instruction, and never changes the Case, its state or
the retained message's evidence. Web cannot perform it, and an environment
without the Worker's mailbox access leaves the instruction where it is
(`mailbox_not_permitted`).

Triage completion is based on the recorded outcome, under
[FRD-03](frd-03-triage.md). Its optional Reply with finding is a staff send
with purpose `TriageOutcomeReply`: a reply to the Triage's origin message
that opens with a preset outcome body. Its Sent item links to the Triage
Case as response evidence, like any exact reply to that message, when the
Triage has no response evidence yet; a Triage keeps one response link. It is
never a completion requirement. A manual Triage chaser is a staff send like any
other, and its exact Sent evidence is recorded the same way; it is never a
completion requirement either.

Local development must never change a mailbox. A Worker project, queue
registration or timer setting does not prove that a real caller ran.

### Outbound correspondence

**One durable operation per send.** Each send is keyed by its sender (a
member of staff or the Automation Actor, recorded as which), the mailbox and
an operation key, with a payload hash that the server
calculates. Reusing a key with a different payload fails. Draft creation and
upload progress survive a restart without a second send attempt. If the
provider's answer is ambiguous, the send stays Unknown until exact evidence
settles it.

**Submitted is not Sent.** Graph `202 Accepted` means Submitted. Sent needs
the retained-MIME pipeline to match the immutable item, the operation marker,
the mailbox generation and the attachment hashes. The operation marker is the
Message-ID Pegasus assigns to every send (`<operation id>@pegasus.invalid`),
or the X-Pegasus headers where the provider keeps them; the mailbox and its
generation are the ones the Sent item was read from. The provider's sent time
and the time Pegasus observed the item stay separate facts. Each enabled Sent
mailbox has its own cursor and activation boundary. Old items move the cursor
forward but are not backfilled. One failing mailbox does not block the
others. The composer shows Submitted until the Worker's Sent-evidence poll
observes the item; there is no staff action that re-checks or re-sends.

**Report sends are rechecked at the last moment.** Immediately before the
provider call, Pegasus revalidates the Case's persisted report readiness, the
exact generation, its versions and artifacts, the sender's authority and the
mailbox generation. The only connector that may trigger it is the Automation
Actor's `pegasus_report_send`
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#sending)).

**Where the controls are.** Reply, Reply all and Forward appear on the Inbox
message and on the Case correspondence surface. Reply also appears on Inbox
list rows, in the Inbox preview pane
([FRD-20](frd-20-mailbox-workspace.md#quick-preview-and-message-detail)) and
on a closed Unidentified item's record, where Request again is that same
reply while the item is open
([FRD-15](frd-15-work-centre-queues-and-search.md#pre-case-records)); each
opens the message record's Reply. Every one appears only when the mailbox the
email arrived in can send. Send chaser is on the Case record
([FRD-16](frd-16-case-record-workspace.md#actions-menu)) and Send report on
the Case's Report section
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md)). There is
no Compose: Pegasus starts no new correspondence that is not a reply, a
forward, a chaser or a report (operator, 12 October 2026). The controls
exist only when the outbound capability is composed into the application.
Otherwise those surfaces show no send control and no composer. The technical
decision is [ADR-0036](../adr/0036-outbound-mail-via-approved-mailbox.md).

**Also resolve query.** A Reply or Reply all filed against a Case that holds
an open query offers one tick, **Also resolve query** (operator, 11 October
2026). It is ticked by default only when the composer was opened from the
Case's Resolve query Next action or on an AI draft
([FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list)).
The tick is kept on the send; when the
Sent item is observed, every open query on the Case resolves
([FRD-13](frd-13-case-lifecycle-and-workflow.md#report-sent-and-queries)).
A reply without it resolves nothing. Forward and Send chaser take no tick.

**Who may send.** A signed-in staff member with the casework right, or the
Automation Actor through `pegasus_mail_send` and `pegasus_report_send` under
its `automation.send` scope (operator, 7 October 2026;
[ADR-0064](../adr/0064-automation-actor-staff-casework-parity.md)). Both use
this one operation from the same approved mailboxes; the operation, its
history and the Case's Notes line record which of them sent it, and an
Automation send never reads as a member of staff. The sender must still be
current at each step that reaches the provider: a staff account still enabled
with a casework role, or the Automation client registration still enabled.
There is no automatic or scheduled send.

**From which mailbox.** Only an Outgoing approved mailbox sends
([FRD-26](frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md#mailbox-allowlist-activation-and-wipe)),
and only while it is send-ready: Approved, activated, its identity and Sent
Items bound. Reply and Forward send as the approved mailbox that holds the
retained message. A Case's report and Send chaser are a reply to the Case's
instruction email, the retained mailbox message its origin receipt came
from, sent as the approved mailbox that email arrived in while that mailbox
can send. When the Case has no instruction email (a manual Case, an uploaded
`.eml` or any other non-mailbox origin) or its mailbox can no longer send,
staff choose an Outgoing mailbox as From and the message is new (operator,
12 October 2026); for a report the choice starts on the Principal's Send from
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-sending-rules)).
Pegasus never silently picks a mailbox. The sender is never
a staff member's own address or any mailbox outside the allowlist; a reply
shows its sender read-only. There is no default sender.

**One size limit.** Every Outgoing mailbox sends under one fixed limit of
35 MB, 36,700,160 encoded bytes (operator, 12 October 2026); there is no
per-mailbox limit.

**What the composer carries.** To, Subject, Message, Case / PO and From.
A reply opened on an AI draft (**Reply with AI draft**,
[FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list))
opens with the draft as its Message and one line above it, marked AI, saying
when and by which client the draft was written and that staff edit it before
sending; the send carries the job, and a send the provider accepts completes
it. A send that has gone but leaves the job as it was shows "The AI draft job
was not completed by this send: {reason}." beside its send status (operator,
16 October 2026). A job that is no longer that message's Draft ready reply
prefills nothing.
The one Case / PO field searches as staff type, by Case / PO,
registration, claimant, claim number or Principal; choosing a Case from its
list is the server action that fixes the Case and its version for the send,
and a Case / PO reference typed in full sends against that Case as it stands.
Reply and Forward keep the retained message's reply
chain and conversation identity. Case defaults to the Case whose correspondence the
Reply or Forward started from, otherwise to the message's current
association, and may be changed before sending. A Triage outcome reply's
Message opens with the Triage outcome template from Administration, rendered
from the Triage's registration and finding
([FRD-17](frd-17-administration-workspace.md#e-mail-templates)); staff edit it
before Send. A Case chaser opened from the Case's Send chaser is a staff
send with purpose `CaseChaser` on its own chaser composer
(`/Cases/{id}/Chaser`): a reply to the Case's instruction email from the
mailbox it arrived in, or, with no usable instruction email, a new message
from the Outgoing mailbox staff choose as From (see From which mailbox). To
is the sender of the Case's instruction, the sender of each image-intake
e-mail paired to the Case and the repairer's directory e-mail, each once;
Subject is "Re:" and the instruction's subject for a reply, or opens with
the registration and claimant for a new message; Message is the Case chaser
template rendered from the Case
([FRD-16](frd-16-case-record-workspace.md#actions-menu)). Staff edit all
three before Send. Its exact Sent evidence is recorded like any other send,
and on a Not ready Case it records the chase
([FRD-13](frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing)).

**Reply targets.** Pegasus keeps the structured MIME Reply-To addresses in
their original order and uses the From addresses only when Reply-To is
absent. A Reply-To header that is present but unusable gives an empty list.
The reply screen shows the retained targets and the chosen recipients so
staff can confirm them. If the retained metadata is missing or the target
list is empty, Reply and Reply all are refused. The transport Sender, To and
Cc are never used as substitutes. The Triage Case composer
([FRD-15](frd-15-work-centre-queues-and-search.md#the-triage-case-page)) has
one exception: when its request is a Collision Engineers staff forward (the
transport sender is on the staff domain) and the forward names one external
original sender, To is that original sender, not the forwarding desk, and
the forward's leading "FW:" or "Fwd:" is dropped before "Re:". A forward
inside the Principal's own organisation is not unwrapped and answers its own
Reply-To or From (operator, issue 1042, 7 October 2026).

**What is retained.** The immutable Sent item that Graph writes is the
evidence. The Sent-evidence poll retains it under the rules in
[Outbound correspondence evidence](#outbound-correspondence-evidence), lists
it under the Inbox's Sent Items scope
([FRD-20](frd-20-mailbox-workspace.md#inbox-scopes-and-filters)) and links it
to the Case named at send time: the Case's Correspondence shows it as a Sent
item and, for a general correspondence send, its Notes record that the
correspondence was sent, with the subject
([FRD-16](frd-16-case-record-workspace.md#files)). Draft text is not evidence
until that Sent item exists. A send that Graph refuses leaves no evidence and
shows as a failure on the composer, never as sent.

**No Flag, no Delete control. One automatic move.** There is no flag control,
no delete control and no staff action that moves a message to Deleted Items,
on any surface. The one exception is not a control: after a report send is
confirmed, Pegasus moves the instruction it answered to Deleted Items
([ADR-0071](../adr/0071-move-answered-instruction-to-deleted-items.md)). No
surface, action or tool deletes a mailbox item permanently. Dismissing a
message is Pegasus data only and is defined in
[FRD-20](frd-20-mailbox-workspace.md#dismiss).

Local development and every test profile use the unavailable implementation
of the send seam and never change a mailbox. Turning sending on in
production is a separately approved live write. A mailbox sends only as an
Outgoing mailbox, whose Sent Items the Sent-evidence poll reads, so every
send has its evidence.

### Externally sent report detection

A report sent from the approved mailbox outside Pegasus is detected, not
asserted. The Sent-evidence poll recognises a report mail in the approved
mailbox when the exact Sent item matches one Case reference and carries a
PDF attachment classified as a report. On that match Pegasus:

1. attaches the PDF to the Case as the report document;
2. links the Sent item as `Report sent` evidence under
   [Outbound correspondence evidence](#outbound-correspondence-evidence);
3. records the report-sent event on the Case. What that event does to the
   Case state is defined in
   [FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels). The
   Case's closure outcome stays a separate, reasoned step.

If the Sent item matches no Case reference, matches several Cases, matches a
Case that already holds report-sent evidence, or has no attachment
classified as a report, it is retained unlinked. It then appears on the Case
for staff to confirm through the ordinary reasoned link. It never completes
a Case on its own. There is no manual "report sent" claim without the Sent
item.

## States and transitions

| Thing | States |
| --- | --- |
| A send operation | Submitted (Graph `202`), Sent (immutable item matched), Unknown (ambiguous provider write), Failed (Graph refused) |
| A Sent item as evidence | Unlinked, Linked to one Case, Unlinked again with a reason, Relinked with a reason; every step stays in history |

A `Report sent` link on a Case feeds the Case lifecycle in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels). This
document never moves a Case by itself.

## Edge cases and fail-closed behaviour

- A Case report or chaser with no usable instruction email and no From
  chosen: refused; with no Outgoing mailbox able to send, it cannot be sent.
- Reply-To missing and From unusable: Reply and Reply all are refused.
- Graph refuses the send: nothing is recorded as sent; the composer shows
  the failure.
- The same operation key reused with a different payload: refused.
- A Sent item with zero or several Case matches, or a Case that already has
  report-sent evidence: retained unlinked, waits for staff.
- Outlook later moves or deletes a confirmed Sent item: the confirmed event
  stays final.
- A mailbox that fails to poll: other Sent mailboxes carry on.
- A matched send whose observation fails: the poll records no outcome for the
  item, so the next poll reads it again and the send does not stay Submitted
  (QDOS26099, 12 October 2026).
- The answered instruction cannot be moved (permission refused, Graph error):
  three attempts, each in Case history, then it stays where it is.

## Acceptance evidence

- A real Graph send from an approved mailbox on the approved test mailbox,
  followed by the Sent-evidence poll retaining and linking the exact item.
- A Sent item that matches one Case reference with a report PDF, linked
  automatically; the same item with two candidate Cases, left unlinked.
- A refused Graph send that leaves no `Report sent` evidence.
- Test profiles proving the send seam is unavailable and no mailbox changes.
- Deployment and live acceptance are separate evidence tiers
  ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `MAIL-12`, `MAIL-14`–`MAIL-16`, `MAIL-25` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-08](frd-08-email-mailbox-and-background-processing.md)
  (inbound mail and classification),
  [FRD-20](frd-20-mailbox-workspace.md) (mail screens),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md) (Case states),
  [FRD-03](frd-03-triage.md) (Reply with finding),
  [FRD-07](frd-07-case-export.md) (Case export).
- Technical constraints:
  [ADR-0036](../adr/0036-outbound-mail-via-approved-mailbox.md) (outbound
  mail via an approved mailbox),
  [ADR-0052](../adr/0052-dismiss-by-logical-folder.md) (dismiss by logical
  folder),
  [ADR-0071](../adr/0071-move-answered-instruction-to-deleted-items.md)
  (answered instruction to Deleted Items),
  [ADR-0044](../adr/0044-mail-occurrence-and-business-identity.md) (mail identity).
