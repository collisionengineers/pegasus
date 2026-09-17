# FRD-21: Outbound correspondence and Sent evidence

> Owner capabilities: MAIL-14, MAIL-15, MAIL-16, MAIL-19 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Pegasus only believes a report was sent when it finds the exact Sent item
  in an approved Outlook mailbox. A draft, an export or a staff note is not
  proof.
- Staff send Reply, Reply all, Forward and Compose from an approved mailbox,
  never from their own address. Only signed-in staff with the casework right
  can send.
- Every send is one durable operation. Graph saying "accepted" means
  submitted, not sent. The Sent item that Graph later writes is the evidence.
- A report sent through EVA is detected from the Sent mailbox, not asserted.
  If the match is unclear, the item waits for staff to link it.
- Nothing here deletes or flags a mailbox item.

## Purpose

Collision Engineers must be able to prove which report went to which
Principal, and when. This document sets the rules for that evidence and for
the mail staff send from Pegasus. Inbound mail and classification live in
[FRD-08](frd-08-email-mailbox-and-background-processing.md). The screens
for browsing mail live in [FRD-20](frd-20-mailbox-workspace.md).

## Behaviour

### Outbound correspondence evidence

Report-sent evidence links one exact, immutable Outlook Sent item to exactly
one Case. The mailbox must be on the Administrator's allowlist.

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

Triage completion is based on the recorded outcome, under
[FRD-03](frd-03-triage.md). Its optional Reply with outcome opens a preset
email; a retained sent reply links to the Triage record without becoming a
completion requirement.

Local development must never change a mailbox. A Worker project, queue
registration or timer setting does not prove that a real caller ran.

### Outbound correspondence

**One durable operation per send.** Each send is keyed by the staff actor,
the mailbox and an operation key, with a payload hash that the server
calculates. Reusing a key with a different payload fails. Draft creation and
upload progress survive a restart without a second send attempt. If the
provider's answer is ambiguous, the send stays Unknown until exact evidence
settles it.

**Submitted is not Sent.** Graph `202 Accepted` means Submitted. Sent needs
the retained-MIME pipeline to match the immutable item, the operation marker,
the mailbox generation and the attachment hashes. The provider's sent time
and the time Pegasus observed the item stay separate facts. Each enabled Sent
mailbox has its own cursor and activation boundary. Old items move the cursor
forward but are not backfilled. One failing mailbox does not block the
others.

**Report sends are rechecked at the last moment.** Immediately before the
provider call, Pegasus revalidates the Case's persisted report readiness, the
exact generation, its versions and artifacts, the staff member's authority
and the mailbox generation. No connector may trigger this send.

**Where the controls are.** Reply, Forward and Compose appear on the Inbox
message and on the Case correspondence surface. They exist only when the
outbound capability is composed into the application. Otherwise those
surfaces show no send control and no composer. The technical decision is
[ADR-0036](../adr/0036-outbound-mail-via-approved-mailbox.md).

**Who may send.** A signed-in staff member with the casework right. There is
no automatic, scheduled or Automation Actor send.

**From which mailbox.** Reply and Forward send as the approved mailbox that
holds the retained message. Compose sends as the default approved mailbox.
The sender is never a staff member's own address or any mailbox outside the
allowlist, and the composer shows the sender read-only. An Administrator
picks the single default in Mailbox settings and records a reason. Only a
send-ready mailbox with Sent-evidence polling configured may be the default.
Replacing the default checks the versions of both mailboxes. Disabling the
default, or removing a capability it needs, requires choosing a replacement
first. With no default configured, a new Compose explains what is missing
and cannot send. It never silently picks the first mailbox.

**What the composer carries.** To, Subject, Message, Case and From
(read-only). Reply and Forward keep the retained message's reply chain and
conversation identity. Case defaults to the message's current association
and may be changed before sending.

**Reply targets.** Pegasus keeps the structured MIME Reply-To addresses in
their original order and uses the From addresses only when Reply-To is
absent. A Reply-To header that is present but unusable gives an empty list.
The reply screen shows the retained targets and the chosen recipients so
staff can confirm them. If the retained metadata is missing or the target
list is empty, Reply and Reply all are refused. The transport Sender, To and
Cc are never used as substitutes. Starting a new message instead is a
deliberate staff choice.

**What is retained.** The immutable Sent item that Graph writes is the
evidence. The Sent-evidence poll retains it under the rules in
[Outbound correspondence evidence](#outbound-correspondence-evidence) and
links it to the Case named at send time. Draft text is not evidence until
that Sent item exists. A send that Graph refuses leaves no evidence and shows
as a failure on the composer, never as sent.

**No Flag, no Delete.** There is no flag control, no delete control and no
move to Deleted Items on any surface. No surface, action or tool removes a
mailbox item at all ([ADR-0052](../adr/0052-dismiss-by-logical-folder.md)).
Dismissing a message is Pegasus data only and is defined in
[FRD-20](frd-20-mailbox-workspace.md#dismiss).

Local development and every test profile use the unavailable implementation
of the send seam and never change a mailbox. Turning sending on in
production is a separately approved live write. It also requires the
Sent-evidence poll to be enabled for the sending mailbox, so every send has
its evidence.

### EVA-sent report detection

A report sent through EVA rather than from Pegasus is detected, not
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

- No default mailbox configured: Compose explains and cannot send.
- Reply-To missing and From unusable: Reply and Reply all are refused.
- Graph refuses the send: nothing is recorded as sent; the composer shows
  the failure.
- The same operation key reused with a different payload: refused.
- A Sent item with zero or several Case matches, or a Case that already has
  report-sent evidence: retained unlinked, waits for staff.
- Outlook later moves or deletes a confirmed Sent item: the confirmed event
  stays final.
- A mailbox that fails to poll: other Sent mailboxes carry on.

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

- Capabilities: `MAIL-14`, `MAIL-15`, `MAIL-16`, `MAIL-19` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-08](frd-08-email-mailbox-and-background-processing.md)
  (inbound mail and classification),
  [FRD-20](frd-20-mailbox-workspace.md) (mail screens),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md) (Case states),
  [FRD-03](frd-03-triage.md) (Reply with outcome),
  [FRD-07](frd-07-eva-and-external-engineering-handoff.md) (EVA handoff).
- Technical constraints:
  [ADR-0036](../adr/0036-outbound-mail-via-approved-mailbox.md) (outbound
  mail via an approved mailbox),
  [ADR-0052](../adr/0052-dismiss-by-logical-folder.md) (no deletion),
  [ADR-0044](../adr/0044-mail-occurrence-and-business-identity.md) (mail identity).
