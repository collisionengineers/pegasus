---
id: ADR-0024
status: accepted
date: 2026-08-10
supersedes: [ADR-0022]
superseded_by: []
related_capabilities: []
related_frd: [frd-08]
tags: [mailbox, identity]
---
# ADR-0024: Stable approved-mailbox identity and per-mailbox fresh start

- Date: 2026-08-10
- Status: accepted 2026-08-11 by Collision Engineers product owner
- Owners: Collision Engineers product owner and Pegasus development team
- Relation: supersedes ADR-0022's Graph-identity poll key and its
  cursor-carrying, cursor-preserving re-enablement. It carries forward
  ADR-0022's durable estate decision — approved inbound mailboxes are
  administrator-owned database policy, not deployment configuration, and the
  Worker stays read-only on that policy. ADR-0022 remains the historical record
  for the clauses not restated here.

## Context

ADR-0022 moved the choice of approved inbound mailboxes into the
administrator-owned `ApprovedMailboxes` estate and kept the Worker read-only on
that policy table. It keyed poll state on a Graph mailbox identity and promised
to preserve the delta cursor on re-enablement. A Graph delta cursor is valid
only for the mailbox, folder, and request shape that minted it, so it cannot be
the durable identity of an inbound source, and preserving it across a re-enable
couples Pegasus state to replaceable provider coordinates.

Pegasus is not yet released and will not create a pre-launch mail backlog, so an
administrator may switch an approved inbound mailbox on or off independently, and
every switch-on begins a fresh activation cycle at its recorded UTC time (§2).
This decision concerns Pegasus operational data only; it never authorises
clearing, moving, or otherwise mutating Outlook mailboxes or messages. Of the
Worker functions, only `InboxPollFunction` reads approved incoming mail and
`SentEvidencePollFunction` reads approved Sent folders; the latter is a separate
capability that must not become active merely because inbound mail is activated
(§4).

## Decision

### 1. Stable source identity and replaceable provider coordinates

`ApprovedMailbox.Id` (`Guid`) is the durable Pegasus identity of one approved
inbound mail source. Inbound poll state, poison occurrences, retained messages,
source duplicate protection, activation cycles, freshness, and telemetry relate
to that stable ID.

Graph mailbox and Inbox-folder identities are replaceable provider coordinates,
not Pegasus identities. They remain administrator-controlled fields on the
existing `ApprovedMailboxes` row. Replacing either coordinate requires the row
to be `Disabled`, an authenticated reason, optimistic-version agreement, and
the existing permanent action-history record. It does not create a new
`ApprovedMailbox.Id` and never re-keys operational state.

ADR-0022's approval re-check inside the claiming transaction remains. The
Worker continues to read which rows are approved; it does not author, repair,
or replace their coordinates.

### 2. Each mailbox has its own fresh-start activation cycle

`Disabled → Approved` begins a new activation cycle at a recorded UTC time,
chosen after the mailbox is disabled and before its inbound polling is enabled.
Re-enabling never resumes an old cursor and never turns mail received while
disabled into a backlog: mail received before the recorded time may advance the
cursor but is never retained, quarantined, passed to Intake, or allocated.

Each mailbox activates independently. A provider-coordinate change, a
cursor-scope mismatch, Graph `410 Gone`, or incomplete scope evidence disables
that one mailbox and requires the same explicit fresh-start cycle; one failing
mailbox never activates itself silently or makes another valid mailbox process
its mail. The activation cycle's data model and message-by-message handling are
specified in
[FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md).

### 3. Launch receipt-identity model

There is no v1-to-v2 receipt backfill in this launch model. Inbound receipt
identity is the stable `(ApprovedMailbox.Id, Graph ImmutableMessageId)`
occurrence, so changing Graph coordinates cannot create a second receipt for the
same message.

A one-time reset of obsolete pre-launch inbound operational data (old poll and
receipt state) is an operational procedure requiring its own exact-target
approval under the [runbook live-operation approval
matrix](../runbook.md#live-operation-approval-matrix); it is not authorised by
this ADR or by a deployment switch, and it may never delete a Case, a
Case-linked record, or retained business evidence.

### 4. Three separate control layers

Pegasus has three deliberately separate control layers:

| Control | Owner | Meaning |
| --- | --- | --- |
| Global Worker switch | reviewed deployment/release | Starts or stops all Worker execution for emergency containment or maintenance. It does not change any mailbox's activation cycle. |
| Individual Worker-function switches | reviewed deployment/release | Enable or disable each deployed Function. They are operational controls, not ordinary mailbox administration. |
| Per-mailbox `Approved`/`Disabled` state and activation cycle | authorised Pegasus administrator | Selects which inbound mailboxes Pegasus may read and, on each enablement, the exact fresh-start time. |

The release configuration states the exact per-function settings rather than
equating an application mode with every Worker function, and an absent,
malformed, or unapproved Worker configuration fails closed. A global Worker stop
leaves all per-mailbox settings and activation times intact; starting it again
does not create a backlog. Which functions an inbound activation enables, and
the dependency set implementation must inventory and test, are specified in
[FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md);
`SentEvidencePollFunction` stays disabled unless a separately reviewed and
explicitly approved Sent-evidence activation enables it.

### 5. Existing boundaries carry the change

Implementation uses `Pegasus.Core` policy and ports, the existing
`Pegasus.Infrastructure` adapters and EF migration stream, the existing
`Pegasus.Worker` composition root and Function App, and the existing production
SQL database. No new top-level directory, project, store, migration stream,
runtime, process, Function App, deployment unit, operator cursor tool, Outlook
mailbox/message mutation, or credential is authorised.

The Worker retains `SELECT` only on `ApprovedMailboxes`. It may insert or update
only its existing operational state tables under the reviewed runtime-role
matrix. Web remains the author of administrator-approved mailbox policy. No
broader SQL, Graph, Azure, Outlook, Principal, Box, or operator authority
follows from this decision.

## Consequences

- Pegasus can turn one inbound mailbox on or off without changing another
  mailbox's activation time or cursor.
- Pegasus processes only mail received on or after that mailbox's recorded
  fresh-start time; earlier mail may advance the candidate cursor but cannot
  enter Pegasus business state.
- A mailbox coordinate change or expired Graph cursor becomes a visible
  disable-and-fresh-start requirement, not an automatic replay.
- Sent-evidence polling remains off by default and is not implied by inbound
  activation.
- Existing product invariants still apply: no Case is deleted, allocation
  remains fail-closed, and implementation requires normal migration and
  caller-backed tests in later tickets.
- This ADR records the target technical contract only; implementation,
  migration, deployment, activation, and live verification proceed under later
  tickets and their own approvals.

Functional behaviour: see [FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md).

## Scope of this decision

The Collision Engineers product owner accepted this ADR on 2026-08-11. That
acceptance authorises this technical decision only. No application,
infrastructure, test, Azure, SQL, Outlook, Principal, or Box change is
authorised by this document.
