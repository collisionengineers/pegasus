---
id: ADR-0041
status: accepted
date: 2026-09-08
supersedes: [ADR-0027]
superseded_by: []
related_capabilities: []
related_frd: [FRD-10]
tags: [security, persistence]
---
# ADR-0041: Persistent Automation keys and grant attribution

## Status

Accepted contract recorded from current FRD-10; this document replaces
only the conflicting mechanism below, not the unrelated clauses of ADR-0027.
This proposed patch does not itself constitute a deployment or new authorization.

## Context

The earlier decision and current functional contract describe different
persistence behavior. Keep one current technical explanation and preserve the
earlier rationale as history.

## Decision

Production uses separate persistent signing and encryption certificates supplied
as exact Key Vault secret versions, read by the Web managed identity at the
approved secret scope. Configuration failure is fail-closed. Isolated process
keys are explicitly development-only and rejected in Production.

Attribution distinguishes the approved human grant from its external client.
Rotation overlaps new and required old material; verify issue, validation and
refresh across restart and replicas before retirement. Old material remains
through the last old-key issuance plus the maximum token lifetime: ten-minute
access tokens and non-sliding refresh tokens of at most fourteen days.
Emergency invalidation is explicit and records affected grants/tokens.

## Consequences

Normal restart no longer invalidates all production tokens. Rotation and
revocation require version-aware operations and distinct evidence. Authorization
code, PKCE, consent and actor restrictions remain as defined by FRD-10; no
secret bytes or standing mutation permission are introduced.

## Links

- [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md)
- [ADR-0027](0027-authorization-code-for-external-mcp-connectors.md)
