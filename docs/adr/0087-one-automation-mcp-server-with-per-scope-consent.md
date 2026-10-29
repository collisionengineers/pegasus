---
id: ADR-0087
status: accepted
date: 2026-10-29
supersedes: [ADR-0080]
superseded_by: []
related_capabilities: [MCP-01, MCP-02, MCP-03, MCP-04, MCP-05, MCP-06]
related_frd: [frd-10]
tags: [mcp, automation, oauth]
---

# ADR-0087: One Automation MCP server with per-scope consent

## Status

Accepted on 29 October 2026 (operator rulings R547 and R548; delivery R549).
Supersedes [ADR-0080](superseded/0080-automation-mcp-one-server-per-scope.md):
its decisions 1 (one server per scope), 4 (a resource, a metadata document
and an endpoint policy per server), 6 (the migration to `/mcp/<scope word>`)
and 7 (no new tool until the split) end. Its decisions 2 (instructions
restate FRD-10's cross-tool rules and make none), 3 (`tools/list` holds only
the tools whose scope the token carries; the list is disclosure and the
resolver is the gate) and 5 (no Administration or read-only server) stand and
are owned here. ADR-0011, ADR-0026, ADR-0027 and ADR-0064 are unchanged.
Raised by issue #1733 from the release-readiness decision E7 (R509).

## Context

Observed at `9a92a20` (`dev`, 29 October 2026). Since Release 123 the
Automation Actor's 69 tools are served by seven servers, one per scope, each
with its own path under `/mcp/`, name, instructions, endpoint policy, RFC 8707
resource and RFC 9728 metadata document listing its one scope; `/mcp` itself
is not served. A client that works a Case end to end adds seven connectors,
and an Administrator consents seven times, each grant expiring after fourteen
days. No record in `docs/operations.md` says the seven connectors were
re-added after Release 123, and the readiness review of 28 October 2026 ruled
"7 connectors was dumb i need to rework this" (R509).

What the split delivered that matters is its Stage 1, which this record
keeps: `tools/list` filtered by the scopes the token carries
(`AutomationToolListing`, reading the one tool-to-scope table
`AutomationToolScopes`). The classifier, holding `automation.mail`, lists its
seven mail tools and not 69. Scope, not the server, was always the authority
boundary: one audience, one client, and `AutomationActorResolver.RequireAsync`
gating every call with its `automation_scope_denied` record. Seven servers
added only a smaller connector for a client holding every scope, at the price
of seven connectors.

How Claude chooses scopes (Anthropic,
[Authentication for connectors](https://claude.com/docs/connectors/building/authentication)):
it requests the `scope` named in the `401`'s `WWW-Authenticate` header, else
every scope the protected-resource metadata lists in `scopes_supported`. With
one server listing seven scopes a claude.ai connector asks for all seven, and
the consent page at `/authorize` approved all of them or refused. ADR-0064
decision 9 gave `automation.send` its own scope "so a grant can hold casework
without sending"; under seven servers that was reached by not adding the send
connector, and with one server it needs the Administrator to be able to
approve fewer scopes than requested.

## Decision

1. **One server.** The Automation Actor's tools are served by one MCP server,
   `pegasus-automation`, at `/mcp`. Every tool class is registered on it. The
   server has one `ServerInstructions` text: the four sentences the operator
   approved as built in Release 123 (paging, the one-command lease, the
   explicit lease tools, native content; R526), joined, restating FRD-10's
   cross-tool rules and making none.
2. **One resource, one document, one policy.** The server's RFC 8707 resource
   is `<origin>/mcp`, the one the client registration permits. Its RFC 9728
   document at `/.well-known/oauth-protected-resource/mcp` lists all seven
   scopes in `scopes_supported`. The endpoint policy requires the shared
   audience and any one scope; the two content routes keep their own policies.
3. **Listing by scope.** `tools/list` holds only the tools whose scope the
   token carries, built once per scope set and protocol version from the one
   tool-to-scope table. The list is disclosure: `RequireAsync` stays the call
   gate, with its refusal and its `automation_scope_denied` record.
4. **Consent per scope.** The Administrator consent page lists each requested
   scope with its description and a tick box, ticked by default. Approving
   issues the code for the Automation Actor principal holding the scopes left
   ticked, and the decision record keeps those scopes. Approving with no scope
   ticked is the refusal the Refuse button records (`access_denied`). A token
   may therefore carry fewer scopes than the connector asked for, as OAuth
   allows (RFC 6749 section 3.3). No new words: the scope descriptions and the
   buttons are unchanged.
5. **No Administration or read-only server.** ADR-0080 decision 5 stands.
6. **Migration.** `/mcp/<scope word>` is replaced, not aliased, in the release
   that ships this record. In the repository: the classifier's
   `PEGASUS_MCP_URL` (`infra/modules/classifier.bicep`, `.env.example`), the
   release skill's connector step and the tests. Outside it: the claude.ai
   connector, which an Administrator adds once at `<origin>/mcp` and consents
   to once. Tokens carry the audience, never a path, so an access token issued
   for a per-scope server serves `/mcp` until it expires; a refresh token
   naming `/mcp/<scope word>` as its resource is refused (`invalid_target`),
   and one naming `/mcp` or nothing is honoured, as the connector tests state.

## Consequences

- One connector and one consent, fortnightly, for a client that works a Case
  end to end. A connector holding every scope lists 69 tools; the client's own
  tool policy narrows that view, which Pegasus does not own (ADR-0026).
- The Administrator decides at consent which scopes a connector holds, and
  the grant's history row keeps the scopes actually granted.
- The server table, the per-server session options, the per-server metadata
  handler and the per-server policies leave the source;
  `docs/current-architecture.md` changes with it. No data change and no
  migration.
- Live evidence is owed at the release and recorded in operations: the seven
  connectors removed and one added at `<origin>/mcp`, one consent with the
  tick boxes, the classifier read back on `/mcp` with one connecting tick, and
  the `tools/list` sizes (the classifier 7, a full connector 69).
- The rate limit, 120 requests a minute per IP address, is spent by one
  initialize and one list where seven servers spent seven of each.

## Options considered

**Two servers, `/mcp` and `/mcp/send`.** Reaches "casework without sending" by
not adding the send connector, keeping the per-server table for two rows and
two connectors. Rejected (R547): consent per scope reaches the same end with
one connector and no table.

**Keep seven servers.** Rejected (R547): seven connectors and seven consents
for the end-to-end client, which the readiness review refused (R509).

**One server, consent all-or-refuse.** No Razor change; a narrower connector
would be the client's job, and `automation.send` could be withheld only from
client-credentials callers. Rejected (R548).

## Links

- [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md)
- [ADR-0080](superseded/0080-automation-mcp-one-server-per-scope.md) (superseded)
- [ADR-0011](0011-restrict-mcp-to-automation-actor.md),
  [ADR-0026](0026-enable-automation-mcp-by-explicit-deployment-configuration.md),
  [ADR-0027](0027-authorization-code-for-external-mcp-connectors.md),
  [ADR-0064](0064-automation-actor-staff-casework-parity.md)
- [Current architecture](../current-architecture.md)
- [Operator rulings](../rulings.md): R509, R547, R548, R549
