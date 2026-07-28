# Provider submissions

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Planned `Next`/`unallocated` — not a `0.1.0-alpha.1` release gate**

## Purpose

Preserve the accepted `Next`/`unallocated` principal-scoped provider boundary without inventing the versioned HTTP contract, credential-administration workflow or limits that implementation still requires. This plan is not a `0.1.0-alpha.1` release gate.

## Feature coverage

Primary feature ownership is: `API-01`, `API-02`, `API-03`, and `API-04`.
The three `Next`/`unallocated` delivery slices below are deliberately contract-neutral: this plan
does not choose an authentication scheme, route, header, request shape,
signature, or provider client.

## Authority and current boundary

- **Authority:** [remaining requirements](../../../../product/qdos-alpha-gap.md#3-complete-intake-formats-and-paths) and [ADR-0004](../../../../architecture/decisions/ADR-0004-provider-api-and-staff-mcp-authentication.md#provider-http-api).
- **Policy owner:** shared Core intake and principal-authorisation policy; Web will translate the accepted HTTP contract.
- **Current implementation/callers:** no provider endpoint, credential store, caller or live client exists. The Development `/Intake/Upload` caller is not a provider API.
- **Accepted invariant:** separately issued principal-scoped client IDs and opaque secrets; store only secret hashes; support rotation/revocation; accept idempotent instruction/attachment submission; expose only that principal's submission receipt, status and resulting Case/PO.
- **Not accepted:** HTTP authentication scheme, routes, headers, multipart schema, signature requirement, bounds, throttling policy, credential issuance workflow or live clients.

## Receive principal scoped submissions

**Evidence state:** Planned — `Next`/`unallocated` contract and credential workflow gated

`API-01` is one authenticated, principal-scoped Web/API caller that delegates
to the existing Core intake and principal-authorisation owners. It must retain
the submitted source before processing, use principal-plus-request
idempotency, and fail closed on missing, revoked, contradictory, or
out-of-principal identity before source acceptance or allocation. A definitive
submission reaches the same Core predicate and atomic acceptance transaction
as the authorised Worker path; the adapter must not add a provider-specific
intake or allocation policy.

## Return provider receipt, status and result

**Evidence state:** Planned — `Next`/`unallocated` contract and credential workflow gated

`API-02` and `API-03` expose only the submitting principal's durable receipt,
processing status, and resulting Case/PO where the shared Core outcome permits
it. Cross-principal access, unknown receipt, replay conflict, incomplete
processing, or ambiguous source evidence is denied or returned as the retained
processing outcome without case search, workflow mutation, reference reuse, or
another receipt authority. The intended Web/API caller and Core query owners
are planned; no endpoint or result lookup is current evidence.

## Issue, rotate and revoke provider credentials

**Evidence state:** Planned — `Next`/`unallocated` credential-administration workflow gated

`API-04` requires a named, authorised staff administration caller, Core
principal/authorisation policy, opaque hashed secrets, permanent action
history, bounded recovery for a stale administrator, and immediate refusal of
revoked credentials. It must not put secrets in source, telemetry, or ordinary
receipt data. Exact issuance, recovery, rotation overlap, expiry, request
bounds, and live client procedures remain contract decisions rather than
invented protocol details.

## Withheld outcome

No code implementation is authorised until a versioned contract and credential-administration workflow are accepted. ADR-0004 does not authorise HTTP Basic, an `Idempotency-Key` header, a payload signature or `/api/v1/submissions`; those remain proposals.

The accepted future task must name one Web caller, the shared Core intake/authorisation owner, principal-plus-request idempotency, cross-principal denial before receipt access, hashed-secret rotation/revocation, stale-administrator behavior, permanent action history, bounded request failures, rollout and rollback. An authenticated principal-scoped new instruction automatically enters the same Core definitive predicate and atomic `AcceptCaseDraft` transaction as the Worker; the credential principal is authoritative, any contradictory principal evidence fails closed, and a non-definitive submission returns its retained processing status without allocating a reference. A definitive incomplete instruction creates one `Not ready` case and the provider may retrieve its Case/PO. The endpoint must not implement another acceptance rule or expose general case search/workflow mutation.

## Activation and approval

Activation requires the accepted versioned wire contract, exact file/request limits, credential issuance/recovery procedure, named provider clients and target environment. Live enablement is separately approval-gated and must prove invalid/revoked credentials, replay conflicts and cross-principal access are refused before any source is accepted. Caller evidence must also prove one definitive submission automatically creates one case/reference, replay returns that result, and uncertain or contradictory evidence never calls the allocator.

## Deferred-capability impact

- **Named capabilities:** external accounts, provider portal, wider case APIs and additional principals.
- **Stable seam retained:** principal ID, immutable source identity, shared intake use case and separate provider action-history actor.
- **Future migration/replacement:** the accepted contract will need credential, receipt/idempotency and action-history persistence in the single migration stream.
- **Deliberately absent:** endpoint, authentication handler, credential table, provider client, dormant configuration and live enablement flag.
