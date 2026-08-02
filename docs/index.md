# Repository documentation

One file per question. Edit these files in place; new Markdown files are
created only as ADRs under [docs/adr/](adr/README.md).

| Question | File |
| --- | --- |
| What am I doing right now? | [`NOW.md`](../NOW.md) (repo root) |
| What must Pegasus do? | [Requirements](requirements.md) |
| What does the product do, in what order? | [Capabilities](capabilities.md) — the roadmap: 229 stable IDs with release targets |
| What is undecided? | [Open decisions](open-decisions.md) |
| What did Collision Engineers actually say? | [Operator notes](operator-notes.md) |
| What exists now? | [Architecture](architecture.md) |
| How do I develop, test, run, deploy, recover? | [Operations](operations.md) |
| How is repository work done? | [Engineering](engineering.md) |
| What durable technical decisions apply? | [Decision index](adr/README.md) (ADR bodies are immutable) |
| What raw supplied evidence exists? | [Reference evidence](reference/README.md) |
| What are the UI rules? | [Design](../design/README.md) |
| What is the Azure production state? | [Operations § Production environment](operations.md#production-environment) and `.azure/deployment-plan.md` |
| What do the imported source workspaces own? | [Workspaces](../workspaces/README.md) |
| What do domain terms mean? | [`CONTEXT.md`](../CONTEXT.md) (repo root) |

## Authority

operator-notes.md (business fact) > requirements.md (intent) >
capabilities.md (schedule) > ADRs (technical decisions) > architecture.md and
operations.md (current state). Code plus passing tests beat any document about
current state. On conflict: fix the losing document in the same commit you
notice it; if the conflict is material and you cannot resolve it, put one line
in [open decisions](open-decisions.md) and stop the affected work.
