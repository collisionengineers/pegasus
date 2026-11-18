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
- Staff have no Delete or Flag control. Two moves send mail to Deleted Items,
  both recoverable: the answered instruction, automatically, after its report
  send is confirmed; and the Case's received e-mails after a send that staff
  ticked **Delete case correspondence** on (9 November 2026, R902).

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

A management event is recorded after each report or fee-note send: when the
send's Sent item is observed, one action history row on the Case,
`management_event`, records the sender, the documents the send carried by
kind (one report, or two) and how many distinct recipients it had. A send
that ends Failed, Cancelled or Unknown records none. It is read in Action
logs alone ([FRD-17](frd-17-administration-workspace.md#logs)), which only
an Administrator sees. This is `MAIL-17`'s management-event clause (operator
rulings, 15 and 22 October 2026).

**Tidying the answered instruction.** A report send that replied to a retained
instruction is due for one move once its Sent item is confirmed. The Worker
moves that instruction to the Deleted Items folder of the mailbox it sits in,
within about two minutes of Send, on the recovery timer's every-minute run
(12 November 2026, R986; it ran every fifth minute before).
It records what happened on the send: moved, already in Deleted Items, no
longer in the mailbox, or failed. A move writes the Case history event
`instruction_moved_to_deleted_items`; each failure writes
`instruction_move_failed` with a failure code, and after three failures
Pegasus stops and leaves the instruction where it is. Both are shown on the
Case's Notes tab, as **Instruction moved to Deleted Items** and
**Instruction not moved** (11 November 2026, R959). A mailbox asking Pegasus
to slow down is not a failure: the run stops and the move is tried again later.
A mailbox that is no longer Approved, or is not activated, is left alone
(11 November 2026, R961). The tidy never moves
anything but that instruction, never deletes it permanently, never runs for a
send that is not a report, a send that is not confirmed Sent, or a report
sent without a retained instruction, and never changes the Case, its state or
the retained message's evidence. Web cannot perform it, and an environment
without the Worker's mailbox access leaves the instruction where it is
(`mailbox_not_permitted`).

**Delete case correspondence.** Send report, a reply that answers a query and
the Triage Case's Reply with finding each carry one optional tick, **Delete
case correspondence**, unticked each time the form opens (operator,
9 November 2026; R902 to R905): a tick left on a dialog that was closed
without sending is cleared when the dialog next opens (10 November 2026,
R916), and a refused send comes back with it clear on all three, for staff
to tick again (11 November 2026, R960). The reply composer offers it exactly where it
offers Mark query done: on a Reply or Reply all to a message that holds an
open query on the Case (R904). Forward, Send chaser and the Triage chaser
never offer it, and a send of any other kind that names it is refused. The
Web reply composer does not refuse a reply whose query was resolved between
opening the form and Send: the reply is sent and both ticks are dropped,
while the Automation tool refuses a call that names Delete case
correspondence there (operator, 16 November 2026; [R1072](../rulings.md)). The
tick is kept on the send. Once the Sent item is confirmed, the Worker moves
the Case's received e-mails to the Deleted Items folder of the mailbox each
sits in (R902), within about two minutes of Send, in the same every-minute
recovery run as the instruction tidy and after it (12 November 2026, R986). The received e-mails are the Inbox-scope
retained messages linked to the Case now, with the request e-mail a Triage
Case was opened from, that sit in a mailbox still Approved and activated
(10 November 2026, R916) and that Pegasus held when the send was prepared; a
message dismissed in Pegasus is among them. Where one of them is a Collision
Engineers staff forward held in instructions@ (R586, R651) or, on the test
instance, in pegasustest@ (11 November 2026, R956), the original it
was made from goes too, whether or not that original is linked to the Case: the
one Inbox message of the mailbox that sent the forward which the forward
answers, named by its In-Reply-To, or by the last of its References where it
has none (10 November 2026, R917). The earlier messages of that thread are
not the original and go only when they are themselves linked to the Case
(11 November 2026, R957). An original that stands linked to another Case is
left (R958). The original goes whether or not the mailbox that forwarded it
is read for Incoming mail: where Pegasus holds no copy of it, the Worker
finds it in that mailbox by the Message-ID the forward answers, wherever it
is filed, and moves it; the mailbox must still be Approved and activated
(R916), and its own Sent Items copy is left (R903) (operator, 11 November
2026; [R975](../rulings.md)). A forward sent as an attachment names no original
and is moved alone (operator, 16 November 2026; [R1073](../rulings.md)).
Pegasus's own Sent items, uploaded `.eml` files and
mail that arrives after the send are left (R903, R962). The move is recoverable and changes nothing
in Pegasus: the retained copies, their links, the Case, its state and its
correspondence view stay as they are. A send that is not confirmed Sent
moves nothing. The send records each run. A run with no failure ends the
tidy and, when a message was moved or was already in Deleted Items, writes
the Case history event `correspondence_moved_to_deleted_items` with how many
were moved, already there and no longer in the mailbox. A run in which a
message could not be moved writes `correspondence_move_failed` with a
failure code, and the send is tried again in a later run, which finds the
messages already moved where they are; after three such runs Pegasus stops
and leaves the rest. Both events are shown on the Case's Notes tab, and on
the Triage Case's for a Reply with finding, as **Case e-mails moved to
Deleted Items** and **Case e-mails not moved** with those sentences
(11 November 2026, R959); neither takes a Case or Triage version. A mailbox
asking Pegasus to slow down is not a failure.
Two ticked sends on one Case are tidied one after the other. Web cannot
perform the move. `pegasus_report_send` and `pegasus_mail_send` take
`deleteCaseCorrespondence` under the same rule (R905;
[FRD-10](frd-10-mcp-automation-and-actor-boundary.md)).

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

**An Unknown send is shown where it was sent from.** When Graph answers a
staff send with a server error, `408` or `429`, or does not answer in time,
the send is recorded Unknown and staff stay on the page they sent from, the
Inbox reply, Send report or the Triage reply, and see "The send result is not
yet known." with the send's status, never the error page (operator, 16
November 2026; [R1069](../rulings.md)). The Case chaser and the photo link
Send and Resend do the same, the sentence beside the Send status those pages
already show; the Automation send tools answer the Unknown operation as they
answer any send's state, never a failed call (operator, 17 November 2026;
[R1123](../rulings.md)). An Unknown send stays Unknown until
the Sent Items check finds it, when it becomes Sent; nothing else settles,
cancels, lists or counts it, and staff send again if the recipient did not get
it (operator, 16 November 2026; [R1070](../rulings.md)). An Unknown send
does not block another: the Triage reply, like the Inbox reply, offers the
composer again under a new operation key, while a send still under way blocks
it; the same key replays the Unknown send and never sends again (operator,
17 November 2026; [R1122](../rulings.md)). A message's send status shows its
most recent send, so a later send that reached Sent reads Sent where an
earlier one is still Unknown; a send still under way is shown first (operator,
17 November 2026; [R1128](../rulings.md)).

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
exact generation, its versions and artifacts, each Case file the send
attaches as that file is filed now (a filed estimate, or an offered Case file
staff ticked; [R1151](../rulings.md)), the sender's authority and the
mailbox generation. The only connector that may trigger it is the Automation
Actor's `pegasus_report_send`
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#sending)).

