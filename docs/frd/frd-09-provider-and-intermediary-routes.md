# FRD-09: Provider and intermediary routes
> Owner capabilities: API (provider/intermediary routes) · Migrated from docs/requirements.md · UI behaviour: docs/design.md

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
