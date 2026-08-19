# Context

## Binding direction

CollisionRenderer is integrated into the Pegasus .NET monolith and Azure deployment. It is not a separate product, service, repository, NuGet package, API, MCP host, or independently deployed unit. `Pegasus.Core` owns report readiness, policy, immutable identity, and the render contract; Infrastructure adapts the proven rendering engine; Web/Worker remain composition roots.

When a system assessment has all required accepted details, the application invokes rendering through the Core-owned workflow. `reference/rendererref1/` is supplied evidence for assessment-report templates, schema, fixed wording, design, signatures, and sample outputs; it informs implementation but does not become a second policy owner.

Generated reports must have immutable version/reference identity and hash, retained provenance and custody, fail closed on incomplete or ambiguous required data, and use correction/addendum versioning rather than overwrite. Azure integration must use the existing deployment topology unless research proves an accepted ADR is required; no cloud write is authorised without the operator's explicit target approval.

## Governing context

ADR-0025 and FRD-11 govern the integration. Repository workflow is governed by AGENTS.md. Member tickets retain their own canonical refs and any existing group memberships.

## Open-question rule

Operator-only product choices are recorded in each ticket's `open-questions/` document and surfaced before implementation. Technical questions that existing authority or read-only evidence answers are resolved by research rather than escalated.
