---
id: ADR-0068
status: accepted
date: 2026-10-12
supersedes: [ADR-0024, ADR-0036, ADR-0064]
superseded_by: []
related_capabilities: [MAIL-12, MAIL-14, MAIL-18, INT-05, INT-06, INT-07]
related_frd: [FRD-10, FRD-11, FRD-13, FRD-16, FRD-20, FRD-21, FRD-26]
tags: [mailbox, outbound-mail, graph, migration]
---

# ADR-0068: Incoming and Outgoing mailboxes, and Case mail replies to its instruction

## Status

Accepted on 12 October 2026 by the operator.
[FRD-26](../frd/frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md#mailbox-allowlist-activation-and-wipe)
owns what an approved mailbox is used for and
[FRD-21](../frd/frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)
owns which mailbox a send leaves from. This record owns how the route scopes
collapsed, what replaced the default sender and the per-mailbox size limit,
and where the one sender rule lives.

This record replaces, in part:

- [ADR-0036](0036-outbound-mail-via-approved-mailbox.md): Compose and "the
  default approved mailbox for Compose". The approved-mailbox send, its Sent
  evidence and its composed-or-absent activation stand.
- [ADR-0024](0024-stable-approved-mailbox-identity-and-explicit-baseline.md)
  §4's separately approved Sent-evidence activation and its consequence that
  Sent-evidence polling is off by default and not implied. Its stable
  identity, fresh start and Worker read-only policy stand.
- [ADR-0064](0064-automation-actor-staff-casework-parity.md) decision 9's
  "`pegasus_mail_send` sends new mail". The Actor's send parity stands.

## Context

An approved mailbox carried three route scopes: inbound intake, Sent
evidence and staff send. A report left the one mailbox holding both staff
send and Sent evidence, so a second such mailbox refused every report.
Compose and the Case chaser left a single default sender an Administrator
chose. Each staff-send mailbox needed a recorded encoded-message size limit,
and an email could not be answered while an earlier reply on it was pending.
Reports and chasers went as new mail, outside the thread the Principal
instructed in.

## Decision

1. `ApprovedMailboxRouteScope` is `Incoming` or `Outgoing`. Incoming reads
   the Inbox into intake; Outgoing sends staff mail as the mailbox and reads
   its Sent Items as the evidence of what left. A message's business route
   is decided per message by classification, never by its mailbox.
   Migration `20261012120000_MailboxIncomingOutgoing` maps the old scopes,
   drops the default-sender flag and the per-mailbox size limit.
2. `ApprovedMailbox.CanSend` is the one send-ready rule: Approved,
   Outgoing, activated, identity and Sent folder bound, generation set. The
   report send, Send chaser, the Triage reply, Reply on the message record
   and the Inbox, and the MCP send tools all read it.
3. `ResolveCaseMailSender` (`src/Pegasus.Core/Operations/CaseMailSender.cs`)
   owns where a Case's report and chaser leave: a Graph reply
   (`createReply`, "Re:" and the instruction's subject) to the Case's
   instruction email, the retained message its origin receipt came from,
   from the mailbox it arrived in while that mailbox can send; otherwise a
   new message from the Outgoing mailbox the caller names. The reviewed
   recipients stay the only recipients.
4. Every Outgoing mailbox sends under one fixed limit,
   `ApprovedStaffSendMailbox.OutgoingEncodedMessageSizeLimit`
   (36,700,160 encoded bytes).
5. Compose, the default sender and the pending-reply refusal are removed.

## Consequences

- An Outgoing mailbox needs the Web identity's scoped `Mail.ReadWrite` and
  `Mail.Send` as well as `Mail.Read`; admitting a mailbox to the tenant
  stays a separately authorised step
  ([runbook](../runbook.md#runbook-admitting-a-new-mailbox-to-the-tenant)).
- The Sent-evidence poll reads every Outgoing mailbox while the Worker and
  that function are enabled; no separate activation is needed.
- `pegasus_mail_send` loses `new` and gains `chaser`; it and
  `pegasus_report_send` take `fromMailbox` only for a Case with no
  instruction reply to make
  ([FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md#sending)).
- The migration is destructive: the default-sender and size-limit columns
  and their values do not return.

## Links

- [FRD-21](../frd/frd-21-outbound-correspondence-and-sent-evidence.md),
  [FRD-26](../frd/frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md),
  [FRD-11](../frd/frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md).
