# Accepted finding: provider API intake already covered by current plans

**Operator decision:** Classified as dealt with on 2026-07-24. ADR-0020's architectural finding is accepted as already covered by current Pegasus requirements, architecture and delivery plans.

**Legacy sources dealt with:** ADR-0020 (`../dealt-with/accepted/0020-provider-api-intake/docs/adr/0020-provider-api-intake-channel.md`) and its direct provider API bundle (`../dealt-with/accepted/0020-provider-api-intake/README.md`).

This classification does not adopt the predecessor wire contract or establish a current implementation.

## Accepted finding

- A machine-to-machine provider channel may submit structured instructions and attachments without creating a second case model or policy engine.
- Provider credentials are separately issued, principal-scoped opaque secrets. Only secret hashes are stored, with rotation and revocation supported.
- Submissions are idempotent and expose only that principal's own receipt, processing status and resulting Case/PO.
- Provider operations use the same Core intake and authorization policies as staff and Worker callers and record the provider client as the action actor in permanent action history.
- The provider API remains separate from the internal staff MCP surface and does not grant general case reads or workflow mutation in the `0.1.0-alpha.1`.

## Difference from the predecessor material

Legacy ADR-0020 contains the same high-level channel and shared-policy direction, but current `Next`/`unallocated` deliberately withholds details that the predecessor fixed:

- The legacy provider contract (`../dealt-with/accepted/0020-provider-api-intake/docs/reference/provider-api-intake-spec.md`) selects `POST /api/provider-intake/cases`, `X-Api-Key`, an `Idempotency-Key` header, Base64-in-JSON attachments, a 50 MB request limit, exact fields and error codes. None of those wire choices or limits is currently accepted for `Next`/`unallocated`.
- The predecessor contract creates a case immediately and returns `201 { caseId, casePo }`. Current `Next`/`unallocated` defines submission receipt, processing status and resulting Case/PO retrieval; it does not yet approve immediate case creation as the provider response contract.
- Legacy ADR-0020 specifies salted/peppered credential verification and rate limiting. Current `Next`/`unallocated` accepts hash-only opaque-secret storage, rotation/revocation and bounded failures, but leaves the hashing construction, authentication scheme and throttling policy to the later accepted contract and implementation slice.
- The predecessor TKT-055 (`../dealt-with/accepted/0020-provider-api-intake/docs/tickets/verify/TKT-055-provider-api-intake/TKT-055-provider-api-intake.md`) describes a TypeScript Data API, database tables, Azure Functions, Blob writes and an Admin key-management UI. Those are old-project implementation records, not `Next`/`unallocated` architecture or delivery evidence.

## Current architecture, plan and evidence state

The settled [questionnaire](../../history/product/project-discovery-questionnaire.md) and [remaining requirements](../../product/qdos-alpha-gap.md) already require the principal-scoped, idempotent provider submission and own-result boundary.

Current [ADR-0004](../../architecture/decisions/ADR-0004-provider-api-and-staff-mcp-authentication.md) owns the provider authentication and MCP separation decision. The [provider submissions delivery plan](../../history/plans/remainder-delivery/integrations/provider-submissions.md) assigns business policy to shared Core intake and principal authorization, with Web translating a later accepted HTTP contract.

The evidence state remains **Planned**. There is no current provider endpoint, credential store, registered caller, live provider client or proven `Next`/`unallocated` submission. The Development-only `/Intake/Upload` path is not a provider API, and predecessor deployment evidence does not change current status.

## Deferred-capability impact

The later versioned contract must retain stable principal identity, immutable source identity, principal-plus-request idempotency, original evidence provenance and a separate provider action actor. It must prove revoked/invalid credentials, replay conflicts and cross-principal access are rejected before accepting a source.

Exact routes, headers, schemas, file/request limits, credential administration, throttling, provider clients, rollout and live enablement remain unbuilt. They activate only after operator acceptance of the contract and administration workflow, named clients and environment, followed by separately approved implementation and live proof.
