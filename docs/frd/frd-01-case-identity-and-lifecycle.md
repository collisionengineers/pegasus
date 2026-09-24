# FRD-01: Case identity and types

> Owner capabilities: ACC-06, CASE-02 to CASE-12 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A Case gets one reference once Pegasus knows the Principal and the Case
  type for certain. That reference never changes and is never reused.
- References look like `QDOS26001`. An Audit Case's reference starts with
  `a.` and a Triage Case's with `t.`.
- There are four Case types: Inspection, Audit, Inspection + Audit, and
  Triage. They share one sequence per Principal per year.
- An Inspection + Audit Case keeps its Audit on the same Case. Create audit
  adds it once the Inspection report is sent; its report reference is
  `a.{Case/PO}` and uses no new number.
- A Case is never deleted. A Case opened under the wrong Principal is marked
  Created in error and linked to its replacement.
- Each Case keeps its own copy of the parties and addresses agreed for it.
  Later directory edits never rewrite that copy.

## Purpose

This document says what makes a Case a Case: who it is for, what its
reference is, what type it is, and which facts belong to it. How a Case moves
through work is in [FRD-13](frd-13-case-lifecycle-and-workflow.md). Who may
edit it, and how editing is protected, is in
[FRD-14](frd-14-record-edit-leases.md).

## Behaviour

### Principal, reference, organisation, and case-party identity

