# FRD-02: Intake, receipt identity and pre-Case gates

> Owner capabilities: INT · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Receiving material is not creating a Case. Pegasus keeps the original
  bytes and their identity first, then works out what the material is.
- A Case/PO is allocated only when the Principal, the Case type and every
  identity-critical fact are certain. Manual uploads also need a staff
  member to accept the proposal.
- Anything safely kept but not understood becomes one Unidentified item
  with a permanent `U<n>` reference and one of seven reasons. This document
  owns what Unidentified means.
- Matching never guesses. A weak or conflicting signal sends material to
  Unidentified, or to a staff decision, never silently onto a Case.
- Manual upload and public upload links are in
  [FRD-18](frd-18-manual-upload-and-upload-links.md). Image-only
  material is in [FRD-19](frd-19-image-led-intake-and-pairing.md).

## Purpose

This document says how Pegasus receives material, proves it was received,
decides what it is, and either creates a Case, links it to an existing
record, or holds it as Unidentified. It serves the PRD outcomes for durable
intake, one Case per instruction, and no invented identity.

## Behaviour

### Intake and source identity

Every intake path must:

- keep the original source bytes and the message or file identity before
  deriving any text or classification;
- keep sender, recipients, subject, message identifiers, timestamps,
  attachment names, content types, byte lengths, hashes, and parent or
  placement relationships where they exist;
- treat the same source occurrence the same way every time, without
  collapsing distinct visible placements into one;
- record unsupported, incomplete, corrupt, encrypted, oversized, ambiguous,
  or technically failed input as an explicit decision, never a silent drop
  or a silent accept;
- record the actor, time, caller, source, policy version, and structured
  before and after values for every transition, plus a reason where that
  transition needs one;
- never let untrusted content become an instruction, a policy, an identity,
  or an authority.

### Ways intake starts

Intake can start from staff-forwarded email, a staff-created upload link,
provider material, manually supplied files, images, correspondence, or a
future approved API route. Receiving something does not create a Case.

**Direct Case creation** is a staff path. It uses the same permanent Case/PO
allocator as intake acceptance. Staff must supply the identity-critical Case
facts. The action is recorded. No intake receipt and no invented source
provenance are created. Ordinary detail may still be missing, so the new
Case starts in `Not ready` until its normal requirements are met.

A staff-created Case, and any other staff acceptance with no accepted mail
route or Provider API credential to name the Principal, records
`work_provider_code` as a Confirmed value from the accepted Principal with
source kind case acceptance. Match indexing and EVA export then name the
Principal instead of an empty value.

**Image-only material** with a usable registration becomes an
Image-initiated Case, not an Unidentified item. That lifecycle is owned by
[FRD-19](frd-19-image-led-intake-and-pairing.md#image-initiated-case-projection).
Image material with no usable registration enters Unidentified with a
reason.

### Unidentified destination and reference

Unidentified is the one place for material that Pegasus has safely kept but
cannot place. It is the pre-Case outcome for every route: email, provider
and intermediary routes, manual upload, image groups, and Triage. There is
no separate "blocked" outcome anywhere.

**What becomes Unidentified.** Retained material whose identity, meaning,
owner, or destination cannot be established becomes one `UnidentifiedItem`.
One item covers one source occurrence or one inseparable submission group.
Group membership is permanent: the group gets one `U<n>` reference, and each
member keeps its own filename, receipt identity, custody, and chronology.

**The reference.** `U` followed by positive, unpadded decimal digits,
allocated atomically from its own sequence and never reused. A U-reference
is never accepted where a Case/PO, Audit reference, Image reference, or
Principal identifier is required. Work that can still be retried does not
get a reference.

**The seven reasons.** Every item stores exactly one Core-owned reason and
bounded safe detail:

| Reason | Meaning |
| --- | --- |
| Unreadable or corrupt | The bytes cannot be opened. |
| Unsupported | The file kind is not accepted. |
| No usable identification | Nothing in the material identifies a Case, vehicle, or owner. |
| Conflicting identification | The material names two different identities, for example two valid registrations in one image group. |
| Ambiguous ownership or destination | More than one Case or owner could claim it. |
| Terminal technical processing failure | Processing failed after custody and cannot be retried (`TechnicalProcessingFailure`). |
| Could not be read (with the file kind) | Unsupported, failed OCR, or a technical failure on the file itself. |

**Per route.** Unidentified mail keeps its classification record and links
to the same item rather than a second queue row. The Inbox Unidentified
scope lists retained mail whose item is still open; the message leaves that
scope when the item resolves. A provider or intermediary route reaches
Unidentified only when the material is kept but no unique owner or
destination can be shown; a reasoned policy refusal is an Unidentified item
closed with that reason. Triage is separate: an open Triage record follows
the Triage states and never gets a U-reference just because it is waiting
for information. A classified Triage request that has no registration yet
is held in Unidentified until a registration is known, and opening the
Triage resolves that item. Grouped vehicle images are judged as one group:
one usable registration follows the Case or Image-initiated route, no
usable registration is one Unidentified item with every file, and two
different valid registrations are the Conflicting identification reason.
Automation may list and look up items by exact U-reference and uses the
same Core resolution command as staff
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md)).

