---
id: ADR-0067
status: accepted
date: 2026-10-08
supersedes: []
superseded_by: []
related_capabilities: [MAIL-02, MAIL-05, MAIL-06, MAIL-07, MAIL-21, MAIL-22, MAIL-23, UI-14]
related_frd: [frd-08, frd-10, frd-20]
tags: [mail, classification, inbox, mcp]
---

# ADR-0067: Simplify mail classification

## Status

Accepted on 8 October 2026 by operator decision. It amends three earlier
records, none of which is superseded as a whole:

- [ADR-0052](0052-dismiss-by-logical-folder.md): Dismiss is a dismissed-at
  scope with no logical folder.
- [ADR-0036](0036-outbound-mail-via-approved-mailbox.md): there is no
  `IRetainedMailFolderMover`; sending is the only mailbox mutation.
- [ADR-0064](0064-automation-actor-staff-casework-parity.md): the Automation
  Actor's mail action is dismiss and restore only.

[FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md) owns the
resulting classification wording and
[FRD-20](../frd/frd-20-mailbox-workspace.md) the Inbox scopes and filters.

## Context

The classification surface had grown beyond what ran. The Outlook folder
move pipeline was inert in every runtime: only the unavailable mover was
composed, so no move ever reached Outlook. The four Sent categories, the
reasoned `Other` category and the `Ambiguous` outcome were never produced
automatically. The result was 28 correction options and 21 detailed Category
filter views for five code branches.

## Decision

1. **The folder recommendation and move pipeline is removed.** There are no
   logical folder types, no folder bindings per mailbox, no folder-move
   record and no move action in Web or MCP. Dismiss and Restore stay as a
   dismissed-at scope; a message carries no logical folder.
2. **Sent items carry no classification category.** The four Sent families
   go. Sent polling, listing and the observed-reply rule stay; a message's
   direction is its folder scope.
3. **`Ambiguous` folds into `Unclassified`.** A classification outcome is
   `Classified` or `Unclassified`. When predicates for more than one category
   match, the reason names the competing candidates and no winner is invented.
4. **The Category filter lists destinations and received families.** It
   offers the four operational destinations (Receiving work, Queries, Triage,
   Unidentified) and one option per received family. There are no
   per-category detailed views.
5. **The reasoned `Other` category is removed.** Every received message is one
   of the eight settled families or `Unclassified`.
6. **The Inbox scope formerly labelled "Case updates" is labelled
   "Queries".** It is the Queries destination.

Classification, application queue and Triage routing are therefore three
facts, with no Outlook folder fact.

## Migration

One hand-written destructive migration,
`20261008090000_SimplifyMailClassification`. It converts decisions and
decision history that were ambiguous, `Other` or Sent to `Unclassified`,
maps the `AmbiguousOwnershipOrDestination` Unidentified reason to
`NoUsableIdentification`, drops the `Direction`, `OtherName`,
`OtherReasoning` and `AmbiguousCandidatesJson` columns, and drops
`RetainedMailFolderMoves` and `ApprovedMailboxFolderBindings`. It is forward
only. A release follows the destructive-migration procedure (ADR-0046).

## Consequences

- The only mailbox mutation Pegasus performs is the ADR-0036 send; Dismiss
  and Restore touch Pegasus data only.
- `pegasus_mail_action` accepts dismiss and restore; it has no move action
  and no `isOther` or `otherName` parameters.
- Correct classification offers received families and the Unclassified
  outcome only.
- Approved mailboxes carry no folder bindings; the intake data wipe no longer
  preserves them.
- The `Down` migration throws; recovery is a restore from backup.

## Links

- [ADR-0052 — Dismiss a retained message by logical folder](0052-dismiss-by-logical-folder.md)
- [ADR-0036 — Outbound mail via the approved mailbox](0036-outbound-mail-via-approved-mailbox.md)
- [ADR-0064 — The Automation Actor has staff casework parity](0064-automation-actor-staff-casework-parity.md)
- [ADR-0046 — Destructive migration runtime shutdown](0046-destructive-migration-runtime-shutdown.md)
- [FRD-08 — Email, mailbox, and background processing](../frd/frd-08-email-mailbox-and-background-processing.md#classification-and-destination-catalogue)
- [FRD-20 — Inbox](../frd/frd-20-mailbox-workspace.md)
