# FRD-03: Triage

## Boundary with Unidentified

Triage is a separate pre-Case work record with a distinct referencing system and workflow. An **open**
Triage follows the Triage states and does not receive a U reference merely
because it is awaiting
information. A classified Triage request with no registration yet has no Triage to
follow: it is held in Unidentified with its canonical reason until a registration is
known, and opening the Triage resolves that item. Holding it there is what stops it
being stranded in neither queue; it does not make it ordinary unidentified material,
and it never turns the request into an instruction. Material that is not accepted
as Triage, or a terminal unreadable/ambiguous source outside that workflow, enters
Unidentified with its canonical reason.
> Owner capabilities: TRI · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Triage

### Normal workflow and completion evidence

Each Triage has a global increasing `T-00001`, `T-00002` reference, with no
yearly or Principal reset and no reuse. Formal instructions allocate a normal
Case/PO and link the existing Triage; Triage itself allocates neither.

Triage begins when the exact accepted route policy classifies a provider
request as an assessment request, when an authenticated Principal declares one
over the Provider API, or when an authorised staff member manually classifies
safely retained, attributable material as Triage. Classification is how a
Triage usually arrives, not the only way it may: a declared Triage carries the
Principal's own declaration as its accepted match evidence, stamped with that
policy's key and version, and opens exactly as a classified one does (operator
decision, 2026-08-28). It allocates no Case/PO. Manual classification records
the source, available route evidence, actor, time, reason, and policy version;
it neither invents Principal identity nor creates a formal Case/PO. Material
whose route or category remains unaccepted stays `Unidentified` and never
becomes Triage or a Case by fallback. A Triage request is a separate pre-Case record with its own
reference and workflow: without a VRM it remains `Unidentified`; with a VRM it
opens as `Open`, may move to `Awaiting information`, records an accepted
finding as `Finding recorded`, and reaches `Completed` when the authorized actor
decides and records the Triage outcome. Sending a message is not required. An acknowledgement, request for information,
Draft, queue action, or other correspondence may be retained but is not itself
a finding or completion evidence.

Automatic creation from intake follows exactly that rule and adds nothing to it.
When the accepted route classification records a received message as a Triage request,
processing does not treat it as a formal instruction: no normal Case/PO is
allocated from it, and the accepted route classification decision is itself the accepted
Triage-match evidence — the same route policy the paragraph above names, with its policy
key and version stamped on the record. A known vehicle registration opens the Triage as
`Open`; no known registration registers the material as Unidentified with its canonical
reason and opens no Triage, until a registration is known. A message whose classification
is the recorded Ambiguous outcome is neither: it opens no Triage and reaches staff.

`History` merges the record's durable events and its staff notes in one
chronological order (D25, 2026-09-01). Notes are append-only and attributable:
each carries its author, time and text. A correction is a new note; note
editing and note deletion do not exist, on any surface or through any caller.

`Files` lists the retained request sources and their attachments together with
the vehicle images linked to the record, each with view and download. Triage
adds no arbitrary file store and no upload action: material reaches a Triage
record through the accepted intake routes only.

Triage records have the states `Open`, `Awaiting information`, `Finding recorded`, `Completed`, and `Cancelled`.

An existing Triage is read-only until an authorised staff member selects Edit.
That claim is scoped to the single Triage record, not its queue or its linked
Case, and follows the same five-minute duration and one-minute heartbeat as a
Case edit lease. Every staff mutation — including assignment, determinations,
notes, response evidence, workflow state and case association — rechecks the
scope holder, token and current Triage version in its own transaction. Save
releases the Triage scope; Cancel commits no mutation. Manual case association
also retains its separate current Case edit lease; neither scope substitutes for
the other.

A recorded finding has two independently optional dimensions:

- Roadworthiness: `Roadworthy` or `Unroadworthy`;
- Assessment: `Repairable` or `Total loss`.

At least one dimension is required. A correction records a reasoned superseding
finding and preserves the prior decision and correspondence. Deciding the
corrected outcome can complete the Triage without sending another message.

Completion records the outcome, actor and time. **Reply with outcome** is optional:
it opens the email feature with a preset outcome template that the user can edit.
A sent reply is linked to the Triage and its correspondence through the normal
email evidence contract, but neither composing nor sending is a completion gate.
`Cancelled` closes a Triage without a finding; neither outcome turns a Triage
finding into a definitive instruction for a later Case.

Triage may have an optional assignee but no due date or chase schedule. It may link to at most one current case; a case may have many Triages. The [staff role access matrix](frd-04-parties-accounts-and-access.md#staff-role-access-matrix) permits every staff role to reasonedly unlink or relink; the exact prior/current Case identities, actor, time, reason, and evidence remain in permanent history.

Selecting or changing the assigned Engineer is an ordinary Triage edit and
does not require a reason. The stored history records the selected assignment,
actor and time. Meaningful Triage determinations, cancellation, reopening and
case-association decisions retain their required reasons.

Cancellation and reopen require reasons. Reopen always returns to `Open` and never erases the prior finding, reply, actor, or chronology.

### Automatic association with a formal Case

Known Principal and accepted Triage registration, corroborated by the retained
typed source identity, may identify exactly one formal Case through the same
principal-scoped matcher used by intake. A contradictory registration,
unknown Principal, competing identity, cancelled Triage, incompatible target identity, or live staff Case edit lease withholds automatic linkage. A
Created-in-error replacement must independently match and retain the Triage's
known Principal. Completed Triages may link without reopening.

Both arrival orders and creation/acceptance replay attempt the same link.
The existing reconciliation timer retries eligible unlinked records, choosing
oldest current matches before applying its batch limit. Nonmatches remain
retryable when new evidence or a formal Case arrives. Recoverable failures are
visible and do not prevent unrelated links.

The automatic write rechecks origin/evaluation, Principal, full candidate
uniqueness, target identity and versions inside its transaction. It records
one SystemWorker-attributed link in Triage and Case history, preserving the
Triage reference, findings and state and allocating no new Case/PO. Deliberate
staff unlink or reassignment is never reversed by recovery. Manual linking
retains its existing staff authority, reason and current Case edit lease.