**Open, closed, resolved.** An item is open or resolved. Staff may **Close
with reason** (free text) any readable item that must not become a Case.
The closed item is resolved, listed under Closed items with its reason, and
**Reopen** (with a reason) returns it to open with a "Resolved to Open"
history row. Resolving an item to a destination needs an operation key, the
expected version, a reason, and one supported destination. Each resolution
appends permanent history with actor, time, target, and before and after
state. A replay returns the original result. Reusing an operation key for a
different change fails closed.

**Automatic resolution.** If an open item's origin receipt later reaches a
real destination, a formal Case or a registered Image intake, Pegasus
resolves the item to that destination itself, in the receipt's own
processing pass or by a sweep for receipts promoted outside their pass. The
destination is written to the item's history. A receipt that is still
genuinely unidentified is never force-closed.

**What the operator sees.** When no category can be determined, the record
shows the U-reference, the reason, the bounded safe detail, the source or
group, the custody state, and the next permitted action. It never shows the
positive rationale for some other category.

### Source occurrence and dispatch identity

A source occurrence is the channel-scoped identity of one visible receipt or
placement. It is not the content hash, the extracted evidence, the processing
dispatch, or any accepted Case projection.

- Replaying the same occurrence with the same bytes returns the existing
  receipt.
- Reusing an occurrence identity for different bytes is a visible identity
  conflict. It creates no receipt, association, Case, or reference.
- Equal bytes received under different permitted occurrence identities stay
  separate evidence with separate provenance.

Pegasus acknowledges receipt only after the original bytes, the source
receipt, and one durable processing-dispatch record have all committed. Each
dispatch has its own stable idempotency identity tied to the occurrence. A
queue carries only the stable work identifier, never the payload. The
acknowledgement means "durably received for processing". It does not mean
classified, associated, accepted as a Case, completed, or closed.

The Web receipt path stages work as pending and never processes it. The
Worker is the sole processing owner: it dispatches pending work, claims
queue deliveries once, recovers expired leases, and records a completed or
failed outcome. A duplicate delivery must not duplicate an evaluation, Case,
reference, or downstream side effect. Staff can see Received, Processing,
Complete, or Failed by the staged receipt identifier. Failure wording is
bounded and never shows exception or infrastructure detail.

An evaluation is recorded before its destination is written, but the work is
not complete until every required association, allocation, Triage, and
Unidentified write has finished. A transient destination failure keeps a
retryable work item with the same evaluation identity and never allocates a
second Case. A recorded unique Case match withholds new-Case allocation even
before its association is persisted. Staging is cleaned only after durable
completion.

A multi-image submission has one group-level destination and, when
unresolved, one Unidentified reference with the group's reason. The bounded
sweep recovers pending groups oldest first before applying its page limit.
Groups with an Unidentified outcome leave that recovery set, so newer
unrelated receipts cannot starve a pending image group.

For email and manual upload alike, the durable commit is followed at once by
a best-effort publication of the work identifier. Publication never precedes
the commit, and a failed publication never rolls it back. Pending work that
was not published, including work marked dispatched whose queue delivery
never became claimable, is eligible for idempotent recovery within one
minute. The recovery sweep is a safety net, not the scheduler. The same rule
covers external or custody work created by a completed intake pass: commit,
publish immediately, and reconcile a missed publication without repeating an
accepted downstream side effect.

