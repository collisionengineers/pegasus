# FRD-02: Intake and source identity
> Owner capabilities: INT · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Intake and source identity

### Ways intake starts

Intake may begin through staff-forwarded email, a staff-created request-scoped upload link, provider material, manually supplied files, images, correspondence, or a future approved API route. Receipt is not case creation.

Direct Case creation is one of those ways, not an exception to them (D26, 2026-09-01). Staff enter the required identity and either attach the instruction or record it. Pegasus persists an attributable intake receipt for that instruction — actor, time, the attached or recorded instruction and its custody identity — and only then reuses the normal principal resolution and Case/PO allocation policy. There is no parallel allocation implementation and no direct-creation route that skips the receipt: the allocator that serves automatic creation is the only allocator.

Image-only material with a usable normalised VRM creates a searchable Image-initiated Case projection with an Image Intake Reference; it is not Unidentified merely because it lacks a formal instruction or accepted Principal. A usable normalised VRM is a staff-confirmed registration or an automatic engine read that meets the accepted recognition bar (operator-accepted 2026-08-03; [FRD-06](frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis) owns the accepted threshold). Image material without a usable normalised VRM enters Unidentified with a required reason. An Image-initiated Case is never allocated a formal Case/PO; it merges into one matching formal Case or is staff-closed with a reason.

A usable registration therefore settles into one of two outcomes the operator
sees (operator ruling, 2026-08-19): when it matches no existing Case, the
Image-initiated Case is the visible, searchable, awaiting-instruction outcome
until something changes it; when it matches exactly one eligible Case at
registration time, the reference is still allocated but the automatic merge
below runs in the same pipeline pass, so what the operator finds is the images
already attached as evidence on that Case, with the Image-initiated reference
retained as linked history rather than a separate open record.

### Unidentified destination and reference

Safely retained material whose identity, meaning, ownership, or destination cannot be
established becomes one `UnidentifiedItem` for the source occurrence or inseparable
submission group. Group membership is durable: one group receives one `U<n>` reference
and every member keeps its own filename, receipt identity, custody, and chronology.
The reference is uppercase `U` followed by positive, invariant, unpadded decimal
digits, allocated atomically from a dedicated sequence and never reused. The item
stores one of the six Core-owned reasons—unreadable/corrupt, unsupported, no usable
identification, conflicting identification, ambiguous ownership/destination, or
terminal technical processing failure—and bounded safe detail. Retryable work does
not allocate a reference.

Unidentified is open or resolved. Authorised staff resolution requires an operation
key, expected version, reason, and one supported destination; it appends immutable
history with actor, time, target, and before/after state. Replays return the original
result; conflicting operation reuse fails closed. An open item whose origin receipt
subsequently reaches a real destination — a formal Case, or a registered Image
intake — is resolved automatically to that destination by the product's own
reconciliation (in the receipt's own processing pass, and by a sweep for receipts
promoted outside their own pass), with the destination recorded in the item's
history; a receipt that is still legitimately unidentified is never force-closed.
The U-reference is never accepted as a Case/PO, Audit, Image Intake, or principal
identity.

Every intake path must:

- preserve original source bytes and message/file identity before deriving text or classifications;
- retain sender, recipients, subject, message identifiers, timestamps, attachment names, content types, byte lengths, hashes, and parent/placement relationships where available;
- be idempotent for the same source occurrence without collapsing distinct visible placements;
- surface unsupported, incomplete, corrupt, encrypted, oversized, ambiguous, or technically failed input as an explicit decision rather than silently dropping or accepting it;

- record the actor, time, caller, source, policy version, and reason for every transition;
- prevent untrusted content from becoming instructions, policy, identity, or authority.

When a retained source becomes Unidentified because no category can be determined, the UI shows its U-reference, canonical reason, bounded safe detail, source/group, custody, and next permitted action rather than presenting the positive rationale for an unrelated category.

### Request-scoped upload links

**Accepted source boundary:** only authenticated staff may create a link. The token has a stable identity and
is bound to exactly one upload request, its allowed operation, and a
server-enforced expiry. It is security-sensitive and is never written to
permanent business history, diagnostic logs, or content-bearing telemetry.
The functional upload link may be delivered to its intended recipient in a
staff-initiated chaser; that bounded delivery is not permission to expose it
elsewhere. Use a cryptographically generated 256-bit token retained only as a hash;
acceptance must prove expiry, revocation, and cross-request isolation through
the real caller. Revocation invalidates every later request, and an
unauthenticated caller cannot extend expiry.

