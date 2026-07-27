# Product requirements

- Repository mode: `development`
- Maturity stage: `prototype`
- Version scheme: Semantic Versioning (`MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]`)
- Current version: `0.0.0-development`
- Release authority: Collision Engineers management accepts production releases; Alex owns technical release execution or authorization.
- Visual UI: `present`

## Purpose and problem

CollisionSpike v2 is the clean-room case-management application for Collision
Engineers. It must deliver the smallest coherent case workflow without the
predecessor's duplicated rules, drift, ticket machinery, or speculative layers.
The current repository is a development proof, not a released product or Azure
deployment.

Detailed settled behavior remains controlled by the
[project questionnaire](../../PROJECT_DISCOVERY_QUESTIONNAIRE.md), read after the
authoritative [operator notes](../operator-notes/README.md). This index owns the
repository-level product profile and routes; it does not weaken those sources.

## Users and outcomes

- Approximately eight internal Collision Engineers staff use self-managed application accounts.
- Administrator, Engineer, and User roles perform authorized case work; only Administrators manage accounts, principals, operational configuration, and the Outlook mailbox allowlist.
- The first usable release proves every active QDOS case type end to end while keeping deferred work absent until activation.
- External/customer application users and public registration are not product roles.

## Success measures

- Every active QDOS Inspection, standalone Audit, and Inspection + Audit follows one real accepted path from authorized intake through Box custody, correct identity, review/work management, EVA JSON/image handoff, exact report evidence, and terminal history.
- Ambiguous or incomplete intake creates no case and allocates no reference.
- A genuine-input cohort and untouched holdout support field-level extraction evidence; operator and management acceptance remain separate gates.
- The supported Azure release path proves migration, immutable packages, health/smoke evidence, recovery, and explicit approval before production use.

## Scope

### Included

- V1 QDOS intake, Triage, case identity/lifecycle, document custody, work management, operator UI, staff MCP, vehicle enrichment, EVA handoff, observability, and approved direct-terminal Azure release.
- One provider-neutral Core workflow with separate code-versioned
  direct-provider and intermediary route policies and Web/Worker composition
  roots.
- Stable capability IDs and activation boundaries in [capabilities](capabilities.md).

### Excluded

- Predecessor code/data migration or operation after cutover.
- Dormant V1.x, V2, V3, V3+, or conditional implementation before its owning decision and activation evidence.
- Every `Never` capability, including public/external accounts, mobile staff UI, general in-app email composition, GitHub Actions deployment, staging/slots/private networking, and speculative compliance workflows.

## Requirements and invariants

- `docs/operator-notes/` is absolute operator/business authority. Azure Workflow
  may maintain its documentation and organization under standing user
  authorization, but cannot change material meaning without direct resolution.
- A case principal and reference become immutable on allocation. Wrong-principal work closes as `Created in error` and links a new replacement; references are never reused.
- Cases are never permanently deleted. Reopening requires a reason and ordinary gates; `Created in error` never reopens.
- `Triage` is a separate pre-case roadworthiness record. `Needs sorting` and `Blocked intake` are separate intake outcomes.
- The current case types are Inspection, standalone Audit, and Inspection + Audit. Audit reference rules fail closed when the required assessment is missing or ambiguous.
- Core owns each business rule once; Web and Worker call the same use cases through Infrastructure ports.
- Box owns long-term original files; SQL owns application workflow/identity/history metadata; transient Azure storage is not long-term custody.
- The full requirement set and first-release boundary are in the [settled questionnaire](../../PROJECT_DISCOVERY_QUESTIONNAIRE.md); the [V1 gap](v1-gap.md) separates current proof from the intended V1 outcome.

## Quality constraints

- Windows and PowerShell 7 are the supported repository workflow.
- Secrets use managed identity/RBAC where possible and approved secret stores otherwise; plaintext credentials never enter source or output.
- Local genuine material remains ignored and immutable. Repository-provided examples only; no synthetic operational material.
- Approximately eight concurrent users and 2,000 cases per month are the current sizing inputs.
- The V1 service targets a 15-minute database recovery point and four-hour restoration path, proven before acceptance rather than inferred from configuration.
- Accessibility requires keyboard operation, visible focus, semantic structure, associated errors, practical target sizes, forced-colours and reduced-motion support; mobile staff UI remains out of scope.

## Supported contracts

None yet. This repository is in development mode at prototype maturity; no
pre-release behavior, deployment interface, data shape, or extension point is a
compatibility promise. Settled business requirements still govern intended
behavior and must not be silently changed.

## Functional areas

- [Capability inventory](capabilities.md)
- [V1 gap baseline](v1-gap.md) and [product boundaries](boundaries.md)
- Areas: [identity/access](areas/identity-and-access.md), [intake/casework](areas/intake-and-casework.md), [documents/integrations](areas/documents-and-integrations.md), [interfaces/automation](areas/interfaces-and-automation.md), and [platform/operator experience](areas/platform-and-operator-experience.md)
- [Direction-neutral operator experience](../../design/product/requirements.md)
- [Architecture](../architecture.md)
- [Operations](../operations.md)

## Limitations

The only proven mutating product caller is the Development-only manual Web intake
route. It creates a reviewable pre-case receipt/draft but no case or reference.
The Worker has no trigger or Core caller. Authentication, production custody,
Graph, Box writes, vehicle recognition, EVA export, lifecycle management, Azure
deployment, and operator acceptance are absent or planned, as detailed in the
[current implementation handoff](../agent-notes/current-implementation-handoff.md).

## Open decisions

- Mailbox categorisation and automatic matching predicates/governance block only their named slices.
- Operations-first is selected for the V1 shell; detailed implementation remains planned.
- Guided capture, Tractable/Ravin, and a custom domain remain conditional/unallocated.

The canonical questions remain in [open decisions](open-decisions.md). Azure ownership/retirement choices
are separate exact-target operations, not product ambiguities.
