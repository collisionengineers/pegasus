# FRD-22: Pre-Case gates, matching and association

> Owner capabilities: INT-22, INT-24, INT-25, INT-29, INT-30 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A Case/PO is allocated only when the Principal, the Case type and every
  identity-critical fact are certain. Manual uploads also need a staff
  member to accept the proposal.
- Missing ordinary detail is not a gate. The new Case starts in `Not ready`.
- Matching never guesses. A weak or conflicting signal sends material to
  Unidentified, or to a staff decision, never silently onto a Case.
- One instruction makes exactly one Case. A source occurrence has at most
  one current Case association, and staff can undo a mistaken one with a
  reason.
- Automatic mail association never writes the Case row, so it never breaks
  a staff edit. Other automatic association yields to a live edit lease.
- Staff may link material to any Case or Triage Case, in any state.
  Automatic association keeps its own, narrower rules.

## Purpose

This document owns the gates that must pass before Pegasus creates a Case or
allocates a reference, and the rules for matching material to a Case and
linking or unlinking it. It serves the PRD outcomes for one Case per
instruction and no invented identity. Receiving material, receipt identity
and Unidentified are owned by
[FRD-02](frd-02-intake-and-source-identity.md). Case fields, provenance and
the global checks are owned by
[FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md). The
principal email routes are owned by
[FRD-09](frd-09-provider-and-intermediary-routes.md), manual upload by
[FRD-18](frd-18-manual-upload.md), image pairing by
[FRD-19](frd-19-image-led-intake-and-pairing.md), and edit leases by
[FRD-14](frd-14-record-edit-leases.md).

## Behaviour

### Mandatory pre-case gates

Before creating a Case or allocating a reference, Pegasus must have:

- a persisted source and the required extraction and classification
  receipts;
- an authenticated Principal identity, and the staff actor where the route
  needs staff;
- the provider or intermediary route identity and an enabled policy, where
  relevant;
- an unambiguous Case type and Principal association;
- processing, size, and format limits satisfied;
- no unresolved wrong-Principal, duplicate-occurrence, receipt-integrity, or
  source-custody ambiguity.

