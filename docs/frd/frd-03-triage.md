# FRD-03: Triage

> Owner capabilities: TRI-01 to TRI-07, TRI-09 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Triage is a Case type for an assessment request that is not a definitive
  instruction. Its Case/PO is `t.` plus the next number from the Principal's
  shared sequence, for example `t.QDOS26003`.
- A Triage Case is created only once its Principal is established and its
  registration is known: by route classification, a Provider API
  declaration, Open the Triage on an Unidentified item, or Create case.
- It moves `Open` → `Awaiting information` → `Finding recorded` →
  `Completed`, or `Cancelled`. Sending a message is never required.
- Its page is `/Cases/{id}`. It has Case Files with upload, a due target and
  a manual chaser.
- It may link to one later instruction Case, which has its own number.
  Pegasus can make that link automatically when exactly one Case matches.

## Purpose

This document owns how a Triage Case is created, worked, decided and linked
to an instruction Case. Case identity and the shared sequence are owned by
[FRD-01](frd-01-case-identity-and-lifecycle.md). The Triage queue, the
Triages metric, Search and the Triage Case page are owned by
[FRD-15](frd-15-work-centre-queues-and-search.md). An open Triage never gets
a U-reference; a Triage request whose Principal is not established, or that
has no registration, is held in Unidentified until both are known
([FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference)).

## Behaviour

### Triage

#### Normal workflow and completion evidence

