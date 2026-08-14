# FRD-02: Intake and source identity
> Owner capabilities: INT · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design.md

## Intake and source identity

### Ways intake starts

Intake may begin through staff-forwarded email, a staff-created request-scoped upload link, provider material, manually supplied files, images, correspondence, or a future approved API route. Receipt is not case creation.

Image-only material with a usable normalised VRM creates a pre-Case Image intake with an Image Intake Reference; it is not `Needs sorting` merely because it lacks a formal instruction or accepted Principal. A usable normalised VRM is a staff-confirmed registration or an automatic engine read that meets the accepted recognition bar (operator-accepted 2026-08-03; [operations § dated evidence](../operations.md#dated-evidence-qualifications) owns the accepted numbers). Image material without a usable normalised VRM remains `Needs sorting`. An Image intake is never allocated a Case/PO or promoted into a Case merely because images arrived.

Every intake path must:

- preserve original source bytes and message/file identity before deriving text or classifications;
- retain sender, recipients, subject, message identifiers, timestamps, attachment names, content types, byte lengths, hashes, and parent/placement relationships where available;
- be idempotent for the same source occurrence without collapsing distinct visible placements;
- surface unsupported, incomplete, corrupt, encrypted, oversized, ambiguous, or technically failed input as an explicit decision rather than silently dropping or accepting it;

- record the actor, time, caller, source, policy version, and reason for every transition;
- prevent untrusted content from becoming instructions, policy, identity, or authority.

When a retained source remains `Needs sorting` because no category can be determined, the UI explains the missing, ambiguous, or contradictory predicates rather than presenting the positive rationale for an unrelated category.

### Request-scoped upload links

**Accepted source boundary:** only authenticated staff may create a link. The token has a stable identity and
is bound to exactly one upload request, its allowed operation, and a
server-enforced expiry. It is security-sensitive and is never written to
permanent business history, message content, or content-bearing telemetry.
Token generation and at-rest representation remain implementation choices;
acceptance must prove expiry, revocation, and cross-request isolation through
the real caller. Revocation invalidates every later request, and an
unauthenticated caller cannot extend expiry.

The public page exposes only the bound request's upload fields and its immediate
structured success or failure. It exposes no case or reference identity,
request/history state, other document, token-management function, external
account, or cross-request lookup. An accepted upload result means only that the
request-local custody boundary succeeded; it is not case creation, Box custody,
EVA handoff, report generation, or external delivery.

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
not form another pre-Case acceptance gate. An Audit's retained original report
is identity-critical: without one separate report with one literal outcome,
Pegasus cannot determine whether the reference is `a.` or `ap.` and enters
`Needs sorting`. The manual case-create screen does not offer Audit; it is
created only by this retained-email route. If the route cannot establish an identity-critical fact, it persists only what is safe and enters the
corresponding pre-Case outcome. `Blocked intake` records a reason and visible
warning, offers reasoned resolve and retry actions, and retains the resolution
evidence and each retry result. It never allocates a reusable identity as a
convenience.

Box case-file custody is a required day-one alpha capability, but it follows Case/PO allocation: Pegasus uses the newly allocated immutable reference to create the Box case folder and stores the retained source material there. Blob staging remains temporary hot processing storage, not accepted Case custody. A Box folder or filing failure retains the allocated Case as `Not ready`, records the exact failure and staff-initiated retry/recovery evidence, and prevents progression that requires accepted Case custody; it never rolls back, reuses, or reallocates the immutable Case/PO reference. No background or automatic business retry is permitted.

### Matching conflicts and reversible association

Matching uses explainable evidence. Message identifiers, provider/domain policy, route identity, accepted reference tokens, VRM, party identity, and operator confirmation may contribute. A weak, ambiguous, or contradictory signal never silently associates material with a case; competing candidate cases and unresolved source-identity conflicts remain visible in `Needs sorting`.

VRM correlation is a suggestion until confirmed by accepted evidence or an authorised operator. Source deduplication is occurrence-aware: exact bytes and transport identifiers support correlation, while each visible placement and chronology entry remains auditable.

Arrival-time proximity never associates or consolidates material. A mismatch
between accepted incident dates may eliminate a candidate; a matching incident
date proves nothing alone and requires corroborating accepted evidence before
association or consolidation.

The immutable source occurrence and its evidence remain distinct from the accepted, editable Case projection. Linking creates a versioned source-to-case relationship; it never converts the source into the case, rewrites source facts, or changes the original intake origin.

An Image intake remains pre-Case until its retained evidence can associate with exactly one eligible pre-report instructed Case. Automatic association requires an unambiguous normalised VRM match and no explicit contradictory identity evidence; otherwise an authorised staff member makes the reasoned decision. A Case after report delivery is not eligible. Association retains both permanent identities and source histories: the instructed Case/PO remains the sole Case identity and the Image Intake Reference remains linked history. Before report delivery, authorised staff may reasonedly reverse or correct the association; the intake returns to awaiting instruction, the instructed Case recomputes readiness, and neither identity, source fact, or relationship event is reused, rewritten, or deleted.

Each direct Case datum retains its current field provenance: staff entry,
extraction, AI prefill or proposal, provider API, or another external
vehicle/estimate source with its applicable identity, version, and time.
Operator UI shows that provenance without treating it as confirmation. A
derived value identifies its accepted inputs and calculation rather than
claiming a separate raw source; provenance and value status remain distinct.

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