Once those facts are established, an automatic route creates the Case/PO and
allocates its permanent reference. Manual upload instead waits for staff to
accept the editable proposal
([FRD-18](frd-18-manual-upload.md#upload-confirmation-surface));
extraction alone never allocates or reserves a Case/PO. Missing ordinary
detail, images, or external checks keep the Case at `Not ready`; they are not
another pre-Case gate.

A Triage Case is allocated only once the Principal is established, before
the request is classified as a Triage, and the registration is known. A
Triage request missing either becomes Unidentified and allocates nothing
([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)).

For a standalone Audit, a missing original report is a Case requirement, not
a pre-Case gate. Once the Principal and identity-critical gates pass, the
instruction creates the `a.` Case/PO. The Case shows **Original report
missing** only while it has neither a filed original report nor
standalone-Audit evidence retained at intake. Staff clear it by marking a
filed document as the original report. A readable report records the
assessment at intake. A manual proposal may create an Audit from a receipt
already classified Audit, with or without standalone-Audit evidence; where
evidence exists, acceptance checks that it belongs to that receipt.

If a route cannot establish an identity-critical fact, it keeps only what is
safe and the material becomes Unidentified. Material that could not be read
(unsupported, failed OCR, or a technical failure on the file) becomes an
Unidentified item with the reason Could not be read and its file kind, so it
ages and falls due like any other item. Readable material that must not
become a Case is closed on its Unidentified item with a free-text reason and
can be reopened. A retryable technical failure stays retryable work (see
[Received file history and technical actions](frd-02-intake-and-source-identity.md#received-file-history-and-technical-actions)).
None of these allocates a reusable identity for convenience.

### Matching conflicts and reversible association

Matching uses evidence it can explain. Message identifiers, provider and
domain policy, route identity, accepted reference tokens, registration,
party identity, and operator confirmation may all contribute. A weak,
ambiguous, or contradictory signal never silently attaches material to a
Case. Automatic routes send competing candidate Cases and unresolved
source-identity conflicts to Unidentified with the matching reason. Manual
upload offers staff its match candidates and every other Case and Triage
Case instead
([FRD-18](frd-18-manual-upload.md#upload-confirmation-surface)).
Unsafe material or an unresolved source-integrity problem still fails closed;
choosing a destination cannot override that.

The fifteen evidenced principal email routes share one route, classification,
and match policy chain and use the existing instruction profiles. The exact
sender identity and the selected current document profile must agree;
document identity alone never allocates a Case. A proved forwarded original
is current material, not discarded quoted history. Unrelated reports and old
thread content can neither supply nor veto a current instruction's profile.
The accepted identities, work-type predicates, preserved QDOS body and Triage
rules, and the shared fail-closed procedure are owned by
[FRD-09](frd-09-provider-and-intermediary-routes.md#accepted-principal-email-routes-and-automatic-association).

Acceptance joins a profile's typed instruction values to the canonical Case
field identity, not to another principal's printed labels. The original
review-field names, candidates, and source locators are kept. Missing,
conflicting, or non-unique source attribution still prevents allocation. A
different label never justifies invented evidence or staff confirmation.

A registration match is a suggestion until accepted evidence or an
authorised operator confirms it. Matching reads an Inspection + Audit Case's
Inspection values, also once its Audit exists
([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)). Deduplication is occurrence-aware: exact
bytes and transport identifiers support correlation, while each visible
placement and chronology entry stays auditable.

Arrival-time proximity never associates or consolidates material. A mismatch
between accepted incident dates may rule a candidate out. A matching incident
date proves nothing on its own and needs corroborating accepted evidence.

The immutable source occurrence and its evidence stay separate from the
editable Case projection. Linking creates a versioned source-to-Case
relationship. It never turns the source into the Case, rewrites source facts,
or changes the original intake origin.

Image-led material has its own pairing rules
([FRD-19](frd-19-image-led-intake-and-pairing.md#pairing-and-merge)).
Triage association follows
[FRD-03](frd-03-triage.md#automatic-association-with-a-formal-case):
creation, formal acceptance, and replay each attempt the same
principal-scoped typed-identity match, and scheduled reconciliation retries
unfinished links. The final transaction rechecks current evidence, complete
candidate uniqueness, versions, and staff edit authority. A deliberate manual
unlink or reassignment stays authoritative. Triage keeps its own identity and
workflow and creates no further Case/PO.

**One Case per instruction.** Definitive authorised intake creates exactly
one instructed Case, and a replay creates no second one. A definitive match
to an existing instructed Case allocates no duplicate. A new instructed Case
starts in `Not ready` until its ordinary detail, required images, and
progression requirements are met; the route may move it to `Review` only
where its policy explicitly allows that. Allocation adds no universal manual
acceptance gate.

**One current association.** A source occurrence has at most one current
Case association. Every automatic or manual association records the exact
source and Case identities, the evidence, the actor, the time, the policy
version, and a reason where one is required. Any authorised staff member may
unlink or reassociate a mistaken match with a reason. The prior relationship
and both origins stay in history, and dependent facts and counts recompute
without deleting anything.

**Association and edit leases.** Automatic mail association does not wait
for a staff editor. It writes only the receipt's own append-only association
and history records, never the Case row or its version, so an editor's
pending save still validates against the version they loaded
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). It yields to an
archived Case. The staff "Add to an existing case" decision is a Case
mutation and takes the edit lease like any other; on a Triage Case it takes
the Triage edit scope.

**Staff linking reaches any Case.** Staff linking of received or uploaded
material (Add to an existing case on Upload, Link to Case on an Unidentified
item, and the Inbox's link) offers every Case and every Triage Case,
whatever its state (operator, 24 September 2026). Only staff linking is
widened. The automatic rules are unchanged: automatic association never
links new material to an archived Case, and the matcher's withholding rules
still apply. Automatic Image-initiated
association checks the current Case version and yields to an active staff
lease; the later image merge also yields to a live lease and rechecks the
current associations in its own transaction. Filing the associated mail's
evidence is a separate Case mutation: it yields to a live editor and rechecks
the Case version before each custody attempt and confirmation. A later retry
reads the current version, so a finished staff edit never strands retained
files.

## States and transitions

- Until every pre-Case gate passes, no Case exists and no reference is
  allocated. Material that cannot pass becomes Unidentified
  ([FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference)).
- When the gates pass, an automatic route creates the Case/PO. Manual upload
  waits for staff to accept the proposal first.
- A new instructed Case starts in `Not ready`
  ([FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels)). A
  route may move it to `Review` only where its policy explicitly allows
  that. A new Triage Case starts `Open`
  ([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)).
- A source occurrence is unlinked or has one current Case association.
  Unlink and reassociate need a reason and keep the prior relationship in
  history.

## Edge cases and fail-closed behaviour

- Two candidate Cases for an automatic route: Unidentified with the
  Ambiguous ownership or destination reason.
- A route cannot establish an identity-critical fact: it keeps only what is
  safe and the material becomes Unidentified.
- An automatic association meets a live staff lease: it yields and retries
  later against the current version.
- Automatic mail association meets an archived Case: it yields.
- Unsafe material or an unresolved source-integrity problem fails closed;
  choosing a destination cannot override that.
- Missing, conflicting, or non-unique source attribution prevents
  allocation.
- A replay of a definitive instruction creates no second Case.

## Acceptance evidence

Acceptance proves, through the real Worker and Web callers: one Case per
definitive instruction; that no edit lease is bypassed by automatic
association; that a Triage request without an established Principal becomes
Unidentified; and that staff link material to a Case and to a Triage Case in
any state. Deployment and live evidence are separate tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `INT-22`, `INT-24`, `INT-25`, `INT-29`, `INT-30` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-03](frd-03-triage.md),
  [FRD-09](frd-09-provider-and-intermediary-routes.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-18](frd-18-manual-upload.md),
  [FRD-19](frd-19-image-led-intake-and-pairing.md),
  [FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md).
- Technical constraints:
  [ADR-0056](../adr/0056-one-case-per-work-data-and-triage-case-type.md)
  (staff linking to any Case; Triage as a Case type).
