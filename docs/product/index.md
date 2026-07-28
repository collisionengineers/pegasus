# Product requirements

- Repository mode: `development`
- Maturity stage: `prototype`
- Version scheme: Semantic Versioning (`MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]`)
- Current version: `0.0.0-development`
- Release authority: Collision Engineers management accepts production releases; Alex owns technical release execution or authorization.
- Visual UI: `present`

## Purpose and problem

Pegasus is Collision Engineers' clean-room case-management and reporting
application. It replaces duplicated rules, spreadsheet control, manual filing
and, capability by capability, EVA engineering work without importing the
predecessor application or treating supplied evidence as executable authority.
The current repository is a Development proof, not a released product or Azure
deployment.

One accepted structured case and engineering record is the intended source for
reports, fee notes, addenda, query documents, invoice inputs and management
measures. Each durable output retains its own capability owner; `CASE-31` does
not turn the case record into a renderer, mailer, invoice engine or statistics
implementation.

Detailed settled behavior remains controlled by the authoritative
[operator notes](../operator-notes/README.md). The distilled
[discovery questionnaire](../history/product/project-discovery-questionnaire.md)
is direct-decision evidence, while this index and the linked product areas own
the living product model.

## Users and outcomes

- Approximately eight operational Collision Engineers staff use self-managed
  application accounts; Alex is the developer and an Administrator rather than
  an additional operational caseworker.
- Administrator, Engineer and User roles perform authorized work.
  Administrators manage accounts, principals, operational configuration and
  the Outlook mailbox allowlist.
- Andrew and Alex are initial Administrator assignments held as application
  data/configuration. No person, name, email address or bypass is compiled into
  authorization.
- The first usable release proves every active QDOS case type end to end while
  retaining EVA for named-Engineer assignment, estimating, valuation and report
  preparation.
- External/customer application users and public registration are not product
  roles. A temporary request-scoped upload link does not create an account.

## Success measures

- Every active QDOS Inspection, standalone Audit and Inspection + Audit follows
  one accepted path from authorized intake through Box custody, immutable
  identity, review/work management, the exact manual EVA handoff, report
  evidence and terminal history.
- Ambiguous or incomplete intake creates no case and allocates no reference.
- One accepted source version drives each derived value and deterministic
  output; accepted case truth is never changed by an imported workspace or AI
  proposal.
- A genuine-input cohort and untouched holdout support extraction evidence;
  operator acceptance, deployment evidence and management acceptance remain
  separate gates.
- The supported Azure release path proves migration, immutable packages,
  health/smoke evidence and recovery before production use.

## Scope

### Included

- QDOS-alpha intake, Triage, identity/lifecycle, document custody, work
  management, operator UI, staff MCP, vehicle enrichment, the manual EVA
  JSON/image handoff, observability and approved direct-terminal release.
- `INT-31` as a `0.1.0-alpha.1` requirement: authenticated staff may generate a
  temporary, revocable, request-scoped image/document upload link. Runtime
  implementation remains a separately accepted slice.
- One provider-neutral Core workflow with separate code-versioned direct and
  intermediary route policies.
- Stable capability IDs and activation boundaries in
  [capabilities](capabilities.md).
- Non-deployed source workspaces for document extraction, deterministic
  rendering and future AI Centre work. Incorporation is not activation.

### Excluded

- Predecessor code/data migration or operation after cutover.
- Dormant capabilities, callers, schemas, routes, credentials, model endpoints
  or UI placeholders before their owning activation.
- Persistent customer/case portals, external accounts and public registration.
- Direct model APIs, assumed Claude-client job automation, EVA replacement,
  report rendering, general/targeted email sending, management dashboards or
  valuation adapters in the current orientation change.
- Every `Not planned` capability, including mobile staff UI, GitHub Actions
  deployment, staging/slots/private networking and dedicated compliance
  workflows.

## Requirements and invariants

- `docs/operator-notes/` is binding operator/business authority. Maintainers may
  preserve and organize it under standing authorization but cannot change
  material meaning without direct resolution.
- Principal and reference become immutable on allocation. Wrong-principal work
  closes as `Created in error`, links a replacement and never reuses either
  reference.
- Cases are never permanently deleted. Reopening needs a reason and normal
  destination gates; `Created in error` never reopens.
- `Triage` is a separate pre-case roadworthiness record; `Needs sorting` and
  `Blocked intake` are distinct intake outcomes.
- The current case types are Inspection, standalone Audit, and Inspection +
  Audit. Audit reference rules fail closed when the required assessment is
  missing or ambiguous.
- Core owns each business rule once. Web, Worker, future workspace adapters and
  AI workers call Core-owned use cases and cannot write accepted case truth
  directly.
- Box owns long-term original files; SQL owns workflow, identity and history;
  transient storage is not long-term custody.
- Engineer acceptance is explicit, logged and attributable for valuation,
  repair specification, outcome, salvage, roadworthiness and any AI proposal.
- The canonical [product areas](areas/) own the full requirement set; the
  retained [questionnaire](../history/product/project-discovery-questionnaire.md)
  preserves direct-decision evidence, and the [QDOS-alpha gap](qdos-alpha-gap.md)
  separates current proof from the intended alpha outcome.

## Quality constraints

- Windows and PowerShell 7 are the supported repository workflow.
- Secrets use managed identity/RBAC where possible and approved secret stores
  otherwise; plaintext credentials never enter source or output.
- Local genuine material remains ignored and immutable. Repository-provided
  examples only; no synthetic operational material.
- Current workload is 1,000–1,200 jobs per month. The sizing target remains
  approximately eight concurrent staff and 2,000 new cases per month.
- The service targets a 15-minute database recovery point and four-hour
  restoration path, proved before acceptance.
- Accessibility requires keyboard operation, visible focus, semantic structure,
  associated errors, practical target sizes, forced colours and reduced motion;
  mobile staff UI remains out of scope.

## Supported contracts

The required current external output contract is the observed 13-key EVA
drag-and-drop shape. It remains the operator-approved handoff format until a
replacement is separately accepted. Infrastructure owns its exact serialization;
Core owns the typed handoff values rather than the vendor shape.

## Functional areas

- [Capability inventory](capabilities.md)
- [QDOS-alpha gap baseline](qdos-alpha-gap.md) and
  [product boundaries](boundaries.md)
- Areas: [identity/access](areas/identity-and-access.md),
  [intake/casework](areas/intake-and-casework.md),
  [documents/integrations](areas/documents-and-integrations.md),
  [interfaces/automation](areas/interfaces-and-automation.md), and
  [platform/operator experience](areas/platform-and-operator-experience.md)
- [Selected operator experience](../../design/product/requirements.md)
- [Architecture](../architecture.md)
- [Operations](../operations.md)

## Limitations

The only proven mutating product caller is the Development-only manual Web
intake route. It creates a reviewable pre-case receipt/draft but no case or
reference. The Worker has no trigger or Core caller. Authentication, production
custody, Graph, Box writes, vehicle recognition, EVA export, lifecycle
management, Azure deployment and operator acceptance remain absent or planned,
as detailed in the
[current implementation handoff](../agent-notes/current-implementation-handoff.md).

## Open decisions

EVA field mapping, provider/client contracts, valuation-source terms, Audatex
variants, report wording and the `AI-09` transport experiment remain named,
capability-specific blockers in [open decisions](open-decisions.md). They do not
activate work or weaken fail-closed behavior.
