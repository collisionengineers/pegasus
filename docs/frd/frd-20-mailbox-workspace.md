# FRD-20: Mailbox workspace

> Owner capabilities: MAIL-10, MAIL-11, UI-10, UI-14 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- The Inbox shows retained mail from every approved mailbox, newest first,
  from each mailbox's retention start. Nothing older is shown or rebuilt.
- Looking at a message never changes anything. The opened message record
  offers classification and Case linking. Ticked rows can be classified,
  dismissed or restored together, each message on its own; there is no bulk
  Case linking.
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

`/Inbox` lists retained mail by scope: All incoming, No Case Found, Sent
Items and Dismissed. There is no Unread scope; an unread row stays bold. The
filters are mailbox, folder and **Category**, one flat list: All categories,
the eleven mail categories
([FRD-08](frd-08-email-mailbox-and-background-processing.md#mail-categories-and-correction))
and No Case Found, with no groups.

The default view is the incoming Inbox across all approved mailboxes, newest
received first. Folder, mailbox, category and search views are explicit
refinements of that view. Sent mail and read-only Deleted Items search are
separate folder scopes. Sent Items lists every Sent item the Sent-evidence
poll ([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md)) has read
from an Outgoing approved mailbox, newest sent first, whether Pegasus or Outlook
sent it; a row opens the message record the same way a received one does.

There is no historical backfill. The workspace shows retained mail from each
approved mailbox's genuine retention start, names that boundary, and says
that mail before it is absent. Nothing earlier is reconstructed or implied.

The No Case Found scope (`queue=no_case_found`, operator 13 October 2026)
lists received, not-dismissed mail whose receipt is linked to no Case: no
active association, accepted link, Case created by its allocation, or
Triage it opened. Once the message is linked to a Case, it leaves the
scope. Only mail carrying nothing but photographs becomes an Unidentified
item; any other e-mail never does
([FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference))
and is refused by Dismiss. A message whose processing left it for sorting
(`NeedsSorting`) reads **No Case Found** in its outcome chip. Mail views link
to the same Unidentified item of photographs that arrived by e-mail. A
photo-only e-mail with no Case is listed in No Case Found and in Unassigned
Images › Unidentified until it is linked.

Each row shows sender, subject and a short excerpt of the body. The subject
opens the message. Clicking elsewhere on a row selects it. The selected row
is marked. Up and Down move through the rows. A received row offers
**Reply** when the mailbox it arrived in can send; it opens the message
record's Reply
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

### Quick preview and message detail

The quick preview opens on pointer or keyboard intent, works with a keyboard
and a screen reader, and never clips or covers nearby controls. When the
intent moves away, the pane keeps the selected message and its navigation
links instead of going blank. It shows the sender, subject, time, mailbox
and state. It shows the message text with its line breaks, up to the first
eight lines. It counts the attachments by kind and does not name them. It
shows the classification, the Case or Triage Case, and the folder. While a
search term is typed, it also says where the term matched. Its actions are
Open full message, Reply when the mailbox the message arrived in can send
(it opens the message record's Reply), and Open Case, or Open Triage when
the message's Case is a Triage Case, whether the message opened it or staff
linked the message to it (operator ruling, 15 October 2026). It has no
controls that change anything. Previewing never changes classification,
association, read state, Case state or custody.

Opening a message keeps the list's filter and position. The record shows the
full retained message, its attachments and a chronological thread, and
states the current classification, processing outcome and Case
association before any action is offered. When a message opened a Triage
and is linked to no Case, its Case tab shows that Triage and still offers
Link to Case.

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
attachment where that applies.

Read-only search of Deleted Items is available within each exact approved
mailbox and folder scope. It does not scan a backlog, rebuild anything,
replay in bulk, allocate a Case or change the mailbox.

### Refresh and staleness

The workspace has a manual refresh, shows the last successful update time,
and has distinct stale and unavailable states. It never presents old data as
current. The stale threshold is a fixed 15 minutes since the last successful
update; it is not configurable and does not depend on load. The list does
not refresh on its own while an operator is reading or acting.

Refresh keeps the active mailbox, folder, category, search filters, page and
open message when that message is still available. If the message has left
the current scope, its detail stays open with a clear "no longer in this
view" state and a way back to the list.

### Classification and linking actions

Case linking is offered only from the opened message record, never from a
row or the quick preview, and each link applies to one exact message; there
is no bulk Case linking. Classification is offered from the opened message
record and, for ticked rows in the incoming scopes, from the list, with one
category and one reason for every ticked message. Each ticked message is
corrected on its own, with its own history entry (operator, 15 and 16
October 2026).

The correction picker offers every mail category and Unclassified, and a
correction to New instruction asks for the work type. Once a classification
is saved, the message record offers the one next action it calls for:
**Create case** for a New instruction, **Open the Triage** for a Triage
request, **Register images** for images received, each only while the
receipt still qualifies and no Case holds it. Staff press it; nothing runs on
its own, and a Case or Triage already opened from the message stays as it is
when the message is corrected away (operator, 1 October 2026). A correction
to or from Query on a linked Case follows
[FRD-13](frd-13-case-lifecycle-and-workflow.md#report-sent-and-queries).

Case linking starts with a deliberate Case search, then shows the target
summary, asks for a reason and needs explicit confirmation. Linking may
happen while classification is still unresolved, when the link evidence on
its own is enough. The Automation Actor links through the same command
(`pegasus_mail_link_case`).

The opened message record also offers **Unlink** beside the linked Case. It
asks for a reason and needs explicit confirmation; where the Case is the one
the message's own acceptance created, the dialog says that unlinking cancels
it. Once no Case is current, **Link to case** relinks the message. Both keep
the history rule in
[FRD-08](frd-08-email-mailbox-and-background-processing.md#mail-categories-and-correction)
(operator ruling, 15 October 2026).

The message record shows its current Case association even while its
classification is Unclassified. Classification, Case association and custody
completion are separate facts. A resolved Unidentified origin is kept as
history.

Selecting the linked Case opens that Case in the same tab. Back returns to
the exact message and the list context it came from.

### Case correspondence view

Each Case workspace shows its linked received and Sent items as one
chronological history, newest first by default with an oldest-first option.
A row opens its message over the Case, read-only like the quick preview: no
classification, association, read-state, Case-state or custody change. Reply,
Reply all and Forward appear there where the record offers them and open the
record's composer with that Case chosen
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)).
The full message record is one link away for every other action
([FRD-16](frd-16-case-record-workspace.md#files)). Cross-mailbox browsing
stays in this mailbox workspace.

### Dismiss

A row or the message record offers **Dismiss**. It records the message as
dismissed and takes it out of every other scope. **Restore**
from the Dismissed scope brings it back. Ticked rows can be dismissed
together in the incoming scopes and restored together in the Dismissed
scope, each message on its own (operator, 15 and 16 October 2026). Both are
always allowed, to staff and to the Automation Actor (`pegasus_mail_action`).
An open Unidentified item stays open, and the message keeps its evidence,
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
- No bulk Case linking exists. A bulk classification, Dismiss or Restore
  applies to each ticked message on its own, so one that fails leaves the
  others applied.

## Acceptance evidence

- Authenticated Web tests for every scope, the three filters, pagination,
  the 15-minute stale state, filter retention across message and Case
  detail, and the fresh-visit reset.
- Tests that a preview and an opened record change no classification,
  association, read state or custody.
- Dismiss and Restore tests showing no Graph call.
- Bulk classification, Dismiss and Restore tests showing one history entry
  per ticked message, a repeated post writing none, a stale message leaving
  the others corrected, and every bulk post refused before a write outside
  its scope or without casework.
- A persistence test that a retried Dismiss replays without a second history
  entry and that concurrent acts do not deadlock, and a grant test that Web
  holds UPDATE and not DELETE on the retained message table.
- Deployment and live acceptance are separate evidence tiers
  ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `MAIL-10`, `MAIL-11`, `UI-10`, `UI-14` in
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