**Where the controls are.** Reply, Reply all and Forward appear on the Inbox
message, which opens as a dialog over the Inbox and composes inside it (v41
items I2 and I15, signed off 19 October 2026,
[v41 notes](https://gitlab.com/collisionengineers-group/pegasus-design-archive/-/blob/main/v41_planning/notes/v41-notes.md);
[FRD-20](frd-20-mailbox-workspace.md#message-dialog-and-message-detail)), and
on the Case correspondence surface. Reply also appears on Inbox
list rows and on a closed Unidentified item's record, where Request again is
that same reply while the item is open
([FRD-15](frd-15-work-centre-queues-and-search.md#pre-case-records)); each
opens the message record's Reply. Forward appears only when the mailbox the
email arrived in can send; Reply and Reply all appear while any Outgoing
mailbox can (4 November 2026, R655). Send chaser is on the Case record
([FRD-16](frd-16-case-record-workspace.md#actions-menu)) and Send report on
the Case's Report section, which opens the report composer dialog while the
Case is being edited (operator, 4 November 2026, R665, R668;
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md)). There is
no Compose: Pegasus starts no new correspondence that is not a reply, a
forward, a chaser, a report or a photo link e-mail (operator, 12 October
2026; 9 November 2026, [R773](../rulings.md)). The controls
exist only when the outbound capability is composed into the application.
Otherwise those surfaces show no send control and no composer. The technical
decision is [ADR-0036](../adr/0036-outbound-mail-via-approved-mailbox.md).

**Mark query done.** A Reply or Reply all to a message that holds an open
query offers one tick, **Mark query done** (operator, 27 October 2026;
[ADR-0085](../adr/0085-mail-is-instructions-or-correspondence-and-queries-are-assignable.md)).
It is ticked by default when the composer was opened from Answer query or on
an AI draft of that message
([FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list)).
The tick is kept on the send; when the Sent item is observed, only that
message's query is marked done
([FRD-13](frd-13-case-lifecycle-and-workflow.md#report-sent-and-queries)).
A reply without it marks nothing done. Forward and Send chaser take no tick.

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
Items bound. From is a default, not a rule: every send but a forward leaves
the send-ready Outgoing mailbox staff choose, starting on the default this
paragraph names (operator, 4 November 2026; R655). Reply and Reply all start
on the approved mailbox that holds the retained message while it can send,
otherwise on the Principal's Send from of the message's Case, and Forward
always sends as it, with one exception to the start: a Collision Engineers staff forward
held in instructions@ (the route of R586) is answered as though it had
arrived in the mailbox that forwarded it; a staff forward held in any other
mailbox is answered as any message is (3 November 2026, R652). Reply, Reply all, a Case's report and Send chaser
answering one start on that mailbox while it can send, otherwise on the
Principal's Send from. A reply that leaves another mailbox than the one
holding the message goes as a new message to the same recipients, since
only the holding mailbox has a copy to reply in, and is still recorded as the
reply to that message (R651, R655). A forward always leaves the mailbox that holds the message (operator,
3 November 2026; R651; kept by R655). A Case's report and Send chaser are a reply to the Case's
instruction email, the retained mailbox message its origin receipt came
from, from whichever Outgoing mailbox staff choose, starting on the approved
mailbox that email arrived in while that mailbox can send (R655). When the
Case has no instruction email (a manual Case, an uploaded `.eml` or any other
non-mailbox origin) the message is new (operator, 12 October 2026). With no
instruction email, or one whose mailbox can no longer send, From starts on
the Principal's Send from (3 November 2026, R654)
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-sending-rules)).
Pegasus never silently picks a mailbox: every send but a forward shows its
From as a choice, starting on that default (R655). The sender is never
a staff member's own address or any mailbox outside the allowlist.

**One size limit.** Every Outgoing mailbox sends under one fixed limit of
35 MB, 36,700,160 encoded bytes (operator, 12 October 2026); there is no
per-mailbox limit.

**What the composer carries.** To, Subject, Message, Case / PO and From. The
report composer carries From, To and Cc (each box after the first To
removable), Subject prefilled with the derived subject, Message, and Files,
one row per document with its include tick, its name as its preview and
Rename for the generation's own documents (operator, 4 November 2026, R665,
R667, R669;
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#companion-documents-and-what-a-delivery-attaches)).
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
(`/Cases/{id}/Chaser`): a reply to the Case's instruction email, or, with
no instruction email, a new message, from the Outgoing mailbox staff choose
as From, starting on the default (see From which mailbox; R652, R655). To
is the sender of the Case's instruction, the sender of each image-intake
e-mail paired to the Case and the repairer's directory e-mail, each once;
Subject is "Re:" and the instruction's subject for a reply, or opens with
the registration and claimant for a new message; Message is the Case chaser
template rendered from the Case
([FRD-16](frd-16-case-record-workspace.md#actions-menu)). Staff edit all
three before Send. Its exact Sent evidence is recorded like any other send,
and on a Not ready Case it records the chase
([FRD-13](frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing)).

**Photo link e-mail.** Send photo link
([FRD-28](frd-28-guided-photo-capture.md#photo-links)) sends a staff send
with purpose `PhotoLink`: a new message to the customer's e-mail from an
Outgoing approved mailbox, its Message the Photo link template rendered for
the link ([FRD-28](frd-28-guided-photo-capture.md#the-photo-link-e-mail)).
It is recorded like any other send. **Resend**, one act since the former
Remind sent the same message ([R914](../rulings.md)), opens the composer and
sends only on **Send by e-mail**; no button sends an e-mail on its own
(operator, 10 November 2026; [R915](../rulings.md)). Pegasus never sends a
photo link by itself. The composer may also text it from Pegasus's own UK
mobile number; a text is not mail and has no Sent evidence, and the link's
history records it ([FRD-28](frd-28-guided-photo-capture.md#the-photo-link-text);
[R918](../rulings.md) to R921). WhatsApp is Copy link and the staff
member's own phone (operator, 9 November 2026, [R773](../rulings.md)).
A capture a standing or website link started is never sent again: **Ask
for it** is retired (operator, 11 November 2026; [R948](../rulings.md)).

**Reply targets.** Pegasus keeps the structured MIME Reply-To addresses in
their original order and uses the From addresses only when Reply-To is
absent. A Reply-To header that is present but unusable gives an empty list.
The reply screen shows the retained targets and the chosen recipients so
staff can confirm them. If the retained metadata is missing or the target
list is empty, Reply and Reply all are refused. The transport Sender, To and
Cc are never used as substitutes. There is one exception: when the message
is a Collision Engineers staff forward (the transport sender is on the staff
domain) held in instructions@ and the forward names one external original
sender, it is answered as though the original had arrived directly. To is
that original sender, not the forwarding desk; Reply all adds the original
message's own To and Cc, read from the forwarded header, less the sending and
the forwarding mailbox (3 November 2026, R653); and the forward's
leading "FW:" or "Fwd:" is dropped before "Re:". The Triage Case composer
answers a staff forward held in any mailbox to its original sender, without
its "FW:". A forward inside the
Principal's own organisation is not unwrapped and answers its own Reply-To or
From (operator, issue 1042, 7 October 2026, first for the Triage Case
composer, [FRD-15](frd-15-work-centre-queues-and-search.md#the-triage-case-page);
every reply to one held in instructions@, 3 November 2026, R651, R652).

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

**No Flag, no Delete control. Two moves to Deleted Items.** There is no flag
control, no delete control and no staff action that moves a chosen message to
Deleted Items, on any surface. Two things move mail there and neither is a
control on a message: after a report send is confirmed, Pegasus moves the
instruction it answered; and after a send that staff ticked **Delete case
correspondence** on is confirmed, Pegasus moves the Case's received e-mails
(9 November 2026, R902 to R905;
[ADR-0071](../adr/0071-move-answered-instruction-to-deleted-items.md)). No
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
- A Sent-evidence poll cut off before it finishes, by a Graph time-out or
  the Worker stopping: that mailbox records `sent_evidence_poll_failure` like
  any other failed poll, and a time-out on one mailbox does not fail the run
  for the others (operator, 16 November 2026; R1107, R1108).
- A matched send whose observation fails: the poll records no outcome for the
  item, so the next poll reads it again and the send does not stay Submitted
  (QDOS26099, 12 October 2026).
- A Sent item the provider presents again at another position in its page:
  the recorded outcome replays as a no-op, and the mailbox's cursor moves
  only at a page boundary, so a run that fails re-reads its page and the poll
  is not held on the item (a.QDOS26157, 4 November 2026).
- The answered instruction cannot be moved (permission refused, Graph error):
  three attempts, each in Case history, then it stays where it is.
- One of a ticked send's Case e-mails cannot be moved: the others are moved;
  three runs, each in Case history, then the rest stay where they are.
- A ticked send whose Case has no received e-mail in an approved mailbox: the
  tidy ends and writes no history.
- An e-mail that arrives on the Case after a ticked send was prepared: it
  stays in the Inbox.
- A staff forward whose original Pegasus never retained, because the
  mailbox that forwarded it is not read for Incoming mail or the original
  arrived before it was: the original is found in that mailbox by its
  Message-ID and moved with the forward (R975). One already in Deleted Items
  counts as already there, and one the mailbox no longer holds as no longer
  in the mailbox.
- A staff forward that names no original, or whose forwarding mailbox is not
  an Approved, activated mailbox: the forward is moved alone.
- A Case e-mail in a mailbox that is Approved but not activated, as every
  mailbox is on the test instance after a production copy: it is left and
  counted nowhere.

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
  (answered instruction, and a ticked send's Case correspondence, to Deleted
  Items),
  [ADR-0044](../adr/0044-mail-occurrence-and-business-identity.md) (mail identity).
