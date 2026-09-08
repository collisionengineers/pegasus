---
id: ADR-0004
status: accepted
date: 2026-07-23
supersedes: []
superseded_by: [ADR-0011]
related_capabilities: []
related_frd: [frd-09, frd-10]
tags: [auth, api, mcp]
---
# ADR-0004: Provider API and staff MCP authentication

- Status: Accepted for the provider API. Its staff-MCP authentication clauses
  are superseded by [ADR-0011](0011-restrict-mcp-to-automation-actor.md);
  `superseded_by` records that partial relationship.
- Date: 2026-07-23
- Owners: Alex and the Pegasus development team

## Context

ADR-0002 treated provider API and MCP clients as the same principal-scoped
machine integration. The product decisions are now narrower and distinct:

- providers need a machine API for submitting instructions and checking the
  result of their own submissions; and
- MCP is an internal staff surface, primarily for Claude Desktop, whose case
  actions must use the staff member's current application role and permanent
  action-history identity.

Pegasus staff accounts remain application-managed ASP.NET Core Identity
accounts rather than Microsoft Entra accounts. The Web project remains the HTTP,
API, MCP, authentication, and composition boundary; business behaviour remains
in shared Core use cases.

## Decision

### Maturity and activation

The provider API is a `Next`/`unallocated` capability: this ADR fixes its security and contract
boundary but does not claim a `0.1.0-alpha.1` implementation. Staff MCP is a `0.1.0-alpha.1` capability,
limited initially to intake-oriented actions through the same Core use cases.
Categorised-email queues, the broader email workspace, and broader MCP email
actions wait for `Next`/`unallocated`. These allocations are not caller or deployment evidence.

### Provider HTTP API

The provider API uses separately issued principal-scoped client IDs and opaque
secrets. Store only each secret's hash, show the clear value once, and support
rotation and revocation.

The first activated provider-API contract is limited to:

- idempotent instruction and attachment submission; and
- retrieval of that principal's own submission receipt, processing status, and
  resulting Case/PO.

It does not expose general case search/read or case-workflow mutation. Every
operation calls the same Core intake and authorization boundary as the Web and
Worker paths and records the principal client as the action actor in permanent action history.

### Internal staff MCP

Expose MCP as a remote Streamable HTTP surface from `Pegasus.Web`. It is
an internal staff interface, not a provider interface.

Use an OAuth 2.1-compatible authorization-code flow with S256 PKCE. For the first
Claude Desktop custom connector, pre-register the OAuth client rather than
implementing Dynamic Client Registration. Register Anthropic's hosted callback
URI, `https://claude.ai/api/mcp/auth_callback`, and expose the protected-resource
and authorization-server metadata required by the MCP authorization protocol.

Use the canonical HTTPS MCP endpoint as the OAuth resource indicator in both
authorization and token requests. The MCP resource server accepts only tokens
issued by its configured authorization server for that exact resource/audience
and validates issuer, lifetime, signature or introspection result, and granted
scopes. OAuth scopes restrict access to the MCP surface; the current application
role and named authorization policy still decide each tool action. Do not accept
or pass through a token issued for another resource.

Each staff member authorizes the connector using their Pegasus account.
Access tokens identify that staff account; every request checks the account is
still enabled and applies its current Administrator, Engineer, or User role.
Disabling an account or changing its role therefore affects subsequent MCP calls
without issuing a separate MCP identity. Never accept a staff password or staff
browser cookie at the MCP endpoint.

MCP may expose the full case, inbox, and document actions that the signed-in role
can perform through the staff application, including lifecycle transitions. It
must not expose:

- account or role administration;
- principal configuration;
- provider or OAuth credential administration;
- Azure, deployment, or other cloud operations; or
- permanent deletion, which the domain does not permit through any surface.

Each MCP tool calls an existing or simultaneously delivered Core use case and
named authorization policy. Mutating tools have narrow operation-specific
contracts, accurate destructive/idempotent annotations, and permanent actor,
reason, and outcome action events. Client approval hints are not an authorization
boundary; the server enforces every permission and invariant.

## Consequences

- Provider credentials and staff OAuth clients/tokens have separate ownership,
  scopes, administration, and action-history identities.
- Claude Desktop actions can be attributed to the actual Pegasus staff
  account rather than a shared bearer actor.
- The Web application must provide OAuth authorization-server capability in
  addition to ASP.NET Core Identity sign-in. Library selection and endpoint code
  belong to the implementation slice and must preserve this contract.
- Pre-registering the custom connector avoids `0.1.0-alpha.1` DCR/CIMD implementation
  while retaining individual user consent and authorization.
- A shared static request header was rejected because Anthropic documents it as
  beta and organization-shared, which cannot provide the required per-user role
  and action attribution.
- A local MCPB/stdio bridge was rejected because the required case system is an
  internet-hosted application and the bridge would introduce a second client and
  credential boundary.

This ADR selects contracts and authentication boundaries only. It does not claim
that the provider API, OAuth server, or MCP endpoint is implemented, deployed, or
verified.

## Sources

- [Anthropic: Authentication for connectors](https://claude.com/docs/connectors/building/authentication)
- [Anthropic: Third-party connectors with remote MCP](https://claude.com/docs/connectors/custom/remote-mcp)
- [Model Context Protocol: Authorization, 2025-11-25](https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization)
