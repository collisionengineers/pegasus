# Repository documentation

One file per question. `docs/` contains prose only; supplied evidence is
indexed under the top-level [`reference/`](../reference/README.md) tree and
design assets remain under [`design/`](../design/).

| Question | File |
| --- | --- |
| What is in flight and what can I take? | [`NOW.md`](../NOW.md) (repo root; authoritative copy is `origin/dev`'s) |
| What must Pegasus do? | [Requirements](requirements.md) |
| What does the product do, in what order? | [Capabilities](capabilities.md) — the roadmap: 228 stable IDs with release targets |
| What is undecided? | [Open decisions](open-decisions.md) |
| What did Collision Engineers actually say? | [Operator notes](operator-notes.md) |
| What exists now? | [Architecture](architecture.md) |
| What is deployed, released, monitored, or recovery-proved now? | [Operations](operations.md) |
| How do I set up, develop, test, run, release, monitor, or recover? | [Runbook](runbook.md) |
| How is repository work done? | [Engineering](engineering.md) |
| What durable technical decisions apply? | [Decision index](adr/README.md) (ADR bodies are immutable) |
| What raw supplied evidence exists? | [Reference evidence](../reference/README.md) |
| What are the UI rules? | [Design](design.md) |
| What is the Azure production state? | [Operations § Production environment](operations.md#production-environment) — the sole current-state owner; `.azure/deployment-plan.md` is the immutable 2026-08-02 execution record |
| What do the imported source workspaces own? | [Workspaces](../workspaces/README.md) |
| What do domain terms mean? | [`CONTEXT.md`](../CONTEXT.md) (repo root) |

## Authority

operator-notes.md (business fact) > requirements.md (intent) >
capabilities.md (schedule) > ADRs (technical decisions) > architecture.md and
operations.md (current state) > runbook.md, engineering.md, and design.md
(working rules within their scopes). Code plus passing tests beat any document about
current state. On conflict: fix the losing document in the same commit you
notice it; if the conflict is material and you cannot resolve it, put one line
in [open decisions](open-decisions.md) and stop the affected work.

## New Markdown files

ADR-0023 is the one authorised restructure exception: this change creates
`docs/runbook.md` and moves the existing design authority to `docs/design.md`.
After that restructure, new Markdown files are created only as accepted
decisions under
[docs/adr/](adr/README.md) or as transient task plans under
[docs/temp-plans/](temp-plans/README.md). Everything else edits an existing
canonical file. Workspace-local documentation remains governed by its accepted
integration contract and existing workspace tree.
