# FRD-02: Intake and source identity

> Owner capabilities: EXT-17, INT-01, INT-03, INT-08, INT-09, INT-18, INT-23, INT-26, INT-33 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Receiving material is not creating a Case. Pegasus keeps the original
  bytes and their identity first, then works out what the material is.
- Anything safely kept but not understood becomes one Unidentified item
  with a permanent `U<n>` reference and one of seven reasons. This document
  owns what Unidentified means.
- Receipt is acknowledged only after the bytes, the receipt and one
  processing-dispatch record have committed. The Worker alone processes it.
- The gates before a Case exists, and matching, are in
  [FRD-22](frd-22-pre-case-gates-matching-and-association.md). Case fields,
  provenance and the global checks are in
  [FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md).
- Manual upload and public upload links are in
  [FRD-18](frd-18-manual-upload-and-upload-links.md). Image-only
  material is in [FRD-19](frd-19-image-led-intake-and-pairing.md).

## Purpose

This document says how Pegasus receives material, proves it was received,
processes it once, and holds what it cannot place as Unidentified. It serves
the PRD outcomes for durable intake and no invented identity. The gates
before a Case is created, matching and association are owned by
[FRD-22](frd-22-pre-case-gates-matching-and-association.md). Case fields,
provenance and the global vehicle and value checks are owned by
[FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md). Manual
upload is owned by [FRD-18](frd-18-manual-upload-and-upload-links.md) and
image-only material by [FRD-19](frd-19-image-led-intake-and-pairing.md).

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

**Tractable capture (`EXT-17`).** Guided image capture happens outside
Pegasus. Collision Engineers sends permanent external upload links that run
through Tractable, and Tractable then emails a PDF. Pegasus has no Tractable
integration. The emailed PDF is ordinary inbound mail: it follows the same
receipt, classification and extraction rules as any other attachment. Which
fields Pegasus reads from the Tractable PDF is an
[open decision](../open-decisions.md).

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

## States and transitions

- A receipt is Received, Processing, Complete, or Failed. Failed splits into
  retryable (Allocation failed, OCR failed, Processing failed) and terminal.
- Its destination is one of: Case created, Linked to Case, Vehicle images
  (an Image-initiated Case, [FRD-19](frd-19-image-led-intake-and-pairing.md)),
  Triage, Unidentified, Could not be read, or Closed.
- An Unidentified item is open or resolved. Close with reason and Reopen
  move between them. Reaching a real destination resolves it.
- Whether a Case may be created, and the state it starts in, are owned by
  [FRD-22](frd-22-pre-case-gates-matching-and-association.md#mandatory-pre-case-gates).

## Edge cases and fail-closed behaviour

- Same occurrence, different bytes: an identity conflict, nothing created.
- Destination write fails after evaluation: the work stays retryable with
  the same evaluation identity; no second Case is allocated.
- Publication of the work identifier fails: the commit stands and recovery
  picks the work up within one minute.
- A file that cannot be read: Unidentified, reason Could not be read, with
  the file kind, so it still ages and falls due.
- No operation may reuse a U-reference, a Case/PO, or an occurrence identity.

## Acceptance evidence

Acceptance proves, through the real Worker and Web callers: durable receipt
before acknowledgement; idempotent replay of the same occurrence; visible
conflict on reused identity; the Unidentified reasons, Close, Reopen, and
automatic resolution; the Intake log outcomes and the three technical
actions; and the p95 five-second target from durable receipt. Deployment
and live evidence are separate tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `EXT-17`, `INT-01`, `INT-03`, `INT-08`, `INT-09`, `INT-18`,
  `INT-23`, `INT-26`, `INT-33` in [capabilities](../capabilities.md).
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
  [FRD-19](frd-19-image-led-intake-and-pairing.md),
  [FRD-22](frd-22-pre-case-gates-matching-and-association.md),
  [FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md).
- Technical constraints:
  [ADR-0044](../adr/0044-mail-occurrence-and-business-identity.md) (mail
  identity), [ADR-0029](../adr/0029-image-initiated-case-projection.md)
  (Image-initiated projection).