The ordinary path records correlated timings for durable receipt,
publication, queue claim, source reading, identification, classification,
extraction, association or allocation, Case creation, custody hand-off, and
terminal state. Timings carry identifiers and bounded outcome data, never
source content. From durable receipt, ordinary supported principal email and
manual-upload work reaches its Case destination, or its truthful terminal
non-Case outcome, within five seconds at p95, excluding time spent waiting
for a manual staff decision. Case custody confirmation is measured as the
final best-effort segment, and any Box or provider delay is attributed
separately. A large, retrying, or legitimately incomplete item stays
Received or Processing; no older terminal outcome is shown over it.

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
([FRD-18](frd-18-manual-upload-and-upload-links.md#upload-confirmation-surface));
extraction alone never allocates or reserves a Case/PO. Missing ordinary
detail, images, or external checks keep the Case at `Not ready`; they are not
another pre-Case gate.

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
[Received file history and technical actions](#received-file-history-and-technical-actions)).
None of these allocates a reusable identity for convenience.

### Received file history and technical actions

There is no received-file page. A received file is shown where it matters:
on its message, on the upload that brought it, and on the record it became
(Case, Triage, Image-initiated Case, or Unidentified item). Each of those
offers **Open file** for the retained original and, for email, **Open
message**.

The receipt's history is in Administration › Logs › **Intake log**,
Administrators only. One row per received file shows source, item, outcome
(Case created, Linked to Case, Vehicle images, Triage, Unidentified, Could
not be read, Closed, Processing failed, Allocation failed, OCR failed), what
it became, and its attempt counts. A row drawer shows the retained original
and the processing evidence: decision, failure, registration readings,
suggested fields, decision evidence, and allocation attempts. The head-line
Failed intake count is the number of files whose outcome is a retryable
failure (Allocation failed, OCR failed, or Processing failed), the same set
Operations lists.

Three technical actions live in the Intake log drawer and on the matching
Operations Attention required row, Administrators only. Each needs a reason
and is offered only where it applies:

- **Retry allocation** when the last allocation attempt failed and can be
  retried.
- **Retry OCR** when the last OCR attempt failed. It re-queues that attempt
  for the Worker once, with a fresh attempt budget, and is not offered again
  once queued.
- **Re-evaluate** a processed file under the current policy, for any other
  processing failure.

Each records the actor, reason, time, and before and after state, and
replays by operation key.

A retained message's attachments each state their own outcome in operator
words: Case created, Linked to Case, Vehicle images, Triage, Unidentified,
Could not be read (with its reason and Unidentified item), or Processing
failed. The message's outcome is not repeated on every attachment.

Box custody follows Case/PO allocation and never precedes it; the custody
rules, including what happens when Box filing fails, are owned by
[FRD-05](frd-05-documents-extraction-and-custody.md#staging-and-custody).

### Matching conflicts and reversible association

Matching uses evidence it can explain. Message identifiers, provider and
domain policy, route identity, accepted reference tokens, registration,
party identity, and operator confirmation may all contribute. A weak,
ambiguous, or contradictory signal never silently attaches material to a
Case. Automatic routes send competing candidate Cases and unresolved
source-identity conflicts to Unidentified with the matching reason. Manual
upload offers the current viable destinations to staff instead
([FRD-18](frd-18-manual-upload-and-upload-links.md#upload-confirmation-surface)).
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
authorised operator confirms it. Deduplication is occurrence-aware: exact
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
mutation and takes the edit lease like any other. Automatic Image-initiated
association checks the current Case version and yields to an active staff
lease; the later image merge also yields to a live lease and rechecks the
current associations in its own transaction. Filing the associated mail's
evidence is a separate Case mutation: it yields to a live editor and rechecks
the Case version before each custody attempt and confirmation. A later retry
reads the current version, so a finished staff edit never strands retained
files.

### Field provenance and value kinds

Each Case datum keeps its current provenance: staff entry, extraction, AI
prefill or proposal, provider API, or another external vehicle or estimate
source, with its identity, version, and time. The UI shows provenance without
treating it as confirmation. A derived value names its inputs and calculation
rather than claiming a raw source.

Each datum also carries a value kind (Fact, Suggestion, or Confirmed) and a
source kind (intake evidence, mail route, case acceptance, staff correction,
vehicle lookup, provider setting, or Provider API). `work_provider_code`
names the Principal for match indexing and EVA export. It is Confirmed with
source kind case acceptance when staff acceptance names the Principal (see
[Ways intake starts](#ways-intake-starts)), and Confirmed with source kind
staff correction on a Wrong-Principal replacement Case
([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
A Confirmed value supersedes an earlier Fact or Suggestion for current use
without erasing it from history.

### Global vehicle and value checks

Every Case must pass three global checks unless a documented exception
applies: vehicle identity and specification, vehicle history and risk, and
market valuation. All three results, or their recorded exceptions, are
required before the Case can enter Review and appear in the Engineers queue.
An authorised staff reviewer may record an exception as a named, reasoned
Case action in permanent history. Provider and route policy choose the
provider, the required result, the acceptable provenance, and the
unavailable or failure behaviour for each check. This requirement names no
provider.

Vehicle details come from the instruction where present, otherwise from the
applicable DVLA or MOT source. Mileage evidence ranks as:

1. an accepted staff-entered value;
2. text extracted directly from the instruction, including a third-party
   engineer report supplied with a Principal's instruction;
3. Document Intelligence extraction from a scanned instruction, or future
   odometer-vision evidence;
4. a DVSA-derived estimate.

DVSA runs for every Case. Its estimate fills the Case mileage only when no
higher-tier value exists. A difference between the DVSA mileage and any
accepted staff-entered, instruction-extracted, Document Intelligence, or
odometer value is shown on the Case as a discrepancy. The odometer-vision
capability does not imply an activated AI caller before its own accepted
evaluation and integration contract. The estimate method is owned by
[FRD-06](frd-06-vehicle-and-engineering-evidence.md#conservative-mot-mileage-estimation).

### Instruction field meanings

A Work Instruction describes a claimant involved in a road traffic accident.
Capture:

| Field | Rule |
| --- | --- |
| Work Provider | Also called the Principal. |
| Claimant Name | From the instruction. |
| Claim Number | The Principal's external reference. |
| Vehicle Registration | The VRM. |
| Source Vehicle Description | Keep the instruction's combined claimant-vehicle description with its source locator, on the Case record and the Received screen. Do not split it into make and model by guesswork, and do not treat a third-party vehicle as the claimant's. The Case Vehicle section shows the looked-up or confirmed make and model instead. |
| Vehicle Make | From the instruction, or an authorised lookup when absent. |
| Vehicle Model | From the instruction, or an authorised lookup when absent. |
| Vehicle Mileage | From the instruction when supplied; MOT-based estimation when available. |
| Accident Circumstances | From the instruction. |
| Date of Incident | From the instruction. |
| Instruction Date | The document value; today's date if absent. |
| Inspection Address | FRD-06 inspection-location rules. |

## States and transitions

- A receipt is Received, Processing, Complete, or Failed. Failed splits into
  retryable (Allocation failed, OCR failed, Processing failed) and terminal.
- Its destination is one of: Case created, Linked to Case, Vehicle images
  (an Image-initiated Case, [FRD-19](frd-19-image-led-intake-and-pairing.md)),
  Triage, Unidentified, Could not be read, or Closed.
- An Unidentified item is open or resolved. Close with reason and Reopen
  move between them. Reaching a real destination resolves it.
- A new instructed Case starts in `Not ready`
  ([FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels)).

## Edge cases and fail-closed behaviour

- Same occurrence, different bytes: an identity conflict, nothing created.
- Two candidate Cases for an automatic route: Unidentified with the
  Ambiguous ownership or destination reason.
- Destination write fails after evaluation: the work stays retryable with
  the same evaluation identity; no second Case is allocated.
- Publication of the work identifier fails: the commit stands and recovery
  picks the work up within one minute.
- A file that cannot be read: Unidentified, reason Could not be read, with
  the file kind, so it still ages and falls due.
- An automatic association meets a live staff lease: it yields and retries
  later against the current version.
- No operation may reuse a U-reference, a Case/PO, or an occurrence identity.

## Acceptance evidence

Acceptance proves, through the real Worker and Web callers: durable receipt
before acknowledgement; idempotent replay of the same occurrence; visible
conflict on reused identity; one Case per definitive instruction; the
Unidentified reasons, Close, Reopen, and automatic resolution; the Intake log
outcomes and the three technical actions; the p95 five-second target from
durable receipt; and that no edit lease is bypassed by automatic association.
Deployment and live evidence are separate tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `INT-01`–`INT-12`, `INT-14`, `INT-15`, `INT-18`–`INT-26`,
  `INT-29`, `INT-30`, `INT-33`, `EXT-15` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-03](frd-03-triage.md),
  [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-08](frd-08-email-mailbox-and-background-processing.md),
  [FRD-09](frd-09-provider-and-intermediary-routes.md),
  [FRD-10](frd-10-mcp-automation-and-actor-boundary.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-18](frd-18-manual-upload-and-upload-links.md),
  [FRD-19](frd-19-image-led-intake-and-pairing.md).
- Technical constraints:
  [ADR-0044](../adr/0044-mail-occurrence-and-business-identity.md) (mail
  identity), [ADR-0029](../adr/0029-image-initiated-case-projection.md)
  (Image-initiated projection).
