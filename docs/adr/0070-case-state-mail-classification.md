---
id: ADR-0070
status: accepted
date: 2026-10-08
supersedes: []
superseded_by: [ADR-0072]
related_capabilities: [MAIL-02, MAIL-03, MAIL-04, MAIL-09, MAIL-21]
related_frd: [frd-08, frd-13, frd-26]
tags: [mail, classification, lifecycle]
---

# ADR-0070: Case-state mail classification

## Status

Accepted on 8 October 2026 by operator decision; landed on the flat mail
category of [ADR-0069](0069-one-flat-mail-category.md) on 12 October 2026.
[FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md) owns the
rule's wording; [FRD-13](../frd/frd-13-case-lifecycle-and-workflow.md) owns
the Case query it feeds.

[ADR-0072](0072-only-images-become-unidentified-and-no-case-found-mail.md)
(13 October 2026) supersedes decision 2's closed-Case arm and its silence on
a Triage Case: every state but Report sent, a closed Case and a Triage Case
included, now gives Case update. It widens decision 3 to the staff link to a
Triage Case and to image intake's automatic link, and it supersedes the
consequence about the Inbox Unidentified scope, which is now No Case Found.
Decisions 1 and 4 and the other consequences stand.

## Context

The Principal classification policy produces only autoreply, Triage request
and new-instruction categories. A Principal's post-report query reaches its
Case through the Principal's case-match keys and is linked automatically, but
its decision stays `Unclassified`, so the Report sent Case never raises a
query without a staff correction. Mail from a sender with no accepted route
carried no decision at all, so it could not be corrected and did not appear
in the Inbox Unidentified scope. Corpus evidence shows the Principal's
queries arrive as fresh messages with no reply headers, so thread evidence
alone would not find them. The operator ruled that the receiving mailbox is
never evidence.

## Decision

1. **Every retained received mail message has a decision.** When no route or
   no policy applies, intake records `Unclassified` under
   `no_mail_classification_policy` with the route's reason
   (`MailClassificationResult.NoPolicy`).
2. **The linked Case's state classifies what no predicate did.** Core owns
   `CaseStateMailClassification.Derive(current, state)`
   (`src/Pegasus.Core/Intake/Classification/CaseStateMailClassification.cs`):
   a Report sent Case gives `Query`, an in-progress Case gives
   `Case update`, a closed Case gives nothing, and a `Classified` decision is
   never overridden.
3. **Applied inside the link transaction.** `EfIntakeMutationStore` applies
   the derivation for mailbox receipts in both automatic association and the
   staff link, as a version-1 automated decision, before `CaseQueryLedger`
   reads the classification. No history row is written, as for any automated
   re-evaluation; a later correction records its own history.
4. **The mailbox carries no purpose.** No mailbox field or policy input
   says what a mailbox is for.

## Consequences

- A Principal query to a Report sent Case raises a Case query on arrival and
  the Query received notice; the Query response AI job recognises it.
- Non-Principal mail can be corrected to any category and appears in the
  Inbox Unidentified scope while its item is open.
- Mail linked to an in-progress Case is labelled Case update without staff
  action; staff correct it when it is something more specific.
- Per-Principal correspondence reference grammars remain a follow-on in
  `PrincipalCaseMatchPolicy`; until then non-QDOS correspondence without
  instruction keys stays staff-classified.
