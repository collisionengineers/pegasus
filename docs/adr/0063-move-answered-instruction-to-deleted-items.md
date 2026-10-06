---
id: ADR-0063
status: accepted
date: 2026-10-05
supersedes: [ADR-0052]
superseded_by: []
related_capabilities: [MAIL-25]
related_frd: [FRD-21]
tags: [mailbox, outbound-mail, worker, graph]
---

# ADR-0063: Move the answered instruction to Deleted Items after a confirmed report send

## Status

Accepted on 5 October 2026, recording the operator's decision D4 of the report
dispatch interview (3 to 5 October 2026). It supersedes
[ADR-0052](0052-dismiss-by-logical-folder.md) in clause 3 only, and with it
the ADR-0036 consequence that no action removes a mailbox item. Dismiss,
the logical `Dismissed` folder and the rest of ADR-0052 stand. Send, the
approved-mailbox identity and the Sent-item evidence rule stand under
[ADR-0036](0036-outbound-mail-via-approved-mailbox.md) and ADR-0042.

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
2. **No Delete control.** There is still no Delete or Flag control, no staff
   action and no tool that moves anything to Deleted Items. The move is a
   consequence of a confirmed report send and nothing else.
3. **The Worker does it.** The Worker moves the message inside its existing
   recovery timer. Web keeps the unavailable mover and cannot move a message.
   The move needs the Worker's Mail.ReadWrite on that mailbox (an operations
   step).
4. **Bounded and recorded.** A report send is due for the move while its
   instruction has not been tidied and it has had fewer than three failed
   attempts. A message already in Deleted Items, or no longer in the mailbox,
   is recorded and not moved. Every outcome is kept on the send operation.
   A move writes Case history `instruction_moved_to_deleted_items`. A failure
   writes `instruction_move_failed` with a failure code on each attempt.
   After the third failure nothing retries; the instruction stays where it is.
5. **Only that message.** The candidate is the retained instruction the report
   send answered, matched by the send's recorded original message. No other
   message, folder or mailbox is touched.

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

## Links

- [FRD-21 — Outbound correspondence](../frd/frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)
- [ADR-0052](0052-dismiss-by-logical-folder.md)
- [ADR-0036](0036-outbound-mail-via-approved-mailbox.md)
