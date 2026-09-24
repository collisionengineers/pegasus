# FRD-20: Mailbox workspace

> Owner capabilities: MAIL-06, MAIL-10, MAIL-11, UI-10, UI-14 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- The Inbox shows retained mail from every approved mailbox, newest first,
  from each mailbox's retention start. Nothing older is shown or rebuilt.
- Looking at a message never changes anything. Only the opened message
  record offers classification, Case linking and folder moves, one message
  at a time.
- A folder move is a separate confirmation after classification, and only
  to the folder the policy names.
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

`/Inbox` lists retained mail by scope: All incoming, Receiving work, Case
updates, Pre-instructions, Unidentified, Sent Items and Dismissed. There is
no Unread scope; an unread row stays bold. The filters are mailbox, folder
and **Category**, which lists destinations and approved categories.

The default view is the incoming Inbox across all approved mailboxes, newest
received first. Folder, mailbox, queue and search views are explicit
refinements of that view. Sent mail and read-only Deleted Items search are
separate folder scopes.

There is no historical backfill. The workspace shows retained mail from each
approved mailbox's genuine retention start, names that boundary, and says
that mail before it is absent. Nothing earlier is reconstructed or implied.

The Unidentified scope lists retained mail whose Unidentified item is still
open. Once that item resolves, the message leaves the scope but keeps its
classification record and Case association. Mail views link to the same
Unidentified item; they never create a second queue row.

Each row shows sender, subject and a short excerpt of the body. Rows show
retained read and unread state, but this workspace never changes that state.
Lists use accessible pagination, not infinite scrolling. Active mailbox,
folder, queue and search filters stay visible and are kept when the operator
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
links instead of going blank. It shows sender, subject, time, excerpt,
classification, association and attachment names. It has no controls that
change anything. Previewing never changes classification, association, read
state, Case state or custody.

Opening a message keeps the list's filter and position. The record shows the
full retained message, its attachments and a chronological thread, and
states the current classification, queue, processing outcome and Case
association before any action is offered.

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

Refresh keeps the active mailbox, folder, queue, search filters, page and
open message when that message is still available. If the message has left
the current scope, its detail stays open with a clear "no longer in this
view" state and a way back to the list.

### Classification, linking and folder-move actions

Classification, Case linking and folder moves are offered only from the
opened message record, never from a row or the quick preview. There is no
bulk action: each decision applies to one exact message.

Case linking starts with a deliberate Case search, then shows the target
summary, asks for a reason and needs explicit confirmation. Linking may
happen while classification is still unresolved, when the link evidence on
its own is enough.

After a classification is saved, the recommended Outlook folder move is a
separate, explicit confirmation. Staff may confirm only the folder the
classification policy names. Wanting a different folder means correcting the
classification, not picking a folder. If a later reclassification names a
different folder, Pegasus offers another separate confirmation; it never
moves the item automatically. If a move fails, the saved classification
stays, the failure is visible, and only a staff-initiated retry may repeat
the move. After a successful move, the message leaves the Inbox view and is
still found through its destination folder or search. It is never
duplicated.

The message's displayed destination reflects its current Case association
even while its classification is Unclassified. Classification, actual
destination and custody completion are separate facts. A resolved
Unidentified origin is kept as history, not shown as the current
destination.

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
([FRD-16](frd-16-case-record-workspace.md#files)). Cross-mailbox browsing and
reconciliation stay in this mailbox workspace.

### Dismiss

A row or the message record offers **Dismiss**. It moves the message into
the `Dismissed` logical folder and out of every other scope. **Restore**
from the Dismissed scope brings it back. Both are always allowed. An open
Unidentified item stays open, and the message keeps its evidence,
associations and history. Dismiss is Pegasus data only: the Outlook item
does not move and no Graph call is made
([ADR-0052](../adr/0052-dismiss-by-logical-folder.md)). There is no flag,
no delete and no Deleted Items move on any surface
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)).

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
- A folder move that fails leaves the classification intact and waits for a
  staff retry.
- A refreshed message that left the scope stays readable with a way back.
- A thread identity seen in another mailbox is never followed.
- A count or list whose query has not run shows nothing, never `0`.
- No bulk classification, linking, move or dismiss exists.

## Acceptance evidence

- Authenticated Web tests for every scope, the three filters, pagination,
  the 15-minute stale state, filter retention across message and Case
  detail, and the fresh-visit reset.
- Tests that a preview and an opened record change no classification,
  association, read state or custody.
- Tests that the folder move is a separate confirmation and refuses any
  folder other than the policy's.
- Dismiss and Restore tests showing no Graph call.
- Deployment and live acceptance are separate evidence tiers
  ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `MAIL-06`, `MAIL-10`, `MAIL-11`, `UI-10`, `UI-14` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-08](frd-08-email-mailbox-and-background-processing.md)
  (classification and retention),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md) (sending),
  [FRD-02](frd-02-intake-and-source-identity.md) (Unidentified and received
  files), [FRD-12](frd-12-operator-experience.md) (shell and routes),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md) (Case states).
- Technical constraints:
  [ADR-0052](../adr/0052-dismiss-by-logical-folder.md) (dismiss by logical
  folder), [ADR-0036](../adr/0036-outbound-mail-via-approved-mailbox.md)
  (outbound mail).
