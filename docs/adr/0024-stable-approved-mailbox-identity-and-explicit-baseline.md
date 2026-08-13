---
id: ADR-0024
status: accepted
date: 2026-08-10
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: [frd-08]
tags: [mailbox, identity]
---
# ADR-0024: Stable approved-mailbox identity and per-mailbox fresh start

- Date: 2026-08-10
- Status: accepted 2026-08-11 by Collision Engineers product owner
- Owners: Collision Engineers product owner and Pegasus development team
- Relation: narrowly supersedes ADR-0022's inbound Graph
  mailbox/folder immutability, disable-and-add consequence, Graph-identity
  poll key, cursor-carrying adoption, and cursor-preservation consequence.
  ADR-0022 remains the historical authority for the administrator-owned
  approved-mailbox estate and every clause not named here.

## Context

ADR-0022 correctly moved the choice of approved inbound mailboxes into the
administrator-owned `ApprovedMailboxes` estate and kept the Worker read-only on
that policy table. Its use of a Graph mailbox identity as the poll-state key,
and its promise to preserve the cursor on re-enablement, couple Pegasus state
to replaceable provider coordinates. A Graph delta cursor is valid only for the
mailbox, folder, and request shape that minted it.

The product decision is intentionally simple: Pegasus is not yet released, so
it will not create a pre-launch mail backlog. An administrator may switch an
approved inbound mailbox on or off independently. Every switch-on begins a
fresh activation cycle at its recorded UTC time; Pegasus ignores earlier mail
in that mailbox. This decision concerns Pegasus operational data only. It never
authorises clearing, moving, or otherwise mutating Outlook mailboxes or
messages.

The existing Worker has individual function settings, but only two functions
read mail: `InboxPollFunction` reads approved incoming mail and
`SentEvidencePollFunction` reads approved Sent folders. The latter is a separate
capability and must not become active merely because inbound mail is activated.

Production containment left the current Worker disabled. This ADR records the
accepted target contract only; it is not implementation, deployment, or live
verification.

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

An approved inbound mailbox has one current activation cycle. The cycle records
an opaque `ActivationCycleId`, immutable `ActivatedAtUtc`, the exact Graph
mailbox and Inbox-folder coordinates, a versioned cursor-scope fingerprint, a
candidate cursor, and state `Pending | Running | Complete | Failed`.

`Disabled → Approved` creates a new cycle with a new UTC activation time. The
time is chosen after the mailbox is disabled and before its inbound polling is
enabled. During the cycle, pages advance the candidate cursor. A message with
`ReceivedAtUtc < ActivatedAtUtc` advances that cursor but is not retained,
quarantined, passed to Intake, or allocated. A message at or after the time
follows the normal exactly-once route. Completion records Graph's terminal
delta link and atomically promotes the matching candidate cursor to active
state.

Disabling stops polling and changes no Outlook content. Re-enabling creates a
new fresh-start cycle; it does not resume an old cursor or turn mail received
while disabled into a backlog. A provider-coordinate change, a cursor-scope
mismatch, Graph `410 Gone`, or incomplete scope evidence disables that mailbox
and requires the same explicit fresh-start cycle. One failing mailbox must not
activate itself silently or make another valid mailbox process its mail.

### 3. Pre-launch Pegasus operational-data reset

Before first production mailbox activation, implementation may perform one
separately approved, exact-target reset of obsolete Pegasus inbound operational
data, including old poll and receipt state. It is not authorised by this ADR or
by a deployment switch. The later procedure must enumerate its exact database
targets, rehearse read-only, prove the recovery boundary, and refuse to delete
a Case, a Case-linked record, or retained business evidence.

There is no v1-to-v2 receipt backfill in this launch model. After the reset,
new inbound receipt identity is based on the stable
`(ApprovedMailbox.Id, Graph ImmutableMessageId)` occurrence, so changing Graph
coordinates cannot create a second receipt for the same message.

### 4. Three separate controls

Pegasus has three deliberately separate control layers:

| Control | Owner | Meaning |
| --- | --- | --- |
| Global Worker switch | reviewed deployment/release | Starts or stops all Worker execution for emergency containment or maintenance. It does not change any mailbox's activation cycle. |
| Individual Worker-function switches | reviewed deployment/release | Enable or disable each deployed Function. They are operational controls, not ordinary mailbox administration. |
| Per-mailbox `Approved`/`Disabled` state and activation cycle | authorised Pegasus administrator | Selects which inbound mailboxes Pegasus may read and, on each enablement, the exact fresh-start time. |

The release configuration must state the exact function settings rather than
equating an application mode with every Worker function. An inbound activation
enables `InboxPollFunction` and only the existing dispatch, queue, recovery, and
reconciliation functions that its real caller path requires. The implementation
must inventory and test that dependency set. `SentEvidencePollFunction` remains
disabled unless a separately reviewed and explicitly approved Sent-evidence
activation enables it.

An absent, malformed, or unapproved Worker configuration fails closed. A
global Worker stop leaves all per-mailbox settings and activation times intact;
starting it again does not create a backlog.

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
- Production remains in its separately evidenced contained state until later
  tickets implement, deploy, activate, and live-verify the accepted contract
  under their own approvals.

## Implementation boundary

The Collision Engineers product owner accepted this ADR on 2026-08-11. That
acceptance authorises this technical decision, not its implementation or any
live operation. No application, infrastructure, test, Azure, SQL, Outlook,
Principal, or Box change is authorised by this document.
