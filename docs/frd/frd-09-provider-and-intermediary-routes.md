# FRD-09: Provider and intermediary routes

## Unidentified route outcome

An unresolved provider/intermediary route is Unidentified only when the received
material itself is safely retained but no unique owner or destination can be
established. The route evidence and bounded reason are preserved under one U
reference; a reasoned policy refusal remains Blocked intake and a retryable technical
failure remains processing.
> Owner capabilities: API (provider/intermediary routes) · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Provider and intermediary routes

Provider identity, intermediary identity, route identity, and
provider/domain-suffix association are separate facts. The versioned
provider/domain package is evidence and configuration input; package presence
does not activate a route, choose a principal, or define an API client.

Direct-provider and intermediary policies may differ, but both call the same
Core intake contract and fail closed when route identity, enabled policy,
principal, or mandatory evidence is missing. The [capability
inventory](../capabilities.md) owns the exact targets for additional-provider
routes and provider APIs.

### Provider API principal and contract boundary

The accepted provider-API security boundary is the stable Pegasus principal,
not an email domain or general external tenant. A provider client receives a
separately issued principal-scoped client ID and opaque secret; only the secret
hash is stored, and rotation and revocation are supported. The client may
submit instructions/attachments idempotently and retrieve only that
principal's own receipt, processing status, and resulting Case/PO. It receives
no staff access, general case search/read, or case-workflow mutation.

Provider operations use the same Core intake and authorization policies as Web
and Worker callers. Receipt, submission, status, result, source-custody, and
idempotency identities remain distinct per principal, and the provider client
is the attributable action actor. Cross-principal query or result disclosure
fails closed. The transport channel alone never changes extraction, instruction
eligibility, or automatic allocation: a definitive provider-API instruction
for its authenticated principal follows the same case-creation path as an
equally definitive email instruction. API-01 is create-only: it never associates
material with or mutates an existing Case.