**When a Case gets its reference.** Pegasus allocates a reference only when
safe source processing has settled one Principal and one Case type and every
identity-critical gate has passed. Material that is missing, conflicting or
ambiguous on identity goes to Unidentified instead
([FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference));
it never reserves a reference. A manual upload also needs a staff member to
accept it. Until then it is a proposal with no reference
([FRD-02](frd-02-intake-and-source-identity.md#ways-intake-starts)).

Missing business detail, missing images or an outstanding external check do
not stop allocation. They create or keep the Case as `Not ready`
([FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review)).

**Reference format.** The base reference is
`{principal code}{YY}{sequence}`. The sequence is at least three digits:
`001` to `999`, then `1000` onward with no upper limit. Inspection, Audit,
Inspection + Audit and Triage Cases share one sequence per Principal per
year, and each Case consumes one number. A sequence value is never reused and
never wraps.

**The four Case types and their references.** The Case/PO is the base
reference with the type's prefix: none for Inspection and Inspection + Audit,
`a.` for a standalone Audit, and `t.` for Triage.

| Case type | Case/PO | Example |
| --- | --- | --- |
| Inspection | The base reference | `QDOS26001` |
| Audit (standalone) | `a.` plus its own new number | `a.QDOS26002` |
| Inspection + Audit | The base reference. Its Audit report, once Create audit has run, carries the Audit reference `a.` plus the same Case/PO | `QDOS26001`, Audit report `a.QDOS26001` |
| Triage | `t.` plus its own new number | `t.QDOS26003` |

A standalone Audit's `a.` value is that Case's own Case/PO. The Audit
reference of an Inspection + Audit Case belongs to the same Case: it
consumes no number and is not a second Case/PO. The assessment outcome never
changes a reference.

**Standalone Audit.** Another firm has already inspected the vehicle. Their
Engineer's original report should arrive with the instruction. Pegasus creates
the `a.` Case once the Principal and identity gates pass, with or without that
report.

- On the email route, a readable original report records the assessment at
  intake.
- On the Provider API route, the authenticated Principal declares the verdict
  and attaches the original report. The declaration records the assessment.
- On manual upload, a staff member must accept the proposal first.

If no original report is filed on the Case and no standalone-Audit evidence
was kept at intake, the Case shows **Original report missing** as an
outstanding requirement. It clears when staff mark a filed document as the
original report. The assessment is a fact on the Case, not part of its
identity.

**Inspection + Audit.** The Case starts as a normal Inspection Case. Once
its Inspection report is sent, **Create audit** (in the Actions menu, inside
an edit session) adds the Audit to the same Case
([FRD-13](frd-13-case-lifecycle-and-workflow.md#create-audit)). The Audit:

- is part of the same Case: one Cases record, one Case/PO, one state, one
  Files and one Notes, with the same Principal and Engineers;
- starts as a copy of the Inspection's values: Case data and fields
  (confirmed values stay confirmed), assessment fields including the fee,
  damage and decisions, every live estimate with its lines, guide valuations
  and the applied Engineer's Value, and report wording. Open AI proposals,
  discarded estimates and estimate revision snapshots are not copied; files
  are shared, not copied;
- is edited on its own. Audit edits change only the Audit's values, and the
  Inspection's values and its sent report become read-only;
- has its own report under the Audit reference `a.{Case/PO}`, printed as Our
  Ref and used for the report's file name and email subject, with its own
  fee note and fee;
- files its report in an `a.{Case/PO}` subfolder of the Case's Box folder
  ([FRD-05](frd-05-documents-extraction-and-custody.md#custody-and-derived-reads)).

A Case has at most one Audit; a second Create audit is refused. The Case/PO
and the Audit reference never change and are never reused. Case lists,
queues and intake matching read the Inspection's values. Once the Audit
exists, Search lists the Case under both references
([FRD-15](frd-15-work-centre-queues-and-search.md#search)).

**Triage.** A Triage Case is created only once its Principal is established
and its registration is known. It consumes the next number from the shared
sequence and its Case/PO is `t.` plus that base reference. Its workflow is
owned by [FRD-03](frd-03-triage.md). A later definitive instruction is a
separate Case with its own number.

**Replacing a principal code.** When a Principal's code changes, Pegasus
replaces it in one Core transaction: it deactivates the old code, continues
the old code's next unused sequence number for the rest of the cutover year
(Europe/London), and starts later years at `001`. Both codes and the reason
stay on record permanently.

**Wrong Principal.** A Case created under the wrong Principal is marked
`Created in error` with a reason and a link to its replacement Case. Neither
Case's identity changes and neither reference is reused. The replacement
records the correct Principal as a Confirmed `work_provider_code` with source
kind staff correction. The original Case's own fields are left as they were.
After Create audit, the replacement starts from the Inspection's values only
and has no Audit. Created in error is a recorded disposition, not a
permanent closure. A Triage Case's Principal is fixed with its `t.` Case/PO:
Triage has no Set principal, and Correct principal is not offered on a
Triage Case.

**Never deleted.** A Case is never deleted and never permanently closed.
Returning a Case to engineering work records a reason and uses the normal
gates in [FRD-13](frd-13-case-lifecycle-and-workflow.md#actions).

**Who is who.** The Principal is the party that instructs and pays. An
Intermediary provides a route to Pegasus; that does not make it the Principal.
The Repairer is the organisation holding or repairing the vehicle. The Image
Source is whoever actually supplied the images. One organisation can hold
several of these roles on one Case. A sender whose identity is ambiguous never
becomes the Principal.

**Snapshots.** Every Case keeps its own copy of the inspection address, the
organisations and the party roles accepted for that Case. Correcting the
reusable directory later never rewrites a Case's copy
([FRD-04](frd-04-parties-accounts-and-access.md#case-party-provenance)).

**Sources keep their identity.** Source messages, files, visible placements,
attachments, images and later correspondence keep stable identities and
provenance. Matching hashes may show that two files have the same bytes; they
never replace the identity of where a file appeared.

**No invented history.** Pegasus does not rebuild old correspondence into
synthetic historical Cases. New correspondence about old work is handled
under the current process, with its provenance recorded.

### Case types

- **Inspection.** Collision Engineers prepares the accepted work for its own
  Engineer's desktop assessment and returns that Engineer's report to the
  Principal.
- **Audit.** Another engineering firm has already inspected the vehicle.
  Collision Engineers receives that firm's original Engineer report with the
  instruction and audits or double-checks the work.
- **Inspection + Audit.** Collision Engineers completes and sends an
  Inspection report on the Case, then audits that report on the same Case
  after Create audit. The Audit has its own values, report, reference and
  fee; the Case's identity, state, files and notes stay shared.
- **Triage.** A Principal asks Collision Engineers to assess a vehicle
  without a definitive instruction. The Triage Case records the finding and
  its outcome under its own workflow
  ([FRD-03](frd-03-triage.md)). It is never converted into an instructed
  Case.

Diminution and Commercial Case types are deferred. They are not aliases of the
four active types and there is no generic Case type.

**What a Case owns.** Its identity, Principal, reference and type. Its
accepted source links. Its snapshotted parties and addresses. Its vehicle
identity. Its work state and due work. Its documents, correspondence,
findings, decisions, action history and closure history. Its assigned
Engineer and Sign-off Engineer. Its one Case Notes history. Its storage
location and inspect-at choice.

**The Inspection and the Audit.** The working values of a Case (Case data,
assessment, damage, estimates, valuations, decisions and report wording)
belong to its Inspection, or on a standalone Audit to that Audit. Create
audit gives an Inspection + Audit Case a second set for its Audit. Every
edit changes the Case's current set: the Audit once it exists, otherwise the
Inspection. Everything else in the list above is held once for the Case.
The Inspection deadline and completeness stay with the Inspection's values.

**What a Triage Case owns.** Its identity, Principal, `t.` Case/PO and type,
its Box case folder and files, and its Triage record: state, registration,
assignee, findings, response evidence, notes, history, origin and linked
instruction Case ([FRD-03](frd-03-triage.md)). It has no Case workflow state,
Case data or Case Notes.

## States and transitions

Identity has no states. Principal, reference and type are fixed when the
reference is allocated. Work states and their transitions are in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels).

## Edge cases and fail-closed behaviour

- Ambiguous Principal or Case type: the material goes to Unidentified and no
  reference is reserved.
- A manual upload proposal reserves nothing until staff accept it.
- A second Create audit on the same Case is refused.
- A Triage request without an established Principal or a registration
  reserves no reference.
- A retired principal code never returns to use.
- A wrong-Principal Case keeps its reference. Only the replacement gets a new
  one.

## Acceptance evidence

Core tests cover reference allocation and format for every prefix, the `a.`
Audit reference, principal-code succession, and Created in error with its
linked replacement. Integration tests cover concurrent allocation across
Case types with no gap or deadlock, Create audit's copy, and that Audit edits
leave the Inspection's values unchanged. Deployment and live acceptance are
separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `ACC-06`, `CASE-02`–`CASE-12` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md) (intake and
  Unidentified), [FRD-04](frd-04-parties-accounts-and-access.md) (parties and
  accounts), [FRD-05](frd-05-documents-extraction-and-custody.md) (custody),
  [FRD-03](frd-03-triage.md) (Triage),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md)
  (reports), [FRD-13](frd-13-case-lifecycle-and-workflow.md) (lifecycle),
  [FRD-14](frd-14-record-edit-leases.md) (edit leases).
- Technical constraints:
  [ADR-0002](../adr/0002-dotnet-modular-monolith-on-azure.md#case-reference-allocation)
  (Case reference allocation),
  [ADR-0056](../adr/0056-one-case-per-work-data-and-triage-case-type.md)
  (one Case with per-work data; Triage as a Case type).