The public page exposes only the bound request's upload fields and its immediate
structured success or failure. It exposes no case or reference identity,
request/history state, other document, token-management function, external
account, or cross-request lookup. An accepted upload result means only that the
request-local custody boundary succeeded; it is not case creation, Box custody,
EVA handoff, report generation, or external delivery.

A link starts a fixed, non-sliding 15-minute submission session when it first
accepts a file successfully (D20, amended 2026-09-02). Failed attempts before
that first success do not start the session. The requester may add or replace
files until explicit finalisation or session expiry; either event closes the
link and all later bytes are refused without disclosing the Case or earlier
submission. Idempotent retries reconcile to the same result. The current
manual/public source limits are owned by Source upload limits below.

Public POST admission checks the route-bound token before reading the form.
Unavailable links refuse the body without buffering it. File and total-body
transport bounds apply before antiforgery and multipart model binding; normal
multipart overhead is allowed within a finite bound. Enforcement counts actual
bytes, including requests with absent or understated Content-Length, and an
oversized refused body never reaches custody. Form or query values cannot
substitute a different token for the one in the route.

File type/count/size limits, authentication of the staff creator, token expiry
and revocation, idempotent retry, abuse handling, durable custody, cross-request
isolation, and non-disclosing error behavior are acceptance gates.
Every attempt returns the same bounded result classes without revealing whether
another request, case, reference, or file exists. This in-house route supersedes
Box File Request behavior.

### Source occurrence and dispatch identity

A source occurrence is the channel-scoped receipt identity for one visible receipt or placement. It is distinct from its content hash, extracted evidence, processing dispatch, and any accepted Case projection.

- Replaying the same occurrence with the same bytes returns the existing receipt.
- Reusing an occurrence identity for different bytes is a visible identity conflict; it creates no new receipt, association, case, or reference.
- Equal bytes received under different permitted occurrence identities remain separate evidence with separate provenance.

Pegasus acknowledges receipt only after the original bytes, source receipt, and one durable processing-dispatch record commit. Each dispatch has its own stable idempotency identity tied to the source occurrence; a queue carries only the stable source/work identifier, never the payload. This acknowledgement means “durably received for processing,” not classified, associated, accepted as a case, completed, or closed.

The Web receipt path stages work as pending and never executes queued-intake
processing. The Worker is the sole processing owner: it dispatches pending work,
claims queue deliveries idempotently, recovers expired leases, and records a
completed or failed outcome. Duplicate delivery must not duplicate an evaluation,
case, reference, or downstream side effect. Staff can inspect Received,
Processing, Complete, or Failed by the staged receipt identifier; failure wording
is bounded and does not disclose exception or infrastructure detail.

An evaluation is recorded before its destination is written, but the work is
not complete until required association, allocation, Triage, and Unidentified
writes finish. A transient destination failure retains a retryable work item
and the same evaluation identity; it must not allocate a second case. A recorded
unique case match withholds new-case allocation even when its association has
not yet been persisted. Staging is cleaned only after that durable completion.

A multi-image submission has one group-level destination and, when unresolved,
one Unidentified reference with the group's canonical reason. Pending groups
are recovered by the existing bounded sweep, selecting eligible oldest groups
before applying its page limit. Groups with an Unidentified outcome leave that
recovery set; unrelated newer receipts cannot starve a pending image group.

For both email and manual upload, the durable commit is followed immediately by
a best-effort publication of the stable work identifier. Publication never
precedes the commit and a publication failure never rolls it back. Pending work
that was not published, including work marked dispatched whose queue delivery
never became claimable, is eligible for idempotent recovery within one minute.
The recovery sweep is a safety net rather than the normal scheduler. The same
rule applies to external or custody work created by the completed intake pass:
commit first, publish its identifier immediately, and reconcile missed
publication without repeating an accepted downstream side effect.

