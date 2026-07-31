# UI-10 email-management workspace decisions

**Date:** 2026-07-31  
**Delivery identity:** GitHub issue [#176](https://github.com/collisionengineers/pegasus/issues/176)  
**State:** accepted product and planned design decisions; not implemented, caller-proved, deployed or live-verified.

## Accepted decisions

- Do not retain `View in Outlook`; the in-app message, attachment and thread view is sufficient.
- Land on the incoming Inbox across all approved mailboxes. Mailbox, folder, queue and search views narrow it; Sent and read-only Deleted Items search are explicit scopes.
- General mailbox search includes retained message bodies, attachment filenames and searchable attachment content; unavailable content is explicit.
- Search remains in the current mailbox/folder scope unless explicitly broadened.
- Search returns individual messages rather than collapsed conversation groups.
- Results identify message-body, attachment-filename and attachment-content hits, naming the matching attachment where applicable.
- Inbox and search-result lists use accessible pagination, not infinite scrolling.
- The all-Inboxes view defaults to newest received message first.
- Active mailbox, folder, queue and search filters remain visible and survive return from message or Case detail.
- A fresh visit resets to the default all-Inboxes view rather than retaining cross-session filters.
- The workspace provides manual refresh, last successful update time and distinct stale/unavailable state.
- Refresh preserves active filters, page and open-message context when still available.
- Inbox rows include a short message-body excerpt beneath sender and subject.
- Inbox rows display read/unread state but UI-10 does not change it.
- Keep quick preview as non-mutating evidence navigation, showing sender, subject, timestamp, excerpt, classification, association and attachment names without mutation controls. Opening a message preserves the originating filtered-list position and provides a detail view.
- Threads show only retained messages within approved mailbox/folder scope; matching thread identity never broadens that scope.
- Classification, linking and folder-move actions appear only in opened message detail.
- UI-10 has no bulk classification, linking or folder-move actions; each decision applies to one exact message.
- A recommended folder move follows a saved classification as a separate explicit confirmation.
- Staff may confirm only the classification policy's designated folder; a different destination requires correcting classification.
- Reclassification to a different designated folder offers a new separate move confirmation; it never moves automatically.
- A failed confirmed folder move leaves the saved classification intact, makes failure visible, and allows staff-initiated retry only.
- A successful folder move removes the message from Inbox without duplication and retains it in destination-folder and search scope.
- The detail view prioritises full retained message content and attachments, followed by a chronological thread. Classification, queue, processing outcome and Case association appear before permitted actions.
- Use source-preserving attachment preview where supported, with access to the original retained file.
- Linking is deliberate: Case search, target summary, reason and explicit confirmation. It may be performed while classification is unresolved where link evidence is sufficient. Ambiguous automatic association remains unlinked.
- Selecting a Case association opens its workspace in the same tab; Back returns to the exact message detail and originating list context.
- Each Case workspace exposes one newest-first chronological history of associated received and Sent correspondence, with explicit oldest-first ordering; cross-mailbox browsing and reconciliation remain in the email-management workspace.
- Authorised correction keeps the existing permanent-history requirements. A staff-confirmed recommended Outlook folder move remains separately gated; it is never automatic.

## Preserved boundaries and deferrals

The settled Core intake flow remains unchanged: a definitive authorised intake creates its Case directly and idempotently; there is no Intake review step or universal manual acceptance gate. Unresolved source route, category or matching evidence remains visible in `Needs sorting`.

UI-10 remains `Next / 0.3.0`. No route, caller, Graph access, mailbox operation, migration, runtime, implementation, deployment or acceptance is created by this record. Activation still requires exact approved mailbox and folder scopes, accepted automatic predicates where applicable, the named Core owner, genuine caller evidence, complete design approval and operator acceptance.

## Evidence

Direct operator decisions were recorded in issue #176 on 2026-07-31. The current product behaviour owner is `docs/requirements.md`, the UI-10 allocation is `docs/capabilities.md`, and the durable planned interaction contract is `design/product/ui-spec.md`.
