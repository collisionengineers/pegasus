# ADR 0001: Repository and runtime boundaries

- **Status:** Accepted
- **Date:** 21 July 2026

## Decision

Use one product repository with explicit top-level ownership boundaries for the Windows app, business
agents, production skills, connectors, shared packages, services, ML operations, and versionable model
metadata.

The runtime dependency direction is:

`desktop -> agents -> skills -> packages/services/connectors`

Provider-specific SDKs remain inside connectors or service adapters. The desktop does not own case
rules. Models propose structured changes; deterministic packages validate and render; a human accepts
or rejects material actions.

The authorised source archives and binary model artifacts may be versioned in this repository
when practical. Large derived artifacts may instead live in a documented registry and be referenced by
immutable hash. Git also stores code, schemas, manifests, cards, fixtures, and evaluation definitions.

## Consequences

- A complete feature normally spans a thin desktop surface, an agent/skill contract, a domain or
  connector implementation, audit events, and boundary tests.
- The existing RAG pipeline becomes `services/collision-brain` without changing its contract.
- Contributor-agent skills live in `.agents/skills`; production business skills live in `skills`.
- Cross-boundary schemas need versioning and migration discipline.
