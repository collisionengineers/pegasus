---
id: ADR-0011
status: accepted
date: 2026-07-30
supersedes: [ADR-0004]
superseded_by: []
related_capabilities: []
related_frd: [frd-10]
tags: [mcp, auth]
---

# ADR-0011: Restrict MCP to a vendor-neutral Automation Actor

## Context

ADR-0004 established a remote MCP surface authenticated with per-staff OAuth
tokens. The accepted operating direction instead restricts MCP to one named,
management/development-controlled Automation Actor. Ordinary staff use the
authenticated Web application and do not receive MCP access.

Pegasus must retain its Core-owned authorization, lease, idempotency, and
permanent-history boundaries while distinguishing the automated actor from a
human staff member. This decision changes only the internal MCP actor and
access boundary. The provider API boundary in ADR-0004 remains unchanged.

## Decision

Supersede the staff-MCP access and authentication clauses of ADR-0004 and the
related staff-MCP clauses of ADR-0002 as follows:

- MCP is a management/development-controlled ingress for one named,
  vendor-neutral Automation Actor, not a staff interface.
- The Actor has distinct authentication and durable identity. Every MCP action
  records that Actor, caller, target, action, outcome, reason where required,
  and the applicable before/after evidence in Pegasus permanent history.
- The Actor may invoke only its approved inventory of ordinary operational Core
  use cases. It has no Administrator, configuration, credential, cloud,
  release, deletion, or other management authority.
- Ordinary staff have no MCP access. They use the Web UI, which retains the
  same Core authorization and Case edit/concurrency guards.
- MCP tools call the same Core use cases as Web and Worker callers. A tool
  schema, OAuth registration, endpoint, or client log neither authorizes a
  business action nor proves a caller, deployment, or operator acceptance.

The user-triggered domain action remains vendor-neutral `Send to AI`. While
Claude is the sole accepted provider, the current UI may label that action
`Send to Claude`; the label neither changes the action identity nor this MCP
access boundary. A later provider or accepted UI change may relabel it without
a domain or data migration.

## Consequences

The Automation Actor is attributable without impersonating staff or creating a
second policy engine. Claude Desktop may supply initial acceptance evidence,
but it does not own the actor identity or Core policy. The exact tool inventory,
authentication/client contract, evaluation, recovery, cancellation, and caller
evidence remain activation work; this decision creates no endpoint, credential,
deployment, or AI caller.

Provider API authentication, principal isolation, and provider-client limits
remain governed by ADR-0004. Any future expansion of MCP access to people or
another automation identity requires a new accepted decision and explicit Core
authorization contract.
