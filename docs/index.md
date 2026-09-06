# Repository documentation

One file per question. `docs/` contains prose only; supplied evidence is
indexed under the top-level [`reference/`](../reference/README.md) tree and
design assets remain under [`docs/design/`](design/).

| Question | File |
| --- | --- |
| What is in flight and what can I take? | The Kanmer board (`.kanmer/`, via the `kanmer` tools) |
| What are the current v1 stream boundaries? | [Current work](../NOW.md), then the owner tickets |
| Why must Pegasus do it — business need, users, outcomes, scope? | [PRD](prd/README.md) |
| How must a capability behave — I/O, states, rules, edge cases, acceptance? | [FRD index](frd/README.md) |
| What does the product do, in what order? | [Capabilities](capabilities.md) — the roadmap and capability-ID registry; its *Canonical owner* column joins each ID to its PRD, FRD, or ADR |
| What is deferred or deliberately excluded, and why? | [Boundaries](boundaries.md) |
| What is undecided? | [Open decisions](open-decisions.md) |
| What did Collision Engineers actually say? | [Operator notes](operator-notes.md) |
| What exists now (the as-built snapshot)? | [Current architecture](current-architecture.md) |
| What is deployed, released, monitored, or recovery-proved now? | [Operations](operations.md) |
| How do I set up, develop, test, run, release, monitor, or recover? | [Runbook](runbook.md) |
| What engineering guidance and evidence tiers apply? | [Engineering](engineering.md) |
| What procedure governs task claims, plans, reviews, and Git safety? | [Repository task workflow](../AGENTS.md#repository-task-workflow) |
| What durable technical decisions apply? | [Decision index](adr/README.md) (ADR bodies are immutable) |
| What raw supplied evidence exists? | [Reference evidence](../reference/README.md) |
| What are the UI rules, and the Integrated Operations Workspace shell contract? | [Design](design/README.md) — visual, component and shell authority; route and page behaviour (Work Centre, Cases, Search, Case workspace) is [FRD-12](frd/frd-12-operator-experience.md) |
| How is a provider's email identified, classified, and mapped? | [Principal rules and mappings](principal-rules-and-mappings/README.md) — descriptive companions; the cited FRD/ADR/policy owners bind |
| What is the Azure production state? | [Operations § Production environment](operations.md#production-environment) — the sole current-state owner; `.azure/deployment-plan.md` is the immutable 2026-08-02 execution record |
| What do the imported source workspaces own? | [Workspaces](../workspaces/README.md) |
| What do domain terms mean? | [`CONTEXT.md`](../CONTEXT.md) (repo root) |

## Authority

operator-notes.md (business fact) > PRD (`prd/`, product intent — what and why) >
FRD (`frd/`, functional specification — required behaviour) > capabilities.md
(schedule and capability-ID registry) > ADRs (durable technical decisions) >
current-architecture.md and operations.md (current state) > runbook.md, engineering.md,
and design/README.md (working rules within their scopes). Code plus passing tests beat any document about
current state. On conflict: fix the losing document in the same commit you
notice it; if the conflict is material and you cannot resolve it, put one line
in [open decisions](open-decisions.md) and stop the affected work.

## New Markdown files

The approved v1 task adds `NOW.md` as a canonical orientation index. It is a
named exception, not a location for task plans, checklists or proof.

A new repository Markdown file is one of: a product requirements document under
[`docs/prd/`](prd/README.md); a functional requirements document under
[`docs/frd/`](frd/README.md); a durable technical decision under
[`docs/adr/`](adr/README.md). Transient task research, plans, checklists,
reviews, and proof live in the owning Kanmer ticket documents. A new PRD or FRD records its
canonical owner in [capabilities.md](capabilities.md) and is linked from this
index. Everything else edits an existing canonical file. Documentation rules and
conventions themselves live in [`AGENTS.md`](../AGENTS.md), not in an ADR.
Workspace-local documentation remains governed by its accepted integration
contract and existing workspace tree.

## Image-initiated Case authority

The durable technical boundary is [ADR-0029](adr/0029-image-initiated-case-projection.md).
Behaviour is owned by FRD-01, FRD-02, FRD-05, FRD-06, and FRD-12. The formal
Instruction-initiated Case remains the only Case/PO allocator; Image-initiated
records use their separate VRM reference and lifecycle history.
