# FRD-03: Triage

> Owner capabilities: TRI-01 to TRI-07, TRI-09 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Triage is a separate pre-Case record for an assessment request. It has its
  own `T-00001` style reference and never gets a Case/PO.
- A Triage opens when a route classifies a request as Triage, a Principal
  declares one over the Provider API, or staff classify retained material.
- It moves `Open` → `Awaiting information` → `Finding recorded` →
  `Completed`, or `Cancelled`. Sending a message is never required.
- A finding records Roadworthiness, Assessment, or both. A correction is a new
  finding with a reason; nothing earlier is erased.
- A Triage may link to one current Case. Pegasus can make that link
  automatically when exactly one Case matches.

## Purpose

This document owns how a Triage request is created, worked, decided and
linked to a Case. An open Triage never gets a U-reference; a Triage request
with no registration is held in Unidentified until one is known
([FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference)).

## Behaviour

### Triage

#### Normal workflow and completion evidence

**Reference.** Each Triage gets the next global reference: `T-00001`,
`T-00002` and so on. The sequence never resets by year or Principal and is
never reused. A formal instruction later allocates a normal Case/PO and links
the existing Triage. Triage itself allocates neither.

**How a Triage starts.** One of three ways:

- the accepted route policy classifies a provider request as an assessment
  request;
- an authenticated Principal declares one over the Provider API;
- an authorised staff member manually classifies safely retained,
  attributable material as Triage.

A declared Triage carries the Principal's declaration as its match evidence,
stamped with that policy's key and version, and opens exactly like a
classified one. Manual classification records the source, the route evidence
available, the actor, time, reason and policy version. It never invents a
Principal and never creates a Case/PO. Material whose route or category is
not accepted stays `Unidentified`; it never becomes a Triage or a Case by
fallback.

**Automatic creation from intake** follows the same rule. When the route
classification records a received message as a Triage request, Pegasus does
not treat it as an instruction and allocates no Case/PO. The classification
decision itself is the match evidence, with its policy key and version on the
record. A known registration opens the Triage as `Open`. No known
registration registers the material as Unidentified with its reason and opens
no Triage until a registration is known. A message classified as Ambiguous
opens no Triage and goes to staff.

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
on any screen or through any caller.

**Files.** `Files` lists the retained request sources and their attachments,
plus the vehicle images linked to the record, each with view and download.
Triage has no separate file store and no upload action. Material reaches a
Triage only through the accepted intake routes.

**Editing.** An existing Triage is read-only until a staff member presses
Edit, which claims a scope for that one Triage record, not its queue or its
linked Case ([FRD-14](frd-14-record-edit-leases.md#record-edit-scopes);
takeover follows [FRD-14](frd-14-record-edit-leases.md#take-over)). Every
change, including assignment, findings, notes, response evidence, state and
Case association, rechecks the holder, token and Triage version in its own
transaction. Manual Case association also needs the separate current Case
edit lease; neither scope stands in for the other.

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

**Assignee and Case link.** A Triage may have an assignee. It has no due date
and no chase schedule. It may link to at most one current Case; a Case may
have many Triages. Every staff role in the
[staff role access matrix](frd-04-parties-accounts-and-access.md#staff-role-access-matrix)
may unlink or relink with a reason. The prior and current Case, actor, time,
reason and evidence stay in history permanently.

Choosing or changing the assigned Engineer is an ordinary edit available to
every enabled staff role, including self-assignment where the existing state
and edit-scope rules allow it. It needs no reason; history records the
assignment, actor and time. Findings,
cancellation, reopening and Case-association decisions keep their required
reasons.

**Cancel and reopen** both need a reason. Reopen always returns to `Open`
and never erases the earlier finding, reply, actor or chronology.

#### Automatic association with a formal Case

When the Principal is known and the Triage registration is accepted, and the
retained typed source identity agrees, Pegasus may find exactly one formal
Case through the same principal-scoped matcher intake uses. It withholds the
link when any of these apply: a contradictory registration, an unknown
Principal, a competing identity, a cancelled Triage, an incompatible target
identity, or a live staff Case edit lease. A Created-in-error replacement Case
must match on its own and keep the Triage's known Principal. A Completed
Triage may be linked without reopening.

The same link is attempted whichever record arrives first, and again on
creation or acceptance replay. The reconciliation timer retries eligible
unlinked records, oldest current matches first, up to its batch limit. A
non-match stays retryable when new evidence or a formal Case arrives.
Recoverable failures are visible and do not block unrelated links.

The automatic write rechecks the origin, evaluation, Principal, candidate
uniqueness, target identity and versions inside its transaction. It records
one SystemWorker link in both Triage and Case history. It keeps the Triage
reference, findings and state, and allocates no Case/PO. Recovery never
reverses a deliberate staff unlink or reassignment. Manual linking keeps its
staff authority, reason and current Case edit lease.

## States and transitions

| From | To | Trigger |
| --- | --- | --- |
| (none) | `Open` | Classification, Principal declaration or staff classification with a known registration |
| `Open` | `Awaiting information` | Staff mark it waiting |
| `Open`, `Awaiting information` | `Finding recorded` | A finding with at least one dimension is recorded |
| `Finding recorded` | `Completed` | The authorised actor records the outcome |
| any open state | `Cancelled` | Cancel with a reason |
| `Completed`, `Cancelled` | `Open` | Reopen with a reason |

## Edge cases and fail-closed behaviour

- No registration: no Triage opens; the material waits in Unidentified.
- Ambiguous classification: no Triage opens; staff decide.
- A finding with neither dimension is refused.
- Automatic linking stops when more than one Case matches, or a Case edit
  lease is live; it retries later.
- A stale scope, token or version changes nothing.

## Acceptance evidence

Core tests cover reference allocation, the state transitions above, findings
and corrections, and automatic association with its withholding rules.
Integration tests cover the Triage list and detail pages over real HTTP,
including the edit scope. Deployment and live acceptance are separate
evidence tiers ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `TRI-01`–`TRI-07`, `TRI-09` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-04](frd-04-parties-accounts-and-access.md),
  [FRD-09](frd-09-provider-and-intermediary-routes.md),
  [FRD-10](frd-10-mcp-automation-and-actor-boundary.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-15](frd-15-work-centre-queues-and-search.md).
