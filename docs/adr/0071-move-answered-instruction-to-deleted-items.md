---
id: ADR-0071
status: accepted
date: 2026-10-05
supersedes: [ADR-0052]
superseded_by: []
related_capabilities: [MAIL-25]
related_frd: [FRD-21]
tags: [mailbox, outbound-mail, worker, graph]
---

# ADR-0071: Move the answered instruction to Deleted Items after a confirmed report send

## Status

Accepted on 5 October 2026, recording the operator's decision D4 of the report
dispatch interview (3 to 5 October 2026). It supersedes
[ADR-0052](0052-dismiss-by-logical-folder.md) in clause 3 only, and with it
the ADR-0036 consequence that no action removes a mailbox item. Dismiss and
the rest of ADR-0052, as [ADR-0069](0069-one-flat-mail-category.md) leaves
it, stand. Send, the
approved-mailbox identity and the Sent-item evidence rule stand under
[ADR-0036](0036-outbound-mail-via-approved-mailbox.md) and ADR-0042.

Amended 9 November 2026 (R902 to R905): clauses 2 and 5 and the clause 4
record are edited in place for the **Delete case correspondence** tick.
Clauses 1 and 3 stand as written. Amended 10 November 2026 (R916, R917):
clause 5 names the activated mailbox and a staff forward's original.
Amended 11 November 2026 (R956 to R961): clause 4 says where the outcome is
shown, and clause 5 names pegasustest@, the one original of a forward and
the activated mailbox for the automatic move.

## Context

A report goes out as a reply in the instruction's thread from the mailbox the
instruction arrived in. Once the Sent item is confirmed, the instruction is
answered. Andy's dispatch routine deleted the answered instruction so the
Inbox held only open work. ADR-0052 withdrew every Deleted Items move, so
Pegasus would leave every answered instruction in the Inbox.

## Decision

1. **One automatic move.** After a report send is confirmed by its Sent item,
   Pegasus moves the instruction e-mail it replied to into the mailbox's
   Deleted Items folder. The move is recoverable: staff and Outlook can take
   the item back out. Pegasus never deletes a mailbox item permanently.
2. **No Delete control; one tick.** There is still no Delete or Flag control
   and no staff action or tool that moves a chosen message to Deleted Items.
   Besides the automatic move of clause 1, one optional tick, **Delete case
   correspondence**, rides on three sends: Send report, a reply that answers
   a query (offered exactly where Mark query done is) and the Triage outcome
   reply. `pegasus_report_send` and `pegasus_mail_send` take it as
   `deleteCaseCorrespondence` and stay non-destructive tools, because the
   move is recoverable. A move is a consequence of a confirmed send and
   nothing else (9 November 2026, R902, R904, R905).
3. **The Worker does it.** The Worker moves the message inside its existing
   recovery timer. Web composes no mover and cannot move a message.
   The move needs the Worker's Mail.ReadWrite on that mailbox (an operations
   step).
4. **Bounded and recorded.** A report send is due for the move while its
   instruction has not been tidied and it has had fewer than three failed
   attempts. A message already in Deleted Items, or no longer in the mailbox,
   is recorded and not moved. Every outcome is kept on the send operation.
   A move writes Case history `instruction_moved_to_deleted_items`. A failure
   writes `instruction_move_failed` with a failure code on each attempt.
   After the third failure nothing retries; the instruction stays where it is.
   A ticked send is recorded the same way in its own columns of the send
   operation: a run with no failure ends it and writes
   `correspondence_moved_to_deleted_items` with its counts; a run with a
   failure writes `correspondence_move_failed`, and the third such run ends
   it (9 November 2026, R902). The four events are written where the Notes
   tab reads them, the Case's workflow events or the Triage Case's own
   history, at the version as it stands; `CaseHistory`, which nothing shows,
   no longer receives them (11 November 2026, R959).
5. **Only those messages.** The automatic move's candidate is the retained
   instruction the report send answered, matched by the send's recorded
   original message, in a mailbox that is Approved and activated
   (11 November 2026, R961). A ticked send's candidates are the Case's received
   e-mails: the Inbox-scope retained messages currently linked to the Case,
   in a mailbox that is still Approved and activated (10 November 2026,
   R916), that Pegasus held when the send was prepared. With a Collision
   Engineers staff forward held in instructions@, or in the test instance's
   pegasustest@ (11 November 2026, R956), goes the one message it answers in
   the mailbox that forwarded it, linked to the Case or not (10 November
   2026, R917), never the earlier messages of its thread (R957), and not
   when it stands linked to another Case (R958).
   Pegasus's own Sent items, uploaded files, mail that arrives after the
   send and every other message, folder and mailbox are left
   (9 November 2026, R903).

## Consequences

- The Worker needs Mail.ReadWrite on every mailbox that receives
  instructions. A refusal is the likeliest live failure and shows as
  `mailbox_not_permitted`.
- The send operation row gains the tidy state, attempt count, moved time and
  failure code. Existing grants cover them.
- The Inbox poll sees the instruction leave the Inbox as it does for any
  Outlook move. The retained message keeps its evidence, association and
  history.
- FRD-21's "No Flag, no Delete" paragraph becomes "No Flag, no Delete
  control", with the one automatic move.
- The tick (9 November 2026) adds five columns and one filtered index to the
  send operation row by an additive migration, and a fourth job folded into
  the Worker's recovery timer after the instruction tidy. A production copy
  into the test instance closes the copied ticked sends as it closes the
  copied instruction tidies (R680).

## Links

- [FRD-21 — Outbound correspondence](../frd/frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)
- [ADR-0052](0052-dismiss-by-logical-folder.md)
- [ADR-0036](0036-outbound-mail-via-approved-mailbox.md)
