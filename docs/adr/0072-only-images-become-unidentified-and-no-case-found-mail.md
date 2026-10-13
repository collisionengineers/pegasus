---
id: ADR-0072
status: accepted
date: 2026-10-13
supersedes: [ADR-0069, ADR-0070]
superseded_by: []
related_capabilities: [INT-23, MAIL-02, UI-14, UI-18]
related_frd: [frd-02, frd-08, frd-15, frd-18, frd-20]
tags: [intake, unidentified, mail, classification, inbox]
---

# ADR-0072: Only images become Unidentified, and mail with no Case is No Case Found

## Status

Accepted on 13 October 2026 by operator decision.
[FRD-02](../frd/frd-02-intake-and-source-identity.md#unidentified-destination-and-reference)
owns what becomes Unidentified;
[FRD-20](../frd/frd-20-mailbox-workspace.md#inbox-scopes-and-filters) owns
the Inbox scopes;
[FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md#mail-categories-and-correction)
owns the case-state classification rule;
[FRD-15](../frd/frd-15-work-centre-queues-and-search.md#cases-queues-and-filters)
owns the Cases rail.

This record replaces, in part:

- [ADR-0070](0070-case-state-mail-classification.md): decision 2's arms for
  a closed Case (it now gives Case update, not nothing) and the absent arm
  for a Triage Case; decision 3's reach, which now also covers the staff link
  to a Triage Case and image intake's automatic link; and the consequence
  that non-Principal mail appears in the Inbox Unidentified scope while its
  item is open. Decisions 1 and 4 and the other consequences stand.
- [ADR-0069](0069-one-flat-mail-category.md): the consequence that the Inbox
  scope rail is All incoming, Unidentified, Sent Items and Dismissed, and
  decision 2's reading of Unidentified as the name for mail no
  classification has decided. Unidentified is still not a category, and an
  undecided message is still Unclassified. The rest stands.

## Context

Every pre-Case outcome, e-mails and documents included, became an
Unidentified item with a U-reference. An e-mail with no Case therefore had
two queues: the Inbox Unidentified scope, which listed undecided mail until
its item resolved, and the item on the Cases Unidentified tab. A Triage
request without a registration or Principal was held as an item too. A
classified message with no Case left the Inbox scope although nothing held
it, and a message linked to a closed or Triage Case kept no category.

## Decision

1. **Only images become Unidentified.** Core owns the gate
   `ProcessIntake.BecomesUnidentified(receipt)`
   (`src/Pegasus.Core/Intake/ProcessIntake.cs`): image-only material from any
   channel (`ImageIntakeLifecycleRules.IsImageOnlyMaterial`), or a
   non-mailbox file whose media kind is an image. Both registration points
   apply it: `IsUnidentifiedEligible` in the processing hook and the queued
   caller's deferred registration in `ProcessQueuedIntake`. An e-mail with no
   Case is not registered and is refused by Dismiss; a manual-upload document
   that cannot become a Case keeps its upload outcome and is refused by its
   upload; non-image material from the Principal API or Automation that
   cannot be placed gets no item. An emailed Triage request without a
   registration or Principal gets no item.
2. **No Case Found is the Inbox scope for mail with no Case.**
   `MailWorkspaceScope.NoCaseFound` (`queue=no_case_found`) replaces the
   Unidentified scope. `EfRetainedMailboxMessageStore` lists received mail
   whose receipt is linked to no Case, reading the Case in the same order as
   the message summaries: an active association; else an accepted link when
   no association row exists; else the Case the latest allocation created,
   unless an association was reversed; else the Triage the receipt opened.
   Classification and Unidentified item state play no part. A `NeedsSorting`
   message's outcome chip and the Category list's last option read No Case
   Found.
3. **A linked message without a category of its own is Case update.**
   `CaseStateMailClassification.Derive(current, state)` takes a nullable
   state: Report sent gives `Query`; every other state, a closed Case
   included, and a Triage Case (no lifecycle state) give `Case update`.
   `EfIntakeMutationStore` applies it, inside the link transaction, in the
   automatic association from a match (`AssociateFromMatchAsync`), the
   automatic link image intake pairing makes (`AutoLinkAsync`) and the staff
   link to a Case or a Triage Case. A `Classified` decision, including a staff
   correction, is never overridden.

## Consequences

- The Cases rail has no Unidentified tab. Unassigned Images
  (`/Cases?tab=unassigned`) shows Identified image records by default and
  the open or closed Unidentified items behind its Show filter; `/Unidentified`
  redirects to `?tab=unassigned&show=unidentified`. The Query tab reads
  Queries.
- `RetainedMailSummary` loses `MailboxIsPolled` and `UnidentifiedResolved`;
  the No longer polled chip leaves the mail list.
- There is no data migration. Unidentified items already registered for
  e-mails or documents are not converted; the operator's intake-data wipe
  after release clears them.

## Links

- [FRD-02](../frd/frd-02-intake-and-source-identity.md),
  [FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md),
  [FRD-15](../frd/frd-15-work-centre-queues-and-search.md),
  [FRD-18](../frd/frd-18-manual-upload.md),
  [FRD-20](../frd/frd-20-mailbox-workspace.md).
- [ADR-0069](0069-one-flat-mail-category.md),
  [ADR-0070](0070-case-state-mail-classification.md).
