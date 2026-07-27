# Repository structure plan

Status: **Historical** — reviewed and implemented on 2026-07-23.

Supersession/current owners: [plans index](docs/history/plans/README.md), [architecture overview](docs/architecture/README.md), and [validation guidance](docs/agent-guidance/validation.md). This planned tree remains implementation history, not ongoing guidance.

## Objective

Create a small, navigable .NET 10 repository in which every dependency has one direction, every runtime calls one domain core, genuine operational data remains local, and Azure infrastructure is reproducible without becoming the application architecture.

## Planned tree

```text
/
|-- AGENTS.md
|-- CollisionSpike.slnx
|-- global.json
|-- Directory.Build.props
|-- azure.yaml
|-- src/
|   |-- CollisionSpike.Core/
|   |-- CollisionSpike.Infrastructure/
|   |-- CollisionSpike.Web/
|   `-- CollisionSpike.Worker/
|-- tests/
|   |-- CollisionSpike.Core.Tests/
|   |-- CollisionSpike.IntegrationTests/
|   `-- CollisionSpike.ArchitectureTests/
|-- infra/
|   |-- main.bicep
|   |-- main.parameters.json
|   `-- modules/
|-- scripts/
|-- docs/
|   |-- agent-guidance/
|   |-- architecture/
|   |-- azure/
|   |-- evaluation/
|   |-- operator-notes/
|   |-- plans/
|   |   `-- ui-ux/
|   `-- runbooks/
|-- retrospectives/
|-- .codex/
|   |-- agents/
|   |-- hooks/
|   `-- skills/
|-- .azure/deployment-plan.md
|-- .github/workflows/ci.yml
|-- corpus/                  # local and gitignored
`-- artifacts/               # generated and gitignored
```

## Ownership and dependency rules

| Area | Owns | Must not own |
|---|---|---|
| Core | domain entities, value objects, decisions, ports | Azure SDKs, EF Core, HTTP, UI |
| Infrastructure | EF Core and Box/Graph/EVA/Azure adapters | duplicated business decisions |
| Web | operator UI, HTTP endpoints, authentication composition | shadow domain models |
| Worker | polling, queue triggers, scheduled orchestration | alternate intake engine |
| Tests | executable evidence and architecture constraints | production implementations |
| Infra | Azure resource declarations and RBAC | application workflow logic |
| Docs | decisions, runbooks, evidence summaries | generated mirrors of code |

## Review

The plan was checked against the accepted modular-monolith ADR, the operator notes, the predecessor retrospective, Azure Functions isolated-process guidance, and Azure Developer CLI template conventions.

Rejected during review:

- Microservices, a project per feature, and separate parsing/OCR engines: they recreate ownership splits before scale requires them.
- A generated ticket or ledger hierarchy: the predecessor proved that repository bookkeeping can pass while runtime behavior fails.
- Copying predecessor source into a `legacy` area: the old code is evidence, not a foundation.
- Committing the corpus: it contains genuine operational material and is local evaluation input only.
- Speculative projects for valuation, invoicing, messaging automation, or mobile capture: these are deferred or separate decisions.
- An `Application` project containing pass-through handlers: add a boundary only if actual use cases outgrow direct Core orchestration.

## Implementation test

The structure is complete when the solution builds, architecture tests enforce dependency direction, the repository check verifies required paths, the corpus remains ignored, and no feature is claimed merely because its folder exists.
