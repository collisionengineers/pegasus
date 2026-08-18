---
id: ADR-0027
status: accepted
date: 2026-08-18
supersedes: []
superseded_by: []
related_capabilities: [MCP-01, MCP-02, MCP-03, MCP-04, MCP-06]
related_frd: [frd-10]
tags: [mcp, automation, oauth]
---

# ADR-0027: Authorization code with PKCE for external MCP connectors

## Status

Accepted. Adds a second grant to the Automation MCP authorization server
decided by ADR-0011/ADR-0021 and enabled in production by ADR-0026; it changes
neither the actor boundary nor the tool inventory.

## Context

External MCP clients (the Claude.ai remote connector observed on 2026-08-18,
and MCP clients generally) obtain tokens by the OAuth 2.1 authorization-code
flow with PKCE, sending the user's browser to `/authorize`. The Pegasus
server issued client-credentials tokens only, so such connectors could not
connect although the endpoint, client registration and secret were correct.

## Decision

The single seeded Automation client may also use authorization code + PKCE
and refresh tokens, alongside client credentials, when — and only when — an
administrator has configured at least one exact redirect URI
(`AutomationMcp:RedirectUris`, rendered from Bicep). The authorization
endpoint is a Pegasus Administrator consent page: a signed-in Administrator
holding the manage-automation-clients right sees the connector's origin and
the requested scopes and approves or refuses; the decision is permanent
history. An approved code is issued for the **Automation Actor** principal
(subject = client id, granted scopes, MCP audience), never for the staff
member, so tokens from every grant are indistinguishable to the actor
resolver, the kill switch, the rate limit and the tools. Refresh tokens carry
`offline_access` for the connector's convenience and die with the client
registration or a Web restart (ephemeral keys). Dynamic client registration
and per-staff MCP tokens remain excluded.

## Consequences

- A connector needs its exact redirect URI registered by configuration; an
  unregistered URI, a missing PKCE challenge or a disabled client is refused
  before consent.
- Consent requires an interactive Administrator sign-in; with the strict
  same-site staff cookie the Administrator signs in once per authorisation.
- Client-credentials tokens keep their previous shape; nothing changes for
  scripted callers.
- The consent page and the connector flow are covered by the integration
  suite (`AutomationConnectorAuthorizationTests`).

## Links

- [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md)
- [ADR-0011](0011-restrict-mcp-to-automation-actor.md), [ADR-0021](0021-automation-actor-direct-write-assessment-contract.md), [ADR-0026](0026-enable-automation-mcp-by-explicit-deployment-configuration.md)
