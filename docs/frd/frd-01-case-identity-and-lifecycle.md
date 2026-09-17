# FRD-01: Case identity and types

> Owner capabilities: CASE-01, CASE-02, CASE-03, CASE-04, CASE-07, CASE-08, CASE-09, CASE-10, CASE-11, CASE-12 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A Case gets one reference once Pegasus knows the Principal and the Case
  type for certain. That reference never changes and is never reused.
- References look like `QDOS26001`. An Audit Case's reference starts with
  `a.`.
- There are three Case types: Inspection, Audit, and Inspection + Audit.
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

**Reference format.** The normal Case/PO is `{principal code}{YY}{sequence}`.
The sequence is at least three digits: `001` to `999`, then `1000` onward
with no upper limit. Inspection, Audit and Inspection + Audit Cases share one
sequence per Principal per year. A sequence value is never reused and never
wraps.

**The three Case types and their references.**

| Case type | Reference | Example |
| --- | --- | --- |
| Inspection | The normal Case/PO | `QDOS26001` |
| Audit (standalone) | `a.` plus its own new number from the shared sequence | `a.QDOS26002` |
| Inspection + Audit | The Inspection Case keeps its Case/PO. Create audit adds one linked Audit Case whose reference is `a.` plus that same number | `QDOS26001` and `a.QDOS26001` |

The `a.` value is the Audit Case's own Case/PO. It is not a second label on
another Case. The assessment outcome never changes the reference.

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

**Inspection + Audit.** The Case starts as a normal Inspection Case. Once a
report has been generated on it, **Create audit** (in the Actions menu, inside
an edit session) creates one linked Audit Case. That Audit Case:

- keeps the same Principal and sequence number, has Case type Audit, and has
  its own `a.{Case/PO}` reference, whatever the assessment outcome;
- inherits the original's Engineer and starts in Review;
- carries the Case data, assessment and estimate forward;
- shares the original's files by reference to the same stored bytes, and gets
  its Box subfolder under the original Case's folder;
- links to the original, and the original links back.

The Audit Case is its own record. A second Create audit on the same Case is
refused. Neither reference changes or is reused.

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
Created in error is a recorded disposition, not a permanent closure.

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
- **Inspection + Audit.** Collision Engineers completes an Inspection report
  on the Inspection Case, then audits that report on a linked Audit Case
  created by Create audit. The Audit Case has its own reference, identity,
  evidence, report and acceptance boundary.

Diminution and Commercial Case types are deferred. They are not aliases of the
three active types and there is no generic Case type.

**What a Case owns.** Its identity, Principal, reference and type. Its
accepted source links. Its snapshotted parties and addresses. Its vehicle
identity. Its work state and due work. Its documents, correspondence,
findings, decisions, action history and closure history. Its assigned
Engineer and Sign-off Engineer. Its one Case Notes history. Its storage
location and inspect-at choice.

## States and transitions

Identity has no states. Principal, reference and type are fixed when the
reference is allocated. Work states and their transitions are in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels).

## Edge cases and fail-closed behaviour

- Ambiguous Principal or Case type: the material goes to Unidentified and no
  reference is reserved.
- A manual upload proposal reserves nothing until staff accept it.
- A second Create audit on the same Inspection Case is refused.
- A retired principal code never returns to use.
- A wrong-Principal Case keeps its reference. Only the replacement gets a new
  one.

## Acceptance evidence

Core tests cover reference allocation and format, the `a.` Audit identity,
principal-code succession, and Created in error with its linked replacement.
Integration tests cover Create audit on a Case with a generated report.
Deployment and live acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `CASE-01`–`CASE-04`, `CASE-07`–`CASE-12` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md) (intake and
  Unidentified), [FRD-04](frd-04-parties-accounts-and-access.md) (parties and
  accounts), [FRD-05](frd-05-documents-extraction-and-custody.md) (custody),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md)
  (reports), [FRD-13](frd-13-case-lifecycle-and-workflow.md) (lifecycle),
  [FRD-14](frd-14-record-edit-leases.md) (edit leases).
- Technical constraints:
  [ADR-0051](../adr/0051-linked-audit-case-identity-and-custody.md) (linked
  Audit Case identity and custody).
