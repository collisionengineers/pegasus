# FRD-20: Mailbox workspace

> Owner capabilities: MAIL-10, MAIL-11, UI-10 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- The Inbox shows retained mail from every approved mailbox, newest first,
  from each mailbox's retention start. Nothing older is shown or rebuilt.
- Looking at a message never changes anything. The opened message record
  offers Type, Case linking, Open query and Mark done. Ticked rows can be
  dismissed or restored together, each message on its own; there is no bulk
  classification and no bulk Case linking.
- The list says when it was last refreshed and goes stale after 15 minutes.
  It never refreshes under an operator's hands.
- Dismiss hides a message in Pegasus only. Outlook is untouched. Restore
  brings it back.

## Purpose

Staff need one place to read retained mail across all approved mailboxes,
decide what each message is, and connect it to a Case, without touching the
mailbox itself. This document owns those screens. What the classifications
mean, and how mail is retained, is owned by
[FRD-08](frd-08-email-mailbox-and-background-processing.md). Sending mail is
owned by [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md).

## Behaviour

### Inbox scopes and filters

`/Inbox` lists retained mail in three tabs: **All**, **No Case Found** and
**Dismissed**. Sent Items and Deleted Items are not Inbox scopes. There is no
Unread tab; an unread row stays bold. The toolbar is Search (`Subject, sender,
reference`), All principals, All Mailboxes, the count and Received order.
There is no Category filter and no bulk Correct classification. The bulk bar
(Dismiss, or Restore on Dismissed) is hidden until a row is ticked.

**All** lists linked mail, a New instruction, a Triage request, a Pre-Pegasus
case, or a receipt nothing has classified yet. **No Case Found** lists
received mail linked to no Case that is not decided to be a Pre-Pegasus case
(operator, 30 October 2026, R561). The ledger is full width: a select-all tick, then Case (None, in quiet
type, when unlinked), Type, Subject, Sender (with the Principal under it) and
Received. Type is the category's status chip; a New instruction carries its
work type beneath it as a plain neutral chip, and Correspondence is a neutral
chip (operator, 30 October 2026, R559). A row click opens
`#mailMessageDialog`.

The message dialog is full width. A context bar under the route line shows
Case, Type and Query. The settings menu (cog, `#icon-settings`) holds, in
order, Case (the Case / PO picker and Link, Change linked Case or Unlink),
Type (one select), Query (Open query or Mark done), Change case (Place on Hold
and Close case, only when the linked Case allows that act) and Decided.
Register images is offered on an unlinked, uncategorised image-only message.

Sent mail remains a retained folder for Case correspondence and the
Sent-evidence poll
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md)). It is not an
Inbox tab. `pegasus_mail_list` still accepts `sent`.

There is no historical backfill. The workspace shows retained mail from each
approved mailbox's genuine retention start, names that boundary, and says
that mail before it is absent. Nothing earlier is reconstructed or implied.

The No Case Found scope (`queue=no_case_found`, operator 13 October 2026)
lists received, not-dismissed mail whose receipt is linked to no Case: no
active association, accepted link, Case created by its allocation, or
Triage it opened. Once the message is linked to a Case, it leaves the
scope. So does a message whose Type is set to Pre-Pegasus case: it is listed
in All, and its outcome chip reads **Pre-Pegasus case** (operator, 30 October
2026, R561). Only mail carrying nothing but photographs becomes an Unidentified
item; any other e-mail never does
([FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference))
and is refused by Dismiss. A message whose processing left it for sorting
(`NeedsSorting`) reads **No Case Found** in its outcome chip. Mail views link
to the same Unidentified item of photographs that arrived by e-mail. A
photo-only e-mail with no Case is listed in No Case Found and in Unassigned
Images › Unidentified until it is linked.

Each row shows sender, subject and a short excerpt of the body. The subject
opens the message as a dialog over the list; the address `/Inbox/{id}` stays
for a new tab and for no script (operator, 16 October 2026, R159 (c),
[v41 notes](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/v41-notes.md)).
Every other in-app link to a message (the Case's message dialog and its
Reply, Reply all and Forward, the Case's query Reply, Unidentified, Image
Intake, Create case, the Logs drawer and notifications) opens the Inbox with
that message's dialog open, `/Inbox?open={id}`; the `/Inbox/{id}` page draws
the dialog's head (operator, 30 October 2026, R560).
Clicking elsewhere on a row selects it. The selected row is marked. Up and
Down move through the rows. In All incoming, No Case Found and Dismissed each
row carries a checkbox in its left gutter; ticking it neither opens nor
selects the row (v41 item I18, signed off 19 October 2026,
[v41 notes](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/v41-notes.md)).
A received row offers **Reply** when the mailbox it arrived in can send; it
opens the message dialog's Reply
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)).
Rows show retained read and unread state, but this workspace never
changes that state.
Lists use accessible pagination, not infinite scrolling. Active mailbox,
folder, category and search filters stay visible and are kept when the operator
returns from a message or a Case. A fresh visit resets to the default
all-Inboxes view; there is no cross-session preference.