**Case identity and reference.** Triage is a Case type. A Triage Case
records an assessment request; it does not constitute a definitive
instruction. It consumes the next number from its Principal lineage's
current-year Case sequence, which it shares with Inspection, Audit and
Inspection + Audit Cases. Its Case/PO is `t.` plus that base reference, for
example `t.QDOS26003`: the prefix is lowercase and the Principal code, year
and sequence follow the normal format
([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
The Case/PO never changes and is never reused. A Triage has no other
reference.

**Principal first.** Pegasus establishes the Principal before it classifies
a request as a Triage, and creates a Triage Case only for an established
Principal and a known registration. A request whose Principal is not
established is not a Triage: intake records it as Unidentified, and **Open
the Triage** is not offered on an item whose receipt has no established
Principal. A Triage Case takes its receipt's Principal. That Principal is
fixed with its Case/PO: Triage has no Set principal, and Correct principal
is not offered on a Triage Case.

**How a Triage starts.** One of four ways:

- the accepted route policy classifies a provider request as an assessment
  request;
- an authenticated Principal declares one over the Provider API;
- an authorised staff member classifies safely retained, attributable
  material on an Unidentified item as Triage with **Open the Triage**;
- an authorised staff member chooses Triage in **Create case**, giving only
  the Principal and the registration.

Automatic creation, from a route classification or a Provider API
declaration, rests on accepted route evidence, the same trust as allocating
an instructed Case. It has no approval step
([ADR-0056](../adr/0056-one-case-per-work-data-and-triage-case-type.md)). A
declared Triage carries the Principal's declaration as its match evidence,
stamped with that policy's key and version, and opens exactly like a
classified one. Open the Triage records the source, the route evidence
available, the actor, time, reason and policy version. Create case records
the staff actor and the operation; it is the only way to create a Triage
without source material, and it invents no intake receipt or source. None of
these invents a Principal. Material whose route or category is not accepted
stays `Unidentified`; it never becomes a Triage or another Case by fallback.

**Automatic creation from intake** follows the same rule. When the route
classification records a received message as a Triage request, Pegasus does
not treat it as an instruction. The classification decision itself is the
match evidence, with its policy key and version on the record. With an
established Principal and a known registration, Pegasus allocates the `t.`
Case/PO, opens the Triage Case as `Open`, records its origin receipt and
history, and starts standard Case custody, all in one transaction. No known
registration registers the material as Unidentified with its reason and
opens no Triage until a registration is known. A message classified as
Ambiguous opens no Triage and goes to staff.

**Registration and states.** Without a registration the request stays
`Unidentified`. With one, the Triage opens as `Open`, may move to
`Awaiting information`, records an accepted finding as `Finding recorded`,
and reaches `Completed` when the authorised actor records the outcome.
Sending a message is not required. An acknowledgement, a request for
information, a draft, a queue action or other correspondence may be kept, but
none of them is a finding or completion evidence.

**History and notes.** `History` shows the record's events and staff notes
in one chronological order. Notes are append-only. Each carries its author,
time and text. A correction is a new note; notes cannot be edited or deleted
on any screen or through any caller. A Triage Case keeps these notes; Case
Notes do not apply to it.

**Files.** A Triage Case has standard Case custody: a Box case folder, as for
any Case, holding the retained request source and its attachments and any
staff uploads. Its Files panel shows the folder's state, **Add evidence**
(which opens Upload) and the documents, each with view and download. Staff
may link uploaded material to a Triage Case in any state
([FRD-18](frd-18-manual-upload.md#upload-confirmation-surface)). Vehicle
images linked to the Triage are shown with it. A Triage created with Create
case has no retained source. Triage has no separate file store.

**Editing.** An existing Triage Case is read-only until a staff member
presses Edit, which claims the Triage edit scope for that one Triage Case,
not its queue or its linked instruction Case
([FRD-14](frd-14-record-edit-leases.md#record-edit-scopes); takeover follows
[FRD-14](frd-14-record-edit-leases.md#take-over)). Every change, including
assignment, findings, notes, response evidence, state and Case association,
rechecks the holder, token and Triage version in its own transaction. Manual
Case association also needs the separate current edit lease of the
instruction Case; neither scope stands in for the other. Where Case custody
or upload association needs edit authority on a Triage Case, the Triage edit
scope and Triage version are that authority. A custody completion by the
system never invalidates a staff edit scope.

**Findings.** A finding has two dimensions, each optional:

- Roadworthiness: `Roadworthy` or `Unroadworthy`;
- Assessment: `Repairable` or `Total loss`.

At least one is required. A correction records a new finding with a reason
and keeps the earlier decision and correspondence. Recording the corrected
outcome can complete the Triage without sending another message.

**Completion.** Completion records the outcome, actor and time. **Reply with
outcome** is optional. It opens the email feature with a preset outcome
template the user can edit. A sent reply is linked to the Triage through the
normal email evidence rules, but neither composing nor sending is a gate.
`Cancelled` closes a Triage without a finding. Neither outcome turns a Triage
finding into an instruction for a later Case.

**Assignee.** A Triage may have an assignee. Choosing or changing the
assigned Engineer is an ordinary edit available to every enabled staff role,
including self-assignment where the existing state and edit-scope rules
allow it. It needs no reason; history records the assignment, actor and
time. Findings, cancellation, reopening and Case-association decisions keep
their required reasons.

**Due target and chaser.** A Triage Case without a finding is due at the
Triage target after it opened, a Workflow configuration setting in whole
calendar days with default 1
([FRD-17](frd-17-administration-workspace.md#workflow-configuration)), and
appears in Needs attention until then
([FRD-15](frd-15-work-centre-queues-and-search.md#work-centre)). Staff may
send a **chaser**: a reply to the Triage's origin message from its approved
mailbox, offered when the Triage came by e-mail and that mailbox may send.
The chaser is manual, never automatic and never a completion gate. Its exact
Sent evidence is recorded under the normal rules
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence-evidence)).

**Case link.** A Triage may link to at most one current instruction Case; a
Case may have many Triages. A later instruction allocates its own next
number and Case/PO. Linking records the link in both histories but does not
complete, cancel, convert or renumber the Triage Case. Every staff role in
the
[staff role access matrix](frd-04-parties-accounts-and-access.md#staff-role-access-matrix)
may unlink or relink with a reason. The prior and current Case, actor, time,
reason and evidence stay in history permanently.

**Cancel and reopen** both need a reason. Reopen always returns to `Open`
and never erases the earlier finding, reply, actor or chronology.

#### Automatic association with a formal Case

When the Triage registration is accepted and the retained typed source
identity agrees, Pegasus may find exactly one instruction Case of the same
Principal through the same principal-scoped matcher intake uses. It
withholds the link when any of these apply: a contradictory registration, a
competing identity, a cancelled Triage, an incompatible target identity, or
a live staff Case edit lease. A Created-in-error replacement Case must match
on its own and keep the Triage's Principal. A Completed Triage may be linked
without reopening.

The same link is attempted whichever record arrives first, and again on
creation or acceptance replay. The reconciliation timer retries eligible
unlinked records, oldest current matches first, up to its batch limit. A
non-match stays retryable when new evidence or an instruction Case arrives.
Recoverable failures are visible and do not block unrelated links.

The automatic write rechecks the origin, evaluation, Principal, candidate
uniqueness, target identity and versions inside its transaction. It records
one SystemWorker link in both Triage and Case history. It keeps the Triage
Case/PO, findings and state, and allocates nothing. Recovery never reverses a
deliberate staff unlink or reassignment. Manual linking keeps its staff
authority, reason and current Case edit lease.

## States and transitions

| From | To | Trigger |
| --- | --- | --- |
| (none) | `Open` | Classification, Principal declaration, Open the Triage or Create case, with an established Principal and a known registration |
| `Open` | `Awaiting information` | Staff mark it waiting |
| `Open`, `Awaiting information` | `Finding recorded` | A finding with at least one dimension is recorded |
| `Finding recorded` | `Completed` | The authorised actor records the outcome |
| any open state | `Cancelled` | Cancel with a reason |
| `Completed`, `Cancelled` | `Open` | Reopen with a reason |

## Edge cases and fail-closed behaviour

- No established Principal: no Triage opens; the material waits in
  Unidentified and allocates nothing.
- No registration: no Triage opens; the material waits in Unidentified.
- Ambiguous classification: no Triage opens; staff decide.
- A finding with neither dimension is refused.
- Automatic linking stops when more than one Case matches, or a Case edit
  lease is live; it retries later.
- A chaser is not offered for a Triage that did not come by e-mail, or whose
  mailbox may not send.
- A stale scope, token or version changes nothing.

## Acceptance evidence

Core tests cover the `t.` reference and shared allocation, the Principal
gate, creation with only a Principal and a registration, the state
transitions above, findings and corrections, the due target and the Triages
count, and automatic association with its withholding rules. Integration
tests cover concurrent allocation beside other Case types, Triage custody for
intake and Create case Triages, uploaded material linked to a Triage Case in
any state, and the Triage Case page at `/Cases/{id}` over real HTTP,
including the edit scope. Deployment and live acceptance are separate
evidence tiers ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `TRI-01`–`TRI-07`, `TRI-09` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-04](frd-04-parties-accounts-and-access.md),
  [FRD-09](frd-09-provider-and-intermediary-routes.md),
  [FRD-10](frd-10-mcp-automation-and-actor-boundary.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-15](frd-15-work-centre-queues-and-search.md),
  [FRD-18](frd-18-manual-upload.md),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md).
- Technical constraints:
  [ADR-0056](../adr/0056-one-case-per-work-data-and-triage-case-type.md)
  (Triage as a Case type).