The ordinary path records correlated timings for durable receipt, publication,
queue claim, source reading, identification, classification, extraction,
association/allocation, case creation, custody hand-off, and terminal state.
Those timings contain identifiers and bounded outcome data, never source
content. From durable receipt, ordinary supported principal email and manual-upload
work reaches its case destination, or its truthful terminal non-case outcome,
within five seconds at p95. Case custody confirmation is measured as the final
best-effort segment and any Box/provider delay is attributed separately. A
large, retrying, or legitimately incomplete item remains Received or Processing
with no older terminal outcome projected over it.

### Mandatory pre-case gates

Before creating a case or allocating a reference, Pegasus must establish:

- successful source persistence and required extraction/classification receipts;
- authenticated Principal identity and the staff actor where the route requires staff;
- provider/intermediary route identity and enabled policy where relevant;
- unambiguous case type and Principal association;
- processing and size/format limits;
- absence of unresolved wrong-Principal, duplicate-occurrence, receipt-integrity, or source-custody ambiguity.

Once those identity-critical facts are established, Pegasus creates the Case/PO
and allocates its permanent reference. Incomplete ordinary business detail,
images, or mandatory external checks retain that Case as `Not ready`; they do
not form another pre-Case acceptance gate. For standalone Audit, allocate the
normal Case/PO once Principal and Audit case type are definitive. Missing or
ambiguous original-report outcome withholds only the later `a.` or `ap.` Audit
reference, as defined in FRD-01. Supported email and Provider API Audit routes
use that same distinction. A manual proposal may create an Audit only when its
own retained receipt is already classified Audit and has its existing
standalone-Audit evidence; the existing acceptance gate verifies that evidence
belongs to that receipt.
If the route cannot establish an identity-critical fact, it persists only what is safe and enters the
corresponding pre-Case outcome. `Blocked intake` records a reason and visible
warning, offers reasoned resolve and retry actions, and retains the resolution
evidence and each retry result. It never allocates a reusable identity as a
convenience.

Box case-file custody is a required day-one alpha capability, but it follows Case/PO allocation: Pegasus uses the newly allocated immutable reference to create the Box case folder and stores the retained source material there. Blob staging remains temporary hot processing storage, not accepted Case custody. A Box folder or filing failure retains the allocated Case as `Not ready`, records the exact failure and staff-initiated retry/recovery evidence, and prevents progression that requires accepted Case custody; it never rolls back, reuses, or reallocates the immutable Case/PO reference. No background or automatic business retry is permitted.

### Matching conflicts and reversible association

Matching uses explainable evidence. Message identifiers, provider/domain policy, route identity, accepted reference tokens, VRM, party identity, and operator confirmation may contribute. A weak, ambiguous, or contradictory signal never silently associates material with a case; competing candidate cases and unresolved source-identity conflicts become Unidentified with the corresponding canonical reason.