The message record's Attachments tab states each attachment's own outcome
([FRD-02](frd-02-intake-and-source-identity.md#received-file-history-and-technical-actions)).

There is no `View in Outlook`. The in-app message, attachment and thread
view was accepted as enough, so there is no Outlook navigation, action or
external access requirement.

### Message dialog and message detail

The message is the one reader, and it is a dialog over the Inbox (v41 items
I2, I4 and I17, signed off 19 October 2026,
[v41 notes](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/v41-notes.md)).
The list has no quick preview. The dialog has one head: the subject, with
From, To and received beneath, and Previous message and Next message at the
right beside Close, so the list can be read without closing it. Then come the
tabs Message, Attachments and Thread, and the message beside Decision, and a
foot. Opening, reading and moving between messages never changes
classification, association, read state, Case state or custody.

Opening a message keeps the list's filter and position. The record shows the
full retained message, its attachments and a chronological thread, and
states the current classification, processing outcome and Case
association before any action is offered. When a message opened a Triage
and is linked to no Case, its Decision shows that Triage as its Case with
Open Triage (operator ruling, 15 October 2026) and still offers Link to Case.

The thread shows only retained messages in the same approved mailbox and
folder scope. A matching thread identity never fetches or reveals other
messages.

Until a retained message has yielded its original effective sender, the
Inbox shows a neutral Processing state. It never shows the forwarding desk
address as the sender and then swaps it later.

### Search

Search covers retained message bodies, attachment file names and searchable
attachment content. An attachment that cannot be searched is shown as such,
never silently left out. Search stays inside the current mailbox and folder
scope unless the operator widens it.

Results are individual messages, not collapsed conversations, because every
action applies to one exact message. Each result says whether the match was
in the body, an attachment name or attachment content, and names the
attachment where that applies. Search does not read Deleted Items.

### Refresh and staleness

The workspace has a manual refresh, shows the last successful update time,
and has distinct stale and unavailable states. It never presents old data as
current. The stale threshold is a fixed 15 minutes since the last successful
update; it is not configurable and does not depend on load. The list does
not refresh on its own while an operator is reading or acting.

Refresh keeps the active mailbox, folder, principal, search, page and
open message when that message is still available. If the message has left
the current scope, its detail stays open with a clear "no longer in this
view" state and a way back to the list.

### Classification and linking actions

Case linking is offered only from the opened message record, never from a
row, and each link applies to one exact message; there is no bulk Case
linking and no bulk classification.

The message dialog's context bar shows Case, Type and Query. The settings
menu holds, in order, Case, Type, Query, Change case and Decided. Type is one
select. It posts at once and records the fixed reason "Corrected from the
Inbox"; no reason is typed. The select's New instruction values are combined
(`new-instruction:Inspection` and the same shape for Audit and Inspection and
Audit). A split New instruction plus work type is the same correction. The
select also offers Pre-Pegasus case (`pre-pegasus-case`). Query
is **Open query** or **Mark done**, the Case's own handlers. Change case
links to Place on Hold or Close case only when that Case allows the act.
Register images is offered on an unlinked, uncategorised image-only message.
Once a type is saved, the record offers **Create case** for a New instruction
or **Open the Triage** for a Triage request, each only while the receipt
still qualifies and no Case holds it. A correction does not open or withdraw
a query
([FRD-13](frd-13-case-lifecycle-and-workflow.md#report-sent-and-queries)).

Case linking starts with a deliberate choice in the **Case / PO** picker under
the Case in Decision, which asks for a reason in one dialog; the choice and the
reason post together, in one step with no prepare round trip, still claiming
the Case's edit authority and releasing it (v41 items I9 and I13, signed off
19 October 2026,
[v41 notes](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/v41-notes.md)).
Linking may
happen while classification is still unresolved, when the link evidence on
its own is enough. The Automation Actor links through the same command
(`pegasus_mail_link_case`).

The opened message record also offers **Unlink**; where the Case is the one
the message's own acceptance created, the dialog says that unlinking cancels
it. Once no Case is current, **Link to Case** relinks the message. Both keep
the history rule in
[FRD-08](frd-08-email-mailbox-and-background-processing.md#mail-categories-and-correction)
(operator ruling, 15 October 2026). **Change linked Case** and Unlink sit
under the picker, each asking for a reason in one step (v41 items I9 and I13,
signed off 19 October 2026,
[v41 notes](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/v41-notes.md)).

The message record shows its current Case association even while its
classification is Unclassified. Classification, Case association and custody
completion are separate facts. A resolved Unidentified origin is kept as
history.

Selecting the linked Case opens that Case in the same tab. Back returns to
the exact message and the list context it came from.

### Case correspondence view

Each Case workspace shows its linked received and Sent items as one
chronological history, newest first by default with an oldest-first option.
A row opens its message over the Case, read-only: no
classification, association, read-state, Case-state or custody change. Reply,
Reply all and Forward appear there where the record offers them and open the
composer in the Inbox's message dialog with that Case chosen
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)).
The full message record is one link away for every other action
([FRD-16](frd-16-case-record-workspace.md#files)). Cross-mailbox browsing
stays in this mailbox workspace.

### Dismiss

A row or the message record offers **Dismiss**. It records the message as
dismissed and takes it out of every other scope. **Restore**
from the Dismissed scope brings it back. Both are always allowed, to staff
and to the Automation Actor (`pegasus_mail_action`). In the message dialog,
Dismiss moves on to the next message, else the one before it, else the list
(v41 item I10,
[v41 notes](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/v41-notes.md)).
Ticked rows can be
dismissed together in All and No Case Found and restored together in
Dismissed, each message on its own. The bulk bar is hidden until a row is
ticked. It shows the ticked count and Dismiss, or Restore on Dismissed.
There is a select-all tick. There is no Correct classification.
A bulk post that could not change some messages says "N not changed" once
above the list, and those rows stay ticked so they can be retried (operator,
22 October 2026, R180,
[v41 notes](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/v41-notes.md)).
An open
Unidentified item stays open, and the message keeps its evidence,
associations and history. Dismiss is Pegasus data only: the Outlook item
does not move and no Graph call is made
([ADR-0052](../adr/0052-dismiss-by-logical-folder.md),
[ADR-0069](../adr/0069-one-flat-mail-category.md)). There is no flag and
no delete control on any surface. The only Deleted Items move is the
automatic one after a confirmed report send
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)).

Both acts write only the message's dismissed-at and dismissed-by cells and one
history entry keyed by the operation, so the Web role holds UPDATE and no
DELETE on the retained message table. A retried post returns the committed
state: the act takes the message row first, so a retry waits for it, and no
range lock is taken on the history index.

## States and transitions

| Surface | States |
| --- | --- |
| Inbox list | loading, current, stale (after 15 minutes), unavailable, empty |
| A retained message in the workspace | in a scope, Dismissed, Restored; read or unread as retained |
| Open message after refresh | still in scope, or "no longer in this view" with a return action |

Case states are owned by
[FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels); this
workspace only shows them.

## Edge cases and fail-closed behaviour

- A search over an unsearchable attachment says so; it does not hide the
  attachment.
- A refreshed message that left the scope stays readable with a way back.
- A thread identity seen in another mailbox is never followed.
- A count or list whose query has not run shows nothing, never `0`.
- No bulk Case linking and no bulk classification exist. Dismiss or Restore
  applies to each ticked message on its own, so one that fails leaves the
  others applied.

## Acceptance evidence

- Authenticated Web tests for every scope, the filters, pagination,
  the 15-minute stale state, filter retention across message and Case
  detail, and the fresh-visit reset.
- Tests that the message dialog and an opened record change no classification,
  association, read state or custody.
- Tests that the Type select records its fixed reason, that a link, a
  change and an unlink are each one post, and that a removed type is refused
  on the record.
- Dismiss and Restore tests showing no Graph call.
- Dismiss and Restore tests showing one history entry per ticked message, a
  repeated post writing none, and every bulk post refused before a write
  outside its scope or without casework.
- A persistence test that a retried Dismiss replays without a second history
  entry and that concurrent acts do not deadlock, and a grant test that Web
  holds UPDATE and not DELETE on the retained message table.
- Deployment and live acceptance are separate evidence tiers
  ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `MAIL-10`, `MAIL-11`, `UI-10` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-08](frd-08-email-mailbox-and-background-processing.md)
  (classification and retention),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md) (sending),
  [FRD-02](frd-02-intake-and-source-identity.md) (Unidentified and received
  files), [FRD-12](frd-12-operator-experience.md) (shell and routes),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md) (Case states).
- Technical constraints:
  [ADR-0052](../adr/0052-dismiss-by-logical-folder.md) (dismiss),
  [ADR-0069](../adr/0069-one-flat-mail-category.md) (one flat mail
  category, dismissed-at scope), [ADR-0036](../adr/0036-outbound-mail-via-approved-mailbox.md)
  (outbound mail).
