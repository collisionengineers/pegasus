# FRD-09: Provider and intermediary routes
> Owner capabilities: API (provider/intermediary routes) · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design.md

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

### Accepted QDOS automatic case-association predicates

> Owner capability: route association (QDOS direct). Relocated from ADR-0020 (2026-08-03). Instantiates the route-policy association frame for the QDOS direct route and supersedes the earlier single-domain QDOS sender identity with the operator-accepted three-domain set. General multi-rule precedence and confidence questions remain open in [open decisions](../open-decisions.md#mailbox-rule-activation-automatic-matching-and-confidence-display).

For mail on the accepted QDOS direct route only:

1. **Route identity.** QDOS direct sender identity is exact whole-domain equality against `qdosassist.co.uk`, `qdoslaw.co.uk`, or `qdosassists.co.uk` (`qdos_mail_route` v3) — no suffix or subdomain widening. An accepted domain alone still classifies nothing and associates nothing.
2. **Match keys** (`qdos_case_match` v1), extracted label-anchored with a required separator, never scraped from free text: the claim reference normalized to its durable token (the `NNNNN/N` tail for `qdosassist` references, full or bare; the letters grammar for `qdoslaw` references), the client-vehicle registration compacted to `[A-Z0-9]` (TP-prefixed labels are never harvested), and the claimant name as title-stripped surname plus first initial. Multiple distinct values for one key withdraw that key. The incident date (labelled fields plus the generated subject `on DD/MM/YYYY`) is never a positive key.
3. **Eliminator procedure.** Candidates are every QDOS case matching ANY key, in every lifecycle state (the operator confirmed staff do not archive; a post-report case is post-report stage). A candidate contradicted by the message's incident date or by another identity key present on both sides is eliminated. Exactly one survivor is an automatic association; zero is no match (instructions proceed to the normal creation gates); several fail closed as the recorded Ambiguous outcome, forcing `Needs sorting` with the competing candidates visible. A `Created in error` survivor redirects to its linked replacement case and is never associated itself. `NoKeys` remains distinguishable from `NoMatch`. No numeric confidence score, threshold, or display exists anywhere.
4. **Recording and reversal.** Every evaluation persists a decision record (keys, per-candidate hits and eliminations with reasons, outcome, policy key and version) one-to-one with the intake receipt. An automatic association is written idempotently by the system-worker identity with the match policy stamped, no-ops when any active association exists, and is reversible through the ordinary staff unlink with full history.

This pulls the QDOS-direct subset of MAIL-09 forward to `Now / 0.1.0-alpha.1`. General multi-provider association, the classified-email workspace, and every other route's matchers remain allocated `Next / 0.3.0`.

Consequences: the predicates are Core-owned, code-versioned policy (`QdosCaseMatchPolicy`, the shared eliminator in `EvaluateIntakeCaseMatch`); a behaviour change is a version bump, never a silent redefinition, and any normalization change requires an explicit rebuild of the derived match index. The match index is a read model of accepted case data maintained in the same transaction by every case-data writer — case acceptance, staff case-data save, vehicle-suggestion confirmation, and Created in error replacement creation — all through one shared projector. The predecessor's false-registration shapes (`AND2`, `OCTOBER`, postcode outward codes, `X5 NOW`) are pinned as negative tests. No generic rule engine, rule table, or admin editor is introduced; a second provider's matcher needs its own operator-accepted predicates and policy.
