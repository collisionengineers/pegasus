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
equally definitive email instruction.

**Source limitation:** the accepted sources do not define an external tenant
model, exact routes, headers, schema, attachment encoding, request limits,
throttling policy, administration UI, or a Pegasus identity/field named
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
> API credential, delivered with the submission endpoint. Live activation for
> a named provider still requires exact-target approval before any credential
> is issued.

The surface is a versioned machine surface, composed only where the
`Features:ProviderApi` gate is on and otherwise absent (404). It accepts no
cookie and no staff identity; a Principal credential is accepted nowhere
else.

- **Credential.** `Authorization: Bearer pgs_<key id>_<secret>` — the secret
  API-04 issued once. Unknown key, wrong secret, revoked credential, or
  inactive Principal is refused as 401 with a recorded security event that
  names the key id when one was well-formed and never the secret. Requests
  are rate-limited per key id.
- **Submit.** `POST /api/provider/v1/submissions` as `multipart/form-data`:
  one or more `files` parts in instruction order (the first is the
  instruction; later parts are its attachments or images), an optional
  `providerReference` field (at most 200 characters), and a required
  `Idempotency-Key` header (at most 200 characters, unique per Principal).
  The envelope bound is the staff Upload bound: at most 20 files, each at
  most 10 MiB; a larger envelope is 413. The files enter the same grouped
  durable intake path as a staff upload, on the `provider_api` source
  channel, bound to the authenticated Principal — the content and any
  sender inside it never select a route or a different Principal — and
  attributed to the Provider actor (that Principal) in action history.
- **Receipt.** 201 with `submissionId`, `receivedAtUtc`, `providerReference`,
  `replayed: false` and the accepted files (ordinal, file name, SHA-256,
  duplicate flag) the moment the envelope is durably received, before any
  processing. A replay of the same key with identical files is 200 with the
  same receipt and `replayed: true`; the same key with different files or a
  different file count is 409 and retains nothing new.
- **Pause.** A paused credential is refused for submission (403, recorded)
  and still reads its own receipts and results; a revoked one is refused
  everywhere.
- **Result.** `GET /api/provider/v1/submissions/{id}` returns the
  submission's `status` (`Received`, `Processing`, `Complete`, `Failed` — the
  intake work vocabulary, not a provider-only one), the `caseReference` once
  processing allocated a Case/PO, and per file the intake `decision`,
  `allocationFailure`, `failureCode` and `caseReference`. A submission that
  does not exist or belongs to another Principal is 404 — the two are
  indistinguishable.
- **Fail closed.** A Principal without an instruction extraction policy, or
  a source with no retained submission binding, is retained for sorting
  rather than allocated. Custody failure is 503 and the caller retries with
  the same key.

### Accepted QDOS automatic case-association predicates

> Owner capability: route association (QDOS direct). Relocated from ADR-0020 (2026-08-03). Instantiates the route-policy association frame for the QDOS direct route and supersedes the earlier single-domain QDOS sender identity with the operator-accepted three-domain set. General multi-rule precedence and confidence questions remain open in [open decisions](../open-decisions.md#mailbox-rule-activation-automatic-matching-and-confidence-display).

For mail on the accepted QDOS direct route only:

1. **Route identity.** QDOS direct sender identity is exact whole-domain equality against `qdosassist.co.uk`, `qdoslaw.co.uk`, or `qdosassists.co.uk` (`qdos_mail_route` v3) — no suffix or subdomain widening. An accepted domain alone still classifies nothing and associates nothing.
2. **Match keys** (`qdos_case_match` v1), extracted label-anchored with a required separator, never scraped from free text: the claim reference normalized to its durable token (the `NNNNN/N` tail for `qdosassist` references, full or bare; the letters grammar for `qdoslaw` references), the client-vehicle registration compacted to `[A-Z0-9]` (TP-prefixed labels are never harvested), and the claimant name as title-stripped surname plus first initial. Multiple distinct values for one key withdraw that key. The incident date (labelled fields plus the generated subject `on DD/MM/YYYY`) is never a positive key.
3. **Eliminator procedure.** Candidates are every QDOS case matching ANY key, in every lifecycle state (the operator confirmed staff do not archive; a post-report case is post-report stage). A candidate contradicted by the message's incident date or by another identity key present on both sides is eliminated. Exactly one survivor is an automatic association; zero is no match (instructions proceed to the normal creation gates); several fail closed as the recorded Ambiguous outcome, forcing `Needs sorting` with the competing candidates visible. A `Created in error` survivor redirects to its linked replacement case and is never associated itself. `NoKeys` remains distinguishable from `NoMatch`. No numeric confidence score, threshold, or display exists anywhere.
4. **Recording and reversal.** Every evaluation persists a decision record (keys, per-candidate hits and eliminations with reasons, outcome, policy key and version) one-to-one with the intake receipt. An automatic association is written idempotently by the system-worker identity with the match policy stamped, no-ops when any active association exists, and is reversible through the ordinary staff unlink with full history.

This pulls the QDOS-direct subset of MAIL-09 forward to `Now / 0.1.0-alpha.1`. General multi-provider association, the classified-email workspace, and every other route's matchers remain allocated `Next / 0.3.0`.

Consequences: the predicates are Core-owned, code-versioned policy (`QdosCaseMatchPolicy`, the shared eliminator in `EvaluateIntakeCaseMatch`); a behaviour change is a version bump, never a silent redefinition, and any normalization change requires an explicit rebuild of the derived match index. The match index is a read model of accepted case data maintained in the same transaction by every case-data writer — case acceptance, staff case-data save, vehicle-suggestion confirmation, and Created in error replacement creation — all through one shared projector. The predecessor's false-registration shapes (`AND2`, `OCTOBER`, postcode outward codes, `X5 NOW`) are pinned as negative tests. No generic rule engine, rule table, or admin editor is introduced; a second provider's matcher needs its own operator-accepted predicates and policy.

### Accepted QDOS automatic Triage predicates

> Owner capability: TRI-01/TRI-02 (QDOS direct). Operator decision 2026-08-23 (INTK-033). Behaviour is owned by [FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence); this records which predicates were accepted and what they may not do.

QDOS sends Triage requests in two disjoint generated templates, and both are accepted
tells of the same one category (`qdos_mail_classification` v4): the body phrase
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
