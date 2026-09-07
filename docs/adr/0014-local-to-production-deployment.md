---
id: ADR-0014
status: accepted
date: 2026-07-31
supersedes: [ADR-0007]
superseded_by: []
related_capabilities: []
related_frd: []
tags: [deployment, environment]
---
# ADR-0014: Local-to-production deployment only

**Status:** Accepted (2026-07-31)  
**Supersedes:** ADR-0007's Azure development/integration-environment clauses

## Context

`DOC-CON-071` records a material conflict: repository material described a shared Azure development/integration environment, while the operator has confirmed that Pegasus has no such environment.  The actual model is local development and validation followed by an explicitly approved deployment to production.

## Decision

Pegasus has two environments: isolated local development and production.  There is no Azure development, test, integration, or staging environment.  Production deployment remains an authorised-terminal operation using committed infrastructure and a build-once, deploy-same-artifact route, with the existing exact-target approval, migration, health, smoke, rollback, and acceptance gates.  Local validation does not prove production behavior, and production deployment does not waive any of those gates.

## Consequences

- `OPS-10` is the direct production deployment capability; `OPS-11` requires production isolation from local resources only.
- No document, capability, infrastructure declaration, or future change may assume or create a non-production Azure environment without a new accepted decision and exact external authority.
- Historical references to a shared development/integration environment remain historical evidence, not current topology or executable authority.