**Additional-contract boundary:** API-01 below owns the accepted current routes,
schemas, limits and Principal credential contract. It does not establish an
additional external tenancy model or a Pegasus identity/field named
`provider_domain_key`. No allowed source proves an owner or current/predecessor
consumer for that name. Pegasus therefore does not create, migrate, map, alias,
or retire it. Any later proposal must first establish authoritative source and
consumer evidence, stable-principal/route/provenance mapping, collision and
unknown handling, cutover, rollback, retention, and explicit retirement proof
through the separate [open
decision](../open-decisions.md#external-data-submission-and-report-contracts);
none may be inferred from provider-domain evidence.

No provider route is active until its exact capability allocation, accepted
contract, credentials/scopes, failure and recovery proof, real caller, and
operator acceptance exist.

### Accepted API-01 submission contract

> Owner capability: API-01 (with API-04 credentials). Operator decision
> 2026-08-28 (EPIC-011 D8): the Principal's Pegasus API key is the Provider
> API credential, delivered with the submission endpoint. The same day, the
> operator replaced the document-only request shape with the declared
> instruction below. Live activation for a named provider still requires
> exact-target approval before any credential is issued.

The surface is a versioned machine surface, composed only where the
`Features:ProviderApi` gate is on and otherwise absent (404). It accepts no
cookie and no staff identity; a Principal credential is accepted nowhere
else.

**The provider states its instruction; Pegasus does not read it back out of a
document.** The first drafted contract took files only and relied on the
Principal's extraction policy to recover the business values. That policy
recognises QDOS, which arrives by e-mail, so the route could not create a case
for the providers it exists for and had no caller. A provider integrating over
HTTP already holds the fields, and states them.

- **Credential.** `Authorization: Bearer pgs_<key id>_<secret>` — the secret
  API-04 issued once. Unknown key, wrong secret, revoked credential, or
  inactive Principal is refused as 401 with a recorded security event that
  names the key id when one was well-formed and never the secret. Requests
  are rate-limited per calling address: the limiter runs before
  authentication, so a presented key id is a claim, not an identity, and
  partitioning on it would let a caller spend another provider's budget or
  mint itself a fresh one per request.
- **Submit.** `POST /api/provider/v1/submissions` as `application/json`, with a
  required `Idempotency-Key` header (at most 200 characters, unique per
  Principal). The body declares the instruction and carries its files inline as
  base64.
- **Principal.** The credential establishes it. A `principal` in the body is
  compared with it and a mismatch is refused (403, recorded); the field exists
  to catch a provider posting to the wrong account and never to select one.
- **Case type.** One of `inspection`, `audit`, `auditreport` or `triage`,
  mapping to `Inspection`, `Audit` and `InspectionAndAudit`; `triage` allocates
  no Case/PO and opens a Triage instead (see FRD-03).
- **Audit.** A standalone `audit` states `originalReportVerdict`
  (`repairable` or `total-loss`) and attaches the original report with its role
  stated. The declared verdict derives the `a.`/`ap.` reference (see
  [FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
  `auditreport` is Collision Engineers auditing its own report and carries
  neither.
- **Files.** One or more, each with a leaf `fileName`, a `mediaType` the intake
  reader supports, and base64 `contentBase64`. An optional `role`
  (`instruction`, `originalreport`, `image`, `correspondence`, `other`) says
  what the file is; absent, nothing is inferred and the file is retained as an
  ordinary attachment. At most 20 files, each at most 10 MiB, at most 30 MiB
  decoded in total and 42 MiB of request body; a larger envelope is 413.
- **Retention.** One submission is one intake receipt. The retained source is
  the request exactly as it arrived, and the submitted files are that receipt's
  attachments — the shape an e-mail instruction already has, which is what lets
  an Audit find its original report among its own evidence. The receipt enters
  the same durable intake path as a staff upload, on the `provider_api` source
  channel, bound to the authenticated Principal, and the submission is the
  attributable action actor in permanent history. If process loss separates
  the accept writes, the existing reconciliation timer repairs the staged-
  receipt link and initial `Accepted` history row once intake retention exists.
  A submission is accepted once: whichever of the request and the repair
  records that acceptance first is the one row, and a repaired row states the
  time the submission was received and says that it was completed by
  recovery.
- **Provenance.** Every declared value is written to the case as its own
  provenance — provider API, distinct from extraction and from staff entry —
  and is visible as such on the case. The Work Provider — the Principal — is
  recorded on the case from the authenticated submission binding with
  provider-API provenance.
- **Receipt.** 201 with `submissionId`, `receivedAtUtc`, `providerReference`,
  `replayed: false` and the accepted files (ordinal, file name, SHA-256,
  duplicate flag) the moment the submission is durably received, before any
  processing. A replay of the same key with the same body is 200 with the same
  receipt and `replayed: true`; the same key with a different body is 409 and
  retains nothing new.
- **Validation.** A malformed or out-of-bounds field is 400 naming the field
  that failed. The identity-critical fields — claimant name, claim number and
  vehicle registration — are the only ones that withhold a reference; ordinary
  detail missing from a declaration leaves the case `Not ready`, exactly as it
  does for an e-mail.
- **Existing-Case rejection.** The existing Case-match policy is applied to the
  declared claim number, vehicle registration, claimant and incident date. A
  unique or ambiguous existing-Case match fails with
  `provider_existing_case_match`; Pegasus allocates no Case or PO and neither
  associates material with nor mutates an existing Case. With no match, the
  submission follows the ordinary creation path. Provider updates remain a
  separate deferred capability under AUTO-017.
- **Pause.** A paused credential is refused for submission before Pegasus reads
  the request body (403, recorded) and still reads its own receipts and results;
  a revoked one is refused everywhere.
- **Result.** `GET /api/provider/v1/submissions/{id}` returns the submission's
  `status` (`Received`, `Processing`, `Complete`, `Failed` — the intake work
  vocabulary, not a provider-only one), the intake `decision`,
  `allocationFailure` and `failureCode`, and the `caseReference` once
  processing allocated a Case/PO. A submission that does not exist or belongs
  to another Principal is 404 — the two are indistinguishable.
- **Fail closed.** A source with no retained submission binding is retained for
  sorting rather than allocated. Custody failure is 503 and the caller retries
  with the same key.

### Accepted QDOS automatic case-association predicates

> Owner capability: route association (QDOS direct). Relocated from ADR-0020 (2026-08-03). Instantiates the route-policy association frame for the QDOS direct route and supersedes the earlier single-domain QDOS sender identity with the operator-accepted three-domain set. General multi-rule precedence and confidence questions remain open in [open decisions](../open-decisions.md#mailbox-rule-activation-automatic-matching-and-confidence-display).

For mail on the accepted QDOS direct route only:

1. **Route identity.** QDOS direct sender identity is exact whole-domain equality against `qdosassist.co.uk`, `qdoslaw.co.uk`, or `qdosassists.co.uk` (`qdos_mail_route` v4) — no suffix or subdomain widening. An accepted domain alone still classifies nothing and associates nothing.
2. **Match keys** (`qdos_case_match` v1), extracted label-anchored with a required separator, never scraped from free text: the claim reference normalized to its durable token (the `NNNNN/N` tail for `qdosassist` references, full or bare; the letters grammar for `qdoslaw` references), the client-vehicle registration compacted to `[A-Z0-9]` (TP-prefixed labels are never harvested), and the claimant name as title-stripped surname plus first initial. Multiple distinct values for one key withdraw that key. The incident date (labelled fields plus the generated subject `on DD/MM/YYYY`) is never a positive key.
3. **Eliminator procedure.** Candidates are every QDOS case matching ANY key, in every lifecycle state (the operator confirmed staff do not archive; a post-report case is post-report stage). A candidate contradicted by the message's incident date or by another identity key present on both sides is eliminated. Exactly one survivor is an automatic association; zero is no match (instructions proceed to the normal creation gates); several fail closed as the recorded Ambiguous outcome, forcing `Unidentified` with the competing candidates visible. A `Created in error` survivor redirects to its linked replacement case and is never associated itself. `NoKeys` remains distinguishable from `NoMatch`. No numeric confidence score, threshold, or display exists anywhere.
4. **Recording and reversal.** Every evaluation persists a decision record (keys, per-candidate hits and eliminations with reasons, outcome, policy key and version) one-to-one with the intake receipt. An automatic association is written idempotently by the system-worker identity with the match policy stamped, no-ops when any active association exists, and is reversible through the ordinary staff unlink with full history.

This pulls the QDOS-direct subset of MAIL-09 forward to `Now / 0.1.0-alpha.1`. General multi-provider association, the classified-email workspace, and every other route's matchers remain allocated `Next / 0.3.0`.

Consequences: the predicates are Core-owned, code-versioned policy (`QdosCaseMatchPolicy`, the shared eliminator in `EvaluateIntakeCaseMatch`); a behaviour change is a version bump, never a silent redefinition, and any normalization change requires an explicit rebuild of the derived match index. The match index is a read model of accepted case data maintained in the same transaction by every case-data writer — case acceptance, staff case-data save, vehicle-suggestion confirmation, and Created in error replacement creation — all through one shared projector. The predecessor's false-registration shapes (`AND2`, `OCTOBER`, postcode outward codes, `X5 NOW`) are pinned as negative tests. No generic rule engine, rule table, or admin editor is introduced; a second provider's matcher needs its own operator-accepted predicates and policy.

### Accepted QDOS automatic Triage predicates

> Owner capability: TRI-01/TRI-02 (QDOS direct). Operator decision 2026-08-23 (INTK-033). Behaviour is owned by [FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence); this records which predicates were accepted and what they may not do.

QDOS sends Triage requests in two disjoint generated templates, and both are accepted
tells of the same one category (`qdos_mail_classification` v5): the body phrase
`Triage Only Request`, and a subject opening with `Engineer Triage` past any forward or
reply prefix. Both are matched case-exactly, because the casing is part of the generated
tell — a human sentence mentioning either is not the tell. Two tells feed **one** triage
candidate; a second candidate for one category would resolve to the Ambiguous outcome, so
a message carrying both would classify worse than one carrying either.

The classification decision is itself the accepted Triage-match evidence, stamped with
that policy's key and version. There is no separate Triage matcher: message-type
classification has one route-owned owner (ADR-0008), and FRD-03 names that owner as what
begins a Triage. Exclusions and outcomes are the classification policy's own — more than
one matching category is the recorded Ambiguous outcome and opens no Triage, and no
numeric confidence score or threshold exists here either.

The registration that decides FRD-03's branch is read by the ordinary label-anchored
extraction: from the letter's `Registration:` line in the body template, and from the
subject's `Vehicle Registration` label in the subject template, which states it nowhere
else.
