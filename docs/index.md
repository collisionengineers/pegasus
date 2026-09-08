# Repository documentation

Use the owner of the question. Current operator instructions and accepted
requirements establish intent. Source establishes implementation; dated exact
artifact observations establish deployment. A green test does not override a
business requirement or prove live acceptance. Resolve material conflicts in
the affected owner; record only the genuinely undecided portion.

| Question | Canonical owner |
| --- | --- |
| Agent entry and non-obvious repository constraints | [AGENTS](../AGENTS.md) |
| Product outcomes, users, quality targets and exclusions | [PRD](prd/pegasus-product.md) |
| Functional behavior, state, limits and acceptance | [FRDs](frd/README.md) |
| Durable technical decisions and current successors | [ADRs](adr/README.md) |
| Domain terminology | [CONTEXT](../CONTEXT.md) |
| Stable capability identities and requirement links | [Capabilities](capabilities.md) |
| Current work, ordering, grants and delivery evidence | Kanmer ticket/group and its linked PR/CI records |
| Unresolved product or technical choices | [Open decisions](open-decisions.md) |
| Source structure and policy/caller locations | [Architecture](current-architecture.md) |
| Last observed deployed estate and operational support | [Operations](operations.md) |
| Engineering and verification policy | [Engineering](engineering.md) |
| Local, verification and operational procedures | [Runbook directory](runbook.md) and existing release/wipe skills |
| Visual assets, components and presentation | [Design](design/README.md); functional interactions remain in FRD-12 |
| Principal-policy evidence and descriptive companions | [Principal mappings](principal-profiles/README.md) |
| Supplied domain evidence | [Reference](../reference/README.md) |
| Supplied vendor/component contracts | [External component documents](external-component-documents/README.md) |
| Retired source imports and admission boundary | [Workspaces](../workspaces/README.md) |

## New Markdown files

Create a PRD for product intent, an FRD for functional behavior, and an ADR for
a durable technical choice. Use their existing directories and indexes. Put a
rule in its existing canonical owner whenever possible; add a focused document
only when it answers a distinct recurring question that lacks an owner.

Operational procedures remain in the runbook. Existing release, wipe and Razor
skills retain their defined scope; do not create skills for documentation sections. Engineering/configuration references may live under
`docs/engineering/`; vendor evidence stays under `docs/external-component-documents/`.
These locations do not create new product or workflow authority.

Routine task research, plans, reviews and proof belong to Kanmer. An explicit
operator request may create a temporary review artifact at the requested path;
mark it temporary and remove it when its useful results have durable owners.
Do not invent a technical ADR merely to authorize a documentation move.

## Markdown convention

- Start ordinary prose with an H1. Supported YAML frontmatter and managed
  preambles may precede it. Preserve generated formats and vendor source fidelity.
- Separate headings and lists from surrounding prose with blank lines.
- Use compact Markdown tables with `| --- |` delimiters, without alignment padding.
- Hard-wrap ordinary prose near 78 columns; tables, code and link-dense lines
  may run longer. Readability matters more than a line-count target.
- Use relative links to canonical clauses; repair inbound links when moving or
  retiring a heading. Immutable supplied evidence is not reformatted merely to
  satisfy prose conventions.

## Decision records

Keep issued ADR IDs and rationale recoverable. A changed technical decision
uses the next unissued ID; do not fill the deliberately unissued ADR-0017 gap.
Frontmatter records status and successor relationships. For partial replacement,
identify replaced clauses and surviving owners explicitly: a whole-file
superseded label must not hide a still-active rule. PRD/FRD indexes define their
document shapes; Kanmer's templates do not override Pegasus-specific placement.
