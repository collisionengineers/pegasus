# FRD-09: Provider and intermediary routes

> Owner capabilities: API-01 to API-04, INT-04 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Who sent it, which route it came by, and which Principal it is for are
  separate facts. A matching domain never picks a Principal on its own.
- The Provider API is create-only. A provider states its instruction in JSON
  with its files attached; Pegasus does not read the values back out of a
  document.
- Fifteen Principal email routes are accepted, each tied to exact domains or
  mailboxes. No wider domain matching is allowed.
- A message links to an existing Case automatically only when exactly one
  Case matches and nothing contradicts it. Several matches fail closed.
- Route rules never overlap. An overlap is a defect, not a tie-break.

## Purpose

This document owns how work reaches Pegasus from Principals and
intermediaries: the Provider API contract, the accepted email routes, the
QDOS Triage tells, and the rule that routes must not overlap. It serves the
PRD outcomes for safe, attributable intake from every accepted provider.

A route reaches Unidentified only when material is kept but no unique owner
or destination can be shown; a reasoned refusal is a closed Unidentified item
([FRD-02](frd-02-intake-and-source-identity.md#unidentified-destination-and-reference)).
A technical failure that can be retried stays in processing.

## Provider and intermediary routes

Provider identity, intermediary identity, route identity and provider or
domain-suffix association are four separate facts. The versioned
provider/domain package is evidence and configuration input. Its presence
does not switch on a route, choose a Principal or define an API client.

Direct-provider and intermediary policies may differ, but both call the same
Core intake contract. Both fail closed when route identity, an enabled
policy, the Principal or mandatory evidence is missing. The
[capability inventory](../capabilities.md) owns the exact targets for extra
provider routes and provider APIs.

### Provider API principal and contract boundary

The security boundary is the stable Pegasus Principal, not an email domain
or an external tenant. A provider client gets a separately issued,
Principal-scoped client ID and an opaque secret. Only the secret's hash is
stored. Rotation and revocation are supported.

The client may submit instructions and attachments idempotently, and read
only its own Principal's receipt, processing status and resulting Case/PO.
It gets no staff access, no general Case search or read, and no Case workflow
changes.

Provider operations use the same Core intake and authorisation policies as
Web and Worker callers. Receipt, submission, status, result, source custody
and idempotency identities stay separate per Principal. The provider client
is the recorded actor. Any cross-Principal query or disclosure fails closed.
The transport channel never changes extraction, instruction eligibility or
automatic allocation: a definitive Provider API instruction follows the same
Case-creation path as an equally definitive email. API-01 is create-only. It
never links material to, or changes, an existing Case.

API-01 owns the supported routes, schemas, limits and Principal credential
contract. Extra tenancy or identity fields need a concrete accepted consumer
requirement; supplied provider-domain evidence does not invent one.

No provider route is live until its exact capability allocation, accepted
contract, credentials and scopes, failure and recovery proof, real caller and
operator acceptance all exist.

### Accepted API-01 submission contract

The Principal's Pegasus API key is the Provider API credential, delivered
with the submission endpoint. Live activation for a named provider still
needs exact-target approval before any credential is issued.

The surface is a versioned machine surface. It exists only where the
`Features:ProviderApi` gate is on; otherwise it is absent (404). It accepts
no cookie and no staff identity. A Principal credential is accepted nowhere
else.

**The provider states its instruction.** Pegasus does not read it back out
of a document. An earlier draft took files only and relied on the
Principal's extraction policy, which recognises QDOS email and so could not
serve the providers this route is for. A provider integrating over HTTP
already holds the fields, so it states them.

- **Credential.** `Authorization: Bearer pgs_<key id>_<secret>`, the secret
  API-04 issued once. An unknown key, wrong secret, revoked credential or
  inactive Principal is refused with 401 and a recorded security event. The
  event names the key id when one was well-formed, and never the secret.
  Requests are rate-limited per calling address. The limiter runs before
  authentication, because a presented key id is only a claim; partitioning
  on it would let a caller spend another provider's budget or mint a fresh
  budget per request.
- **Submit.** `POST /api/provider/v1/submissions` as `application/json`,
  with a required `Idempotency-Key` header (at most 200 characters, unique
  per Principal). The body declares the instruction and carries its files
  inline as base64.
- **Principal.** The credential establishes it. A `principal` in the body is
  compared with it, and a mismatch is refused (403, recorded). The field
  exists to catch a provider posting to the wrong account, never to select
  one.
- **Case type.** One of `inspection`, `audit`, `auditreport` or `triage`,
  mapping to `Inspection`, `Audit` and `InspectionAndAudit`. `triage`
  allocates no Case/PO and opens a Triage instead
  ([FRD-03](frd-03-triage.md)).
- **Audit.** A standalone `audit` states `originalReportVerdict`
  (`repairable` or `total-loss`) and attaches the original report with its
  role stated. The declared verdict records the assessment. The Audit Case's
  reference is the `a.` value itself, and the verdict never changes it
  ([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
  `auditreport` is Collision Engineers auditing its own report and carries
  neither.
- **Files.** One or more, each with a leaf `fileName`, a `mediaType` the
  intake reader supports, and base64 `contentBase64`. An optional `role`
  (`instruction`, `originalreport`, `image`, `correspondence`, `other`) says
  what the file is. Without it, nothing is inferred and the file is kept as
  an ordinary attachment. Limits: at most 20 files, each at most 10 MiB, at
  most 30 MiB decoded in total, and at most 42 MiB of request body. A larger
  envelope is 413.
- **Retention.** One submission is one intake receipt. The retained source
  is the request exactly as it arrived, and the files are that receipt's
  attachments, the same shape an email instruction has. That is what lets an
  Audit find its original report among its own evidence. The receipt enters
  the same durable intake path as a staff upload, on the `provider_api`
  source channel, bound to the authenticated Principal. The submission is
  the recorded actor in permanent history. If a process crash separates the
  accept writes, the existing reconciliation timer repairs the staged-receipt
  link and the initial `Accepted` history row once intake retention exists.
  A submission is accepted once: whichever of the request and the repair
  records it first is the one row, and a repaired row states when the
  submission was received and that recovery completed it.
- **Provenance.** Every declared value is written to the Case with its own
  provenance, provider API, distinct from extraction and from staff entry,
  and shows as such on the Case. The Work Provider (the Principal) is
  recorded from the authenticated submission binding with provider-API
  provenance.
- **Receipt.** 201 with `submissionId`, `receivedAtUtc`,
  `providerReference`, `replayed: false` and the accepted files (ordinal,
  file name, SHA-256, duplicate flag), the moment the submission is durably
  received and before any processing. A replay of the same key with the
  same body is 200 with the same receipt and `replayed: true`. The same key
  with a different body is 409 and retains nothing new.
- **Validation.** A malformed or out-of-bounds field is 400, naming the
  field. Only the identity-critical fields withhold a reference: claimant
  name, claim number and vehicle registration. Ordinary detail missing from
  a declaration leaves the Case `Not ready`, exactly as for an email.
- **Existing-Case rejection.** The Case-match policy runs on the declared
  claim number, vehicle registration, claimant and incident date. A unique
  or ambiguous existing-Case match fails with
  `provider_existing_case_match`; Pegasus allocates no Case or PO and
  neither links material to nor changes an existing Case. With no match, the
  submission follows the ordinary creation path. Provider updates are a
  separate deferred capability.
- **Pause.** A paused credential is refused for submission before Pegasus
  reads the request body (403, recorded), but can still read its own
  receipts and results. A revoked credential is refused everywhere.
- **Result.** `GET /api/provider/v1/submissions/{id}` returns the
  submission's `status` (`Received`, `Processing`, `Complete`, `Failed`, the
  intake work vocabulary), the intake `decision`, `allocationFailure` and
  `failureCode`, and the `caseReference` once processing allocated a
  Case/PO. A submission that does not exist, or belongs to another
  Principal, is 404; the two are indistinguishable.
- **Fail closed.** A source with no retained submission binding is kept for
  sorting rather than allocated. A custody failure is 503 and the caller
  retries with the same key.

### Accepted principal email routes and automatic association

**Fifteen routes.** The fifteen existing instruction profiles are active
through the ordinary email path. Provider API credentials and mailbox
onboarding are separate capabilities.

`PrincipalMailRoutePolicy.AcceptedIdentities` is the single runtime catalogue
of evidenced exact domains and mailboxes. Principal Settings displays it. It
covers ALS, AX, BC, BLACK, DFD, FW, KBS, MP, OAK, PCH, QCL, QDOS, RJS, SBL
and YML. YML accepts only its evidenced mailbox, never the shared Gmail
domain. HDUK-branded instructions in the supplied YML samples belong to the
confirmed YML route; HDUK is recorded as document issuer, separately from
the instructing Principal. Branding never creates a Principal or widens an
accepted sender. No suffix or subdomain widening is allowed.

**One sender.** Exactly one consistent transport sender is required. A
Collision Engineers staff forward must also prove one external original
sender. PCH mail from the evidenced Connexus or Ensurance intermediary needs
one agreeing PCH instruction profile; an intermediary is not a direct PCH
identity. An accepted sender on its own classifies and allocates nothing.

**One document.** A profile is chosen from its signals within one physical
current document, not assembled across attachments. Separate reports cannot
disqualify an instruction. A proved forwarded original stays current;
arbitrary nested messages and quoted history do not. A conflicting or
ambiguous profile fails closed. Automatic instruction extraction for a
non-QDOS Principal needs one profile that agrees with the sender. Existing
QDOS route-bound body and Triage shapes stay accepted when no competing
profile matches.

**Classification.** The one `PrincipalMailClassificationPolicy`
implementation is bound to each extraction registration. QDOS keeps its
generated Inspection, Audit, combined and Triage tells (below). Other
profiles need the evidenced explicit inspect or examine request, or the
DFD/FW instruction template. PCH's explicit Audit request beats its generic
inspection footer; a separate credit-repair inspection request stays
distinct. Replies, unknown work and competing work types cannot allocate a
new Case. Missing ordinary fields do not withhold an otherwise definitive
Case's reference.

**Case matching.** `PrincipalCaseMatchPolicy` is bound the same way. Its
non-QDOS keys come from the profile's typed role-labelled fields and keep the
full Principal reference, including Fairway's `-01` suffix. QDOS alone uses
its settled claim-tail grammar. A field with conflicting values withdraws its
key. Reads and Case-index writes share one normalisation.

**The QDOS grammar and the shared association rules.**

1. **Route identity.** QDOS direct sender identity keeps its three accepted
   domains in the shared catalogue (`principal_mail_route` v1).
2. **QDOS match keys** (`principal_case_match` v1). Extracted only from
   labelled fields with a required separator, never from free text: the
   claim reference normalised to its durable token (the `NNNNN/N` tail for
   `qdosassist` references, full or bare; the letters grammar for `qdoslaw`
   references); the client-vehicle registration compacted to `[A-Z0-9]`
   (TP-prefixed labels are never harvested); and the claimant name as
   title-stripped surname plus first initial. Several distinct values for
   one key withdraw that key. The incident date (labelled fields plus the
   generated subject `on DD/MM/YYYY`) is never a positive key.
3. **Shared eliminator.** Candidates are every Case for the established
   Principal that matches any key, in every lifecycle state. A candidate is
   eliminated if the message's incident date, or another identity key
   present on both sides, contradicts it. Exactly one survivor is an
   automatic association. Zero is no match, and an instruction proceeds to
   the normal creation gates. Several survivors fail closed as the recorded
   Ambiguous outcome, forcing `Unidentified` with the competing candidates
   visible. A `Created in error` survivor redirects to its linked
   replacement Case and is never associated itself. `NoKeys` stays distinct
   from `NoMatch`. There is no numeric confidence score, threshold or
   display anywhere.
4. **Recording and reversal.** Every evaluation stores a decision record
   (keys, per-candidate hits and eliminations with reasons, outcome, policy
   key and version), one per intake receipt. An automatic association is
   written once by the system-worker identity with the match policy
   stamped, does nothing when any active association exists, and staff can
   reverse it through the ordinary unlink with full history.

The Core policies are versioned in code. The derived match index is kept in
the same transaction by every existing Case-data writer through one shared
projector. There is no new rule engine, rule table, admin editor or parallel
matcher. A new identity needs genuine evidence and an agreeing supported
extraction profile, not a guessed company domain.

### Accepted QDOS automatic Triage predicates

Triage behaviour is owned by
[FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence). This
section records which tells are accepted and what they may not do.

QDOS sends Triage requests in two disjoint generated templates. Both are
tells of the same one category (`principal_mail_classification` v1): the
body phrase `Triage Only Request`, and a subject opening with
`Engineer Triage` after any forward or reply prefix. Both are matched
case-exactly, because the casing is part of the generated tell; a human
sentence mentioning either is not the tell. Two tells feed one Triage
candidate. A second candidate for one category would resolve to the
Ambiguous outcome, so a message carrying both would classify worse than one
carrying either.

The classification decision is itself the Triage-match evidence, stamped with
that policy's key and version. There is no separate Triage matcher.
Message-type classification has one route-owned owner
([ADR-0008](../adr/0008-separate-direct-provider-and-intermediary-email-policies.md)),
and FRD-03 names that owner as what begins a Triage. Exclusions and outcomes
are the classification policy's own: more than one matching category is the
recorded Ambiguous outcome and opens no Triage, and no numeric confidence
score or threshold exists here either.

The registration that decides FRD-03's branch is read by the ordinary
label-anchored extraction: from the letter's `Registration:` line in the
body template, and from the subject's `Vehicle Registration` label in the
subject template, which states it nowhere else.

### Triage result contract

A Provider API Triage submission returns the same result shape, with the same
Principal-scoped access, as a regular Case submission, using the Triage `T-`
reference in place of a Case/PO. It does not allocate a formal Case just to
fill that result. Receipt and processing state keep their ordinary meaning.
A result is not proof that a response was emailed
([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)).

### Non-overlapping route rules

Accepted route predicates must be mutually exclusive for their intended
input. A concrete audit request is not a generic footer match; the predicates
make that distinction, not a precedence score. Staff are never asked to
choose a winning rule. An unexpected overlap fails closed with visible
evidence. It is a defect to fix, not a supported ambiguous mode and not
permission to guess.

## States and transitions

| Thing | States |
| --- | --- |
| Provider API submission | `Received`, `Processing`, `Complete`, `Failed`; a replay returns the same receipt |
| Provider credential | active, paused (read-only on its own receipts), revoked (refused everywhere) |
| Automatic association | one survivor associates; zero proceeds to creation; several is the Ambiguous outcome and goes to Unidentified |
| Route outcome | allocated Case, Triage opened, Unidentified with a reason, or retryable processing failure |

## Edge cases and fail-closed behaviour

- Wrong or missing credential: 401, recorded. Body `principal` mismatch:
  403, recorded.
- Same idempotency key with a different body: 409, nothing retained.
- Envelope over the limits: 413.
- Existing-Case match, unique or ambiguous: `provider_existing_case_match`,
  nothing allocated or changed.
- Two matching classification categories: Ambiguous, no Triage, no Case.
- Overlapping route predicates: fail closed with evidence.
- No retained submission binding: kept for sorting, not allocated.

## Acceptance evidence

Core tests cover the submission contract (credential, replay, validation,
existing-Case rejection), the fifteen-route catalogue, the QDOS match keys
and eliminator, and the two Triage tells. Integration tests cover the
Provider API over real HTTP with the feature gate on and off. Live activation
for a named provider needs exact-target approval and its own evidence tier
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `API-01`–`API-04`, `INT-04` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-03](frd-03-triage.md),
  [FRD-08](frd-08-email-mailbox-and-background-processing.md),
  [FRD-18](frd-18-manual-upload-and-upload-links.md).
- Technical constraints:
  [ADR-0004](../adr/0004-provider-api-and-staff-mcp-authentication.md),
  [ADR-0008](../adr/0008-separate-direct-provider-and-intermediary-email-policies.md),
  [ADR-0020](../adr/0020-accepted-qdos-case-association-predicates.md).