The fifteen evidenced principal email routes use the existing instruction
profiles through one route, classification and match policy ownership chain.
Exact sender identity and selected current document profile must agree;
document identity alone never allocates a Case. A proved forwarded original
is current material, not discarded quoted history. Unrelated reports and old
thread content cannot supply or veto a current instruction's profile.
The accepted identities, explicit work-type predicates, preserved QDOS
body/Triage rules and shared fail-closed association procedure are owned by
[FRD-09](frd-09-provider-and-intermediary-routes.md#accepted-principal-email-routes-and-automatic-association).

Acceptance joins a profile's typed instruction values to the canonical Case
field identity, not another principal's printed field labels. The original
review-field names, candidates and source locators remain unchanged. Missing,
conflicting or non-unique source attribution still prevents allocation; a
different label never justifies invented evidence or staff confirmation.

VRM correlation is a suggestion until confirmed by accepted evidence or an authorised operator. Source deduplication is occurrence-aware: exact bytes and transport identifiers support correlation, while each visible placement and chronology entry remains auditable.

Arrival-time proximity never associates or consolidates material. A mismatch
between accepted incident dates may eliminate a candidate; a matching incident
date proves nothing alone and requires corroborating accepted evidence before
association or consolidation.

The immutable source occurrence and its evidence remain distinct from the accepted, editable Case projection. Linking creates a versioned source-to-case relationship; it never converts the source into the case, rewrites source facts, or changes the original intake origin.

An Image-initiated Case remains Awaiting instruction until its retained evidence can associate with exactly one eligible pre-report instructed Case. Automatic association requires an unambiguous normalised VRM match and no explicit contradictory identity evidence; otherwise an authorised staff member makes the reasoned decision. A Case after report delivery is not eligible. Association retains both permanent identities and source histories: the instructed Case/PO remains the sole formal Case identity and the Image Intake Reference remains linked history. On a unique match the Image-initiated Case becomes Merged into Instruction-initiated Case; if instructions never arrive, staff may record a permanent Staff-closed outcome with a reason. Neither identity, source fact, or relationship event is reused, rewritten, or deleted.

Image-only material with a usable VRM therefore creates a searchable Image-initiated Case reference, not a formal Case/PO. A group with no usable VRM or conflicting valid VRMs follows the Unidentified contract with its explicit reason marker instead.

Pairing uses the Case's current accepted registration and principal, not its
original instruction draft. A registered image identity requires an exact
registration match; a known principal must agree. The existing single-image
exact-match precedence also applies to one-member groups. Multi-member groups
retain their stricter complete-candidate uniqueness rule; persisted expected
membership, not the presence of a group identifier, distinguishes them.

Both arrival orders, registered-receipt replay and acceptance replay resume
the same pairing operation. The existing reconciliation timer retries oldest
eligible Awaiting instruction records, including linked-but-unmerged records.
Current nonmatches do not consume that bounded batch or become permanently
excluded. Failures remain visible and do not stop unrelated pairings.
Every current image member must be associated before a group merges once.
Automatic writes recheck current identity and uniqueness transactionally;
merge rechecks every current association, destination eligibility and active
Case edit lease. A deliberate staff unlink or reassignment is never undone by
recovery. A still-current reasoned staff association retains its authority,
including an intentional identity override, regardless of the retry actor.
A reasoned staff decision on a registered group's origin also authorises
completion of untouched image members. That completion checks the current
origin decision and its observed version, records the originating staff
identity/reason/version with SystemWorker completion attribution, and never
overwrites or revives a sibling with association history. Final merge also
refuses a changed origin decision even when the target Case ID is unchanged.

**Age and chase state (INT-32).** Each half of a pairing keeps its own chronology: the instruction side's opened/received timestamp and the Image-initiated Case's own `RegisteredAtUtc`, both already visible on their respective queue rows — no relative "age" figure is computed or shown anywhere in the application, so none is introduced for either half. While an Image-initiated Case is Awaiting instruction, its chase-due state is a derived read, not a persisted schedule: it is due once `RegisteredAtUtc` has stood for the same configured chase interval a Not-ready formal Case's first chase falls due at (one global whole-calendar-day value, 1 to 365, default 7 — D23), and not-due before that. There is no held or stopped state and no generated chaser draft for the image half — those exist on the Case side because a formal Case has manual chase-pause controls and outbound chaser text; an Image-initiated Case has neither, and this ticket does not add them. Pairing completion remains visible the way INT-32's coupled INT-28 already delivered it: the derived `Associated with Case` label wherever the origin receipt's case association is shown, and the merge event recorded on the resulting Case's own history the moment it happens — not a separate notification.

Triage association follows [FRD-03](frd-03-triage.md#automatic-association-with-a-formal-case):
creation, formal acceptance and replay attempt the same principal-scoped
typed-identity match; existing scheduled reconciliation retries unfinished
links. The final transaction rechecks current evidence, complete candidate
uniqueness, versions and staff edit authority. A deliberate manual unlink or
reassignment remains authoritative. This association preserves the distinct
Triage identity and workflow and creates no further Case/PO.

### Grouped image-intake routing

**Settled operator truth (2026-08-19):** a retained vehicle image either shows a
readable registration that matches an existing eligible Case — in which case
every image attaches to that Case as evidence — or it shows a readable
registration that matches no existing Case, in which case it starts the
Image intake's own pre-Case identity. A multi-file image submission from a
manual upload or the eligible mailbox-attachment route below is one evidence
group, not a set of independent images: a damage close-up carrying no
registration must not detach itself from an overview image submitted with it,
and the group — never an individual image — is the unit
that reaches an association, a pre-Case Image intake registration, or a
`Unidentified` outcome.

- **Mailbox attachment entry.** When a newly processed mailbox message would
  otherwise enter Unidentified, has not been routed as an instruction, Case or
  Triage, and contains one or more direct `image/*` attachments, those direct
  attachments are submitted as one image group through this same grouped
  lifecycle. The original EML source, inline images, and images derived from
  another attachment are excluded. The child image receipts preserve the
  mailbox source channel and a durable parent-receipt relationship; the parent
  email does not also receive an Unidentified item. Existing group precedence
  then produces exactly one settled result: association to one eligible Case,
  one Image-initiated Case, or one group-level Unidentified item for
  no-readable/conflicting registrations. A terminal failure to submit the
  attachments produces one technical-processing Unidentified item for the
  parent receipt. Completed historical mail is not backfilled or replayed by
  this rule.

- **Membership and completeness.** A group's member count is fixed at the
  originating submission and is never inferred from however many members
  happen to be durably stored yet. Routing is evaluated only once every
  declared member is present and every present member's image evidence
  carries a terminal recognition outcome (a suggestion, no-readable-result, an
  unavailable dependency, or a technical failure all count as terminal — an
  empty or still-processing result does not). A group short of its declared
  membership, or carrying any non-terminal member, reaches no decision and is
  re-evaluated as later members complete.
- **Non-image members are excluded.** A batch may mix vehicle images with
  other material submitted in the same request (for example an instruction
  document). Only members whose retained evidence is image-only material
  contribute to recognition and to the group's routing decision; a non-image
  member's presence still counts toward the declared membership check above,
  but it is never scanned for a vehicle registration and never blocks the
  image members' own decision.
- **Distinct-VRM aggregation.** Only reads at or above the accepted automatic
  recognition bar count. The decision inspects the distinct set of accepted,
  normalised VRMs across every image member in the group — never one member's
  read in isolation.
- **Associate-or-hand-off precedence, applied in this order:**
  1. Any image member's recognition ended in a technical failure or an
     unavailable dependency: the group fails closed to a named technical
     outcome. No association or registration is attempted while any member's
     evidence is unreliable.
  2. Exactly one distinct accepted VRM across the group, and exactly one
     eligible pre-report instructed Case carries it: every member in the
     group associates to that Case as evidence, under the same unambiguous
     normalised-match rule (including its confirmed-registration completion
     for a one-character-missing read) that governs single-image association
     above.
  3. Exactly one distinct accepted VRM, but zero or more than one eligible
     instructed Case carries it: the VRM is usable but not uniquely matched.
     The group registers as **one** pre-Case Image intake identity — exactly
     one Image Intake Reference is allocated for the whole submission group,
     never one per member — and every member's receipt and retained evidence
     records against that single registration; none associates to any Case.
     This FRD does not re-specify the further searchable lifecycle of that
     pre-Case identity.
  4. Zero distinct accepted VRMs, or more than one (conflicting readable
     VRMs): no single usable identity exists. The intact group — every member
     together, kept as one unit — remains `Unidentified`; no VRM-based
     reference is fabricated for it, and no member is split off into an
     unrelated generic outcome.
- **Fail-closed is a group property, not a per-member one.** Case 3 and case 4
  above ("no unique match" and "ambiguous/conflicting") are handled
  distinctly but both withhold association. A per-member candidate search run
  while registering that member's evidence must never resolve an ambiguity
  the group itself did not resolve: if the group's own eligible-Case count
  for its one accepted VRM was zero or more than one, no member of that group
  may associate to a Case, even where a member-level search over the same
  candidates could otherwise select one by exact match. The group's own
  decision is the sole authority for whether association happens.
- **Recognition is idempotent per retained image.** A group can be
  re-evaluated more than once (a sibling member arriving, a replay). An image
  whose recognition outcome is already durably recorded is never re-scanned;
  the recorded outcome is reused so each retained image is recognised once
  regardless of how many times its group is evaluated.

Each direct Case datum retains its current field provenance: staff entry,
extraction, AI prefill or proposal, provider API, or another external
vehicle/estimate source with its applicable identity, version, and time.
Operator UI shows that provenance without treating it as confirmation. A
derived value identifies its accepted inputs and calculation rather than
claiming a separate raw source; provenance and value status remain distinct.

### Upload confirmation surface

A grouped upload exposes **one submission decision** — whether the submission
itself was accepted or refused — with the per-file processing and outcome
details beneath it (D20, 2026-09-01). The submission decision is never a
summary that hides a per-file outcome.

Once a manually uploaded file's processing resolves, the operator sees an
explicit destination decision rather than a passive status label. Manual upload
retains the source and its extraction but never automatically associates it,
allocates a formal Case/PO, or treats a unique match as staff consent. Mailbox
and Provider routes retain their own automatic policy.

The decision table, evaluated against the current retained material:

1. **A case is already associated** (`CurrentCaseId` set). This is a report of
   an already committed decision. The operator sees the case reference and the
   existing reversal path; the confirmation surface does not invent another
   association mechanism.
2. **Registered as a new Image-initiated Case** (`ImageIntakeRegistered`).
   Also always automatic (a usable VRM with no unique existing-Case match);
   reported with a link to its own searchable surface, never re-offered as a
   manual creation (an Image-initiated Case's reference is VRM-keyed and
   cannot be hand-created without one). While the registration is still
   Awaiting instruction, the surface additionally offers the staff decision
   to add the uploaded material to an existing case found by search (below);
   that decision links the registration's origin receipt, which carries the
   Image-initiated Case through its normal merge transition. Once merged,
   the surface reports the destination case instead of the registration.
3. **A manual non-image file eligible to become a Case.** Staff must either
   confirm one viable existing Case or open the extracted new-Case proposal.
   A unique match is one suggestion, not an automatic selection. The proposal
   is editable; reject/cancel changes nothing and leaves the source
   unallocated. Acceptance alone runs the existing allocation path and may
   allocate the Case/PO.
4. **Cannot become a case** (blocked, unsupported, or a technical failure) or
   **the file itself failed to process.** Reported plainly; no offer, since
   none is genuine.

Where the staff decision is genuinely open — rows 2 and 3 — the surface also
carries the decision itself:

  - **Add to an existing case.** The retained match candidates are shown
   first, including a sole viable candidate; typed receipt-scoped search adds
   only current viable Cases (reference, registration, claimant and stage
   shown — never an internal identifier). A unique candidate is never an
   automatic selection. Selecting a Case and confirming with a nonblank
   reason of at most 500 characters acquires that
   Case's edit lease and links through the existing staff link path, which also
   runs the Image-initiated Case merge transition where one is registered. The
   route and group membership are loaded server-side; a posted receipt id is
   not authority. The page operation id, reviewed receipt version and reviewed
   target Case version bind the decision. A typed non-script reference first
   renders that exact target for confirmation before any write. A replay is
   successful only for the identical committed staff decision (actor, target,
   reason and reviewed input), not merely the same target. A stale version,
   changed decision, competing lease, unavailable
   destination or incomplete group reports an honest conflict and changes
   nothing further.
- **Cancel.** Returns to the Upload screen and changes nothing: the material
  stays retained and its state stays honestly reported.

For a group, the operator makes one submission-level choice. Completed members
must prove the same decision; missing, elsewhere-associated or failed members
are reported rather than silently counted as success. Every other action the
confirmation surface offers routes to an existing surface that already performs
it (case details, the received-item screen's attach/reverse controls, the
case-creation screen, the Image-initiated Case and Unidentified detail screens).

### Global vehicle and value checks

Every Case must satisfy globally required vehicle identity/specification,
vehicle-history/risk, and market-valuation checks, unless an explicit,
documented exception applies. All three results or their recorded exceptions
are required before staff may accept Case review and expose the Case in the
Engineers queue. The authorised staff reviewer may record an exception as a
named, reasoned Case action in permanent history. Provider and route policy
select the provider, required result, acceptable provenance, and
unavailable/failure behavior for each check; no provider is inferred by this
requirement.

Vehicle details are extracted from the instruction where available, otherwise
obtained from the applicable DVLA/MOT source. Mileage evidence ranks as:

1. an accepted staff-entered value;
2. directly extracted instruction text;
3. Document Intelligence extraction from a scanned instruction or future
   odometer-vision evidence; and
4. a DVSA-derived estimate.

DVSA is run for every Case. Where no higher-tier mileage value is available, it
supplies the source-labelled estimate. A difference between DVSA mileage and
any accepted staff-entered, instruction-extracted, Document Intelligence, or
odometer value is a visible Case discrepancy. The later odometer-vision
capability does not imply an activated AI caller before its own accepted
evaluation and integration contract.

The DVSA estimate follows [ADR-0012](../adr/0012-conservative-mot-mileage-estimation.md):
it preserves raw observations, validates units, groups fail/retest episodes,
segments corroborated odometer drops, and excludes implausible or
low-information intervals without deleting them. It uses a recency- and
quality-weighted median of clean rates, with a versioned cohort prior only for
eligible sparse histories; interpolation and forecasting remain bounded. An
estimate without eligible chronological holdouts is a wider, explicitly
non-probabilistic range and never defaults into the Case.

Definitive authorised intake creates exactly one instructed Case idempotently. A definitive match to an existing instructed Case allocates no duplicate. A new instructed Case enters `Not ready` until its ordinary business detail, required source images, and applicable progression requirements are satisfied; the route may move it to `Review` only when its explicit policy permits that transition. The allocation decision adds no universal manual acceptance gate.

One source occurrence has at most one current Case association. Every automatic or manual association records the exact source and Case identities, evidence, actor, time, policy/version, and reason where required. Any authorised staff member may reasonedly unlink or reassociate a mistaken match; the prior relationship and both source origins remain permanent, and dependent facts and counts recompute without deleting history.

Automatic mail association does not wait for a staff editor. It writes only the receipt's own append-only association and history records, never the Case row or its version, so it is one of the background records [FRD-01](frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery) holds separate from editable Case state, and an editor's pending save still validates against the version they loaded. It still yields to an archived case. The staff "add to an existing case" decision above is a Case mutation and acquires the edit lease as any other does. Automatic Image-initiated Case association checks the current Case version and yields to an active staff lease; the subsequent image merge also yields to a live lease and rechecks the current associations inside its transaction.

## Source upload limits

The anonymous link remains a hashed 256-bit token and discloses no Case.
The accepted source limits are 100 MiB per file, 20 files and 200 MiB
aggregate bytes per multipart request, plus 64 KiB of fixed multipart
overhead. The Provider API keeps its separate 30 MiB decoded envelope and
42 MiB encoded request limits.

| Setting | Accepted source value |
| --- | --- |
| Aggregate file bytes | 209 715 200 (200 MiB) |
| Multipart request | 200 MiB plus 64 KiB fixed overhead |
| Per-file bytes | 104 857 600 (100 MiB) |
| File count | 20 |
| Link lifetime | 168 h (7 days) |
| Submission session | Fixed, non-sliding 15 minutes |
| Rate, per token | 20 per 10 minutes |
| Rate, per address | 30 per minute |
| Content types | `application/pdf`, `image/jpeg`, `image/png`, `…wordprocessingml.document`, `application/msword`, `message/rfc822`, `application/vnd.ms-outlook` |

`IntakeEnvelopeLimits` is the single Core owner of the manual/public
per-file, file-count and aggregate ceilings. `RequestUploadLimits` may
tighten those ceilings for a configured estate and may never raise them.
The public-upload session reserves capacity before custody, admits at most
one current successor for a replacement occurrence, and expires after its
fixed 15-minute window. Expiry, revocation, limit-version mismatch and
capacity refusal are typed outcomes; none authorises a fresh upload.

The per-address limiter remains necessary because an unknown token is
refused before the token-partitioned limiter runs, while Razor's antiforgery
handling may otherwise buffer the multipart request first. It runs after
routing and before endpoint execution.

The repository's deployment configuration and dated live evidence are
separate from this accepted source policy and remain owned by
[operations](../operations.md). Existing links are bound to the limits version
and lifetime recorded when issued; a mismatch fails closed, after which
staff may issue a new link through the existing Case action.

## Instruction field meanings

A Work Instruction contains details of a claimant involved in a road traffic accident. Capture:

| Field | Rule |
| --- | --- |
| Work Provider | Also referred to as the principal. |
| Claimant Name | Extract from the instruction. |
| Claim Number | External reference number. |
| Vehicle Registration | VRM. |
| Vehicle Make | Extract from the instruction or obtain through an authorized lookup capability when absent. |
| Vehicle Model | Extract from the instruction or obtain through an authorized lookup capability when absent. |
| Vehicle Mileage | Extract when supplied; estimation from MOT data is a required capability when available. |
| Accident Circumstances | Extract from the instruction. |
| Date of Incident | Extract from the instruction. |
| Instruction Date | Use the document value; if absent, default to the current date. |
| Inspection Address | Use FRD-06 inspection-location rules. |
