---
id: ADR-0036
status: accepted
date: 2026-08-28
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: [frd-08]
tags: [mailbox, outbound-mail, graph]
---
# ADR-0036: Outbound mail via the approved mailbox

## Status

Accepted 2026-08-28 by the Collision Engineers product owner (EPIC-011
decision D4). Acceptance authorises this technical decision only; the
production activation it names is a separately approved step.

## Context

Until now Pegasus has read approved mailboxes and never written to one:
Case association, classification and folder recommendation leave the mailbox
untouched, and the one mutation seam that exists — the confirmed folder move
through `IRetainedMailFolderMover` — is composed only by explicit
configuration and is otherwise the unavailable implementation. The
[boundaries](../boundaries.md) listed automated correspondence as excluded
until activation.

The operator has asked for staff to reply to, forward and compose mail from
the Inbox message and the Case correspondence surfaces without leaving
Pegasus, and to flag or delete a retained message. [FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence)
already defines the only outbound evidence Pegasus trusts: an exact immutable
Sent item in an approved mailbox, retained by the Sent-evidence poll and
linked to one Case. The product invariant that local alpha work never mutates
an Outlook mailbox, and the safety rail that every live external write needs
explicit approval, both still bind.

## Decision

Staff-initiated Reply, Forward and Compose from the Inbox message and Case
correspondence surfaces are sent through Microsoft Graph as the approved
mailbox identity — the approved mailbox the message belongs to, or the default
approved mailbox for Compose — never as the staff member's own identity or any
non-approved identity.

The Sent item Graph writes for that send **is** the Sent evidence FRD-08
already defines. Pegasus does not keep a second outbound record: the existing
Sent-evidence poll retains the item and auto-links it to the Case named at
send time, and the draft text is not evidence until that item exists.

Flag and Delete are mailbox mutations through the same seam as the confirmed
folder move. Delete moves the item to Deleted Items — it is never a hard
delete — and the item stays reachable through the existing read-only
Deleted Items search.

The capability is composed only by explicit configuration, following the
composed-or-absent pattern of `IRetainedMailFolderMover`: absent, the
surfaces carry no send, flag or delete control at all. Local alpha and every
test profile use the unavailable implementation and never mutate a mailbox.
Enabling it in production is a separately approved live write under the
[runbook approval matrix](../runbook.md#live-operation-approval-matrix).

## Consequences

- The application registration needs Graph `Mail.Send` and `Mail.ReadWrite`
  for the approved mailboxes, granted as a separately approved tenant change
  and kept apart from the read/intake scopes as FRD-08 requires.
- The Sent-evidence poll (`SentEvidencePollFunction`, ADR-0024 §4) is part of
  the activation set: a send whose Sent item is never polled has no evidence,
  so activation without it is refused.
- The [boundaries](../boundaries.md) correspondence row now reads
  staff-initiated send from an approved mailbox as in scope; autonomous or
  automated sending, template campaigns, and sending from any non-approved
  identity remain excluded. No autonomous send exists after this decision.
- No new project, store, migration stream, runtime, or deployment unit is
  introduced; the Graph adapter, Worker poll and Core ports already exist.

## Links

- Behaviour: [FRD-08 § Outbound correspondence](../frd/frd-08-email-mailbox-and-background-processing.md#outbound-correspondence)
  and [§ EVA-sent report detection](../frd/frd-08-email-mailbox-and-background-processing.md#eva-sent-report-detection).
- Mailbox identity and activation layers: [ADR-0024](0024-stable-approved-mailbox-identity-and-explicit-baseline.md).
- Report-sent business event: [FRD-11](../frd/frd-11-reports-correspondence-and-reviewed-proposals.md).
