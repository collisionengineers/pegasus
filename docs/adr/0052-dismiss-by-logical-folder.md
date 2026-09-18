---
id: ADR-0052
status: accepted
date: 2026-09-16
supersedes: [ADR-0036]
superseded_by: []
related_capabilities: []
related_frd: [frd-08, frd-12]
tags: [mailbox, inbox, persistence]
---

# ADR-0052: Dismiss a retained message by logical folder; no Flag or Delete

## Status

Accepted, recording the operator's 13 September 2026 decision (v26 planning,
Inbox) that Stage 2 implemented. Partially supersedes ADR-0036's Flag and
Delete clause only. Send, the approved-mailbox identity, the Sent-item
evidence rule and the composed-or-absent pattern remain accepted under
ADR-0036 and ADR-0042.

## Context

ADR-0036 decided that staff could flag or delete a retained message as
mailbox mutations through the confirmed folder-move seam. Neither was built:
the v26 replica of every live page found no Flag or Delete handler and dropped
both controls. What the operator wanted from the Inbox was narrower: a way to
take a message out of the incoming lists without classifying, linking or
touching it in Outlook, and a way to get it back.

## Decision

1. **Dismiss is a Pegasus-side move.** A retained message can be dismissed
   from a list row or from its record. Dismissing records a dismissed-at time
   on the retained message and places it in the `Dismissed` logical folder of
   the existing mail logical-folder vocabulary. Nothing is sent to Microsoft
   Graph; the Outlook item does not move.
2. **Dismissed is a scope, not a deletion.** Dismissed messages appear under
   the Dismissed scope and nowhere else, keep their evidence, associations and
   history, and are restored from that scope. Classification never recommends
   the Dismissed folder. Dismiss and Restore are always allowed, including on
   a message with an open Unidentified item, which stays open.
3. **Flag and Delete are withdrawn.** No Flag control, Delete control or
   Deleted Items move exists on any surface, and none is planned. The
   ADR-0036 rule that no action removes a mailbox item irrecoverably stands
   because no action removes one at all.

## Consequences

- The only mailbox mutations Pegasus performs remain the confirmed folder
  move and the ADR-0036 send; Dismiss adds no Graph scope or seam.
- FRD-08's Flag and Delete bullets are replaced by Dismiss; FRD-20 owns the
  Inbox scopes and controls.
- A retained message's dismissed state is Pegasus data and survives mailbox
  re-polls; a later Outlook move by a person does not undo a Dismiss.

## Links

- [FRD-21 — Outbound correspondence](../frd/frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)
- [FRD-20 — Inbox](../frd/frd-20-mailbox-workspace.md#inbox-scopes-and-filters)
- [ADR-0036](0036-outbound-mail-via-approved-mailbox.md)
- Provenance: [v26 planning, Inbox](../../design/planning-and-old-designs/v26_planning/pages/inbox/how-it-should-work.md)
