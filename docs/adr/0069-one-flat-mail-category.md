---
id: ADR-0069
status: accepted
date: 2026-10-12
supersedes: [ADR-0036, ADR-0052, ADR-0064]
superseded_by: [ADR-0072]
related_capabilities: [MAIL-02, MAIL-21, MAIL-22, UI-14]
related_frd: [FRD-02, FRD-08, FRD-10, FRD-13, FRD-17, FRD-20]
tags: [mailbox, classification, inbox]
---

# ADR-0069: One flat mail category

## Status

Accepted on 12 October 2026 by operator decision.
[FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md#mail-categories-and-correction)
owns the categories and what each does;
[FRD-20](../frd/frd-20-mailbox-workspace.md#inbox-scopes-and-filters) owns
the Inbox scopes and the Category list.

This record replaces, in part:

- [ADR-0052](0052-dismiss-by-logical-folder.md): dismiss by logical folder.
  Dismiss and Restore stay as a Pegasus-only dismissed-at scope with no
  folder.
- [ADR-0036](0036-outbound-mail-via-approved-mailbox.md): the confirmed
  folder move and its mover seam. The send is the only mailbox mutation.
- [ADR-0064](0064-automation-actor-staff-casework-parity.md): the
  Automation actor's mail actions are dismiss and restore; it corrects a
  classification as before.

It supersedes the "simplify mail classification" decision in open PR 1126,
whose ADR-0067 of 8 October 2026 never merged and whose number collides with
the merged ADR-0067.

[ADR-0072](0072-only-images-become-unidentified-and-no-case-found-mail.md)
(13 October 2026) supersedes the consequence that the Inbox scope rail is
All incoming, Unidentified, Sent Items and Dismissed: it is All incoming,
No Case Found, Sent Items and Dismissed, and the Category list ends in No
Case Found. It also supersedes decision 2's use of Unidentified as the name
for mail no classification has decided; such mail is Unclassified, and
Unidentified is still not a category. The rest of this decision stands.

## Context

The operator supplied the eight Received families (CollisionSPikeCurrenttree
outline, discovery questionnaire). The operational-destination table, the
"Detailed classification" destination and the thirteen Outlook folder names
were authored by earlier development sessions on 19–20 August 2026, not by
the operator. Only five categories carried behaviour. The folder
recommendation and the confirmed folder move were never composed in any
runtime.

## Decision

1. Every retained received message has one flat mail category, from exactly
   eleven: New instruction (carrying a work type: Inspection, Audit, or
   Inspection and Audit), Triage request, Pre-instruction, Images received,
   Case update, Cancellation, Query, Billing, General, Not client related
   and Internal CC.
2. Unidentified is not a category. It means no classification has decided
   the message: the outcome is Unclassified. Several matching candidates are
   Unclassified, and the reason names the candidates.
3. Removed: families and subtypes, the reasoned Other category, the
   Ambiguous outcome, Sent-mail categories (Sent items carry no category),
   operational destinations (Receiving work, Queries, Triage, Other,
   Detailed classification), the Outlook logical folders, folder
   recommendation, the confirmed folder move and per-mailbox folder
   bindings. Sending is the only mailbox write.
4. Folded categories: autoreply, undeliverable, acknowledgement, general
   chase and case summary are General; payment notification, remittance,
   invoice request, billing query and general billing are Billing; audit,
   diminution, inspection, new client and website enquiry are New
   instruction, whose work type carries the case type; case update, chasing
   for update and ongoing correspondence are Case update; query, dispute and
   amendment request are Query; pre-formal instruction request is
   Pre-instruction.
5. What each category does is unchanged; only the names changed. Staff and
   the Automation actor may correct a message to any category or to
   Unclassified, with a reason; the history is kept.

## Consequences

- Amendment requests are Query: the Amendment requests column leaves the
  Case list and the Engineer activity report.
- The Inbox scope rail is All incoming, Unidentified, Sent Items and
  Dismissed. Categories are chosen from one flat Category list with no
  groups.
- The Unidentified reason "Ambiguous ownership or destination" is removed;
  those items read "No usable identification".
- `pegasus_mail_action` loses `move_folder`.

## Links

- [FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md),
  [FRD-20](../frd/frd-20-mailbox-workspace.md),
  [FRD-02](../frd/frd-02-intake-and-source-identity.md),
  [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md).
