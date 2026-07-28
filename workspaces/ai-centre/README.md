# Collision AI Centre

Collision AI Centre is the engineering home for a full Windows AI workstation built specifically
for collision engineers and remote vehicle assessment.

The intended product combines a case-centred desktop experience with governed agents that can
assemble instructions and evidence, work with Outlook, support assessment and valuation, retrieve
cited technical knowledge, draft correspondence, and produce reviewable reports and PDFs. The
engineer remains the decision-maker and author of record.

## Current state

This repository is at foundation stage.

- `services/collision-brain` is an implemented, tested provider-neutral ingestion and retrieval
  service with MCP interfaces.
- `ml-ops/reports` and `ml-ops/strategy` contain the opportunity assessment, governance analysis,
  training strategy, evaluation framework, and phased delivery research.
- The desktop app is a very-long-term product direction, not a current runtime. It must compose
  accepted Pegasus UI, Core, service, and renderer contracts rather than create parallel owners.
  See the [desktop application direction](docs/architecture/desktop-application-direction.md).
- Approved local development and ML-operations inputs are addressed beneath the ignored,
  immutable repository-root `corpus/ai-centre/` boundary. Box and Outlook archives are not
  imported. See the
  [development corpus contract](docs/governance/development-mlops-corpus.md).

## Product shape

```mermaid
flowchart LR
  Engineer["Collision engineer"] --> Desktop["Windows case workstation"]
  Desktop --> Runtime["Governed agent runtime"]
  Runtime --> Skills["Typed business skills"]
  Skills --> Connectors["Permissioned connectors"]
  Skills --> Brain["Collision Brain retrieval"]
  Skills --> Domain["Deterministic case and report packages"]
  Runtime --> Models["Approved hosted or owned models"]
  Domain --> Review["Evidence and approval surface"]
  Review --> Engineer
```

## Start here

- [Delivery plan](PLAN.md)
- [System architecture](docs/architecture/system-overview.md)
- [Long-term desktop application direction](docs/architecture/desktop-application-direction.md)
- [Data boundaries](docs/governance/data-boundaries.md)
- [Data-use authorisation](docs/governance/data-authorisation.md)
- [Development and ML-operations corpus](docs/governance/development-mlops-corpus.md)
- [Repository decisions](docs/adr/0001-repository-and-runtime-boundaries.md)
- [ML operations](ml-ops/README.md)
- [Collision Brain](services/collision-brain/README.md)

Repository contributors and coding agents must read [the workspace instructions](../AGENTS.md).
