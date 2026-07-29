# Pegasus source workspaces

These directories are independently buildable source imports. They are not
projects in `Pegasus.slnx`, application callers, runtime services, deployment
units, or owners of Pegasus business policy.

## Current sources and provenance

| Workspace | Durable role and documentation | Source provenance | Imported source manifest |
| --- | --- | --- | --- |
| `document-extraction/` | CollisionDocNet document/email extraction libraries and CLI; [workspace owner](document-extraction/README.md) | Local source snapshot `../collisiondocnetconverter`; no `.git` metadata was present, so branch, remote, and commit are unavailable | 202 files, 2,232,617 bytes, SHA-256 `0601db7fafd343b4ab46b67dc67f87a9711e635b5a0c52a051e571fe7f522901` |
| `report-renderer/` | Deterministic CollisionRenderer report-rendering source; [workspace owner](report-renderer/README.md) | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collisionrenderer` | 108 files, 610,098 bytes, SHA-256 `376b6f796acd5864cbb67be210b2aed1c7b4d1f90f767fb1bd24e5607a893619` |
| `ai-centre/` | AI model, evaluation, training, provider, and AI-service experimentation; [workspace owner](ai-centre/README.md) | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `collision-ai-centre` | 76 files, 607,769 bytes, SHA-256 `997b36219d4b1d7437ece89061c08bf193be9979cb8b4041475788fc2eed7bda` |
| `ai-centre/skills/` | Source skill packages and pack-validation tools; [package index](ai-centre/skills/README.md) | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collision-agent-skills` | 224 files, 3,063,546 bytes, SHA-256 `3a3719beb8b0b6539c41b90c6b5220ff891e6461a8149f2dcc8c597c0ef6eca1` |

The manifest hashes each tracked Git index path in UTF-8 immediately followed
by its staged blob payload, in ordinal path order. The AI Centre row excludes
`skills/`, `ml-ops/data/`, nested `.github/`, caches, and build outputs. The
skills row also excludes nested `.github/`, caches, and
`assets/style-examples/` or `fixtures/style-examples/`. Source `_dev/` trees are
represented under `dev-ref/<skill-name>/`. A manifest proves source identity,
not application integration, deployment, or acceptance.

## Ownership and activation

- `Pegasus.Core` owns every business rule and accepted case outcome.
  Infrastructure, Web, and Worker are the application composition roots.
- Workspace validation is independent. Application build, publish, and deploy
  must not compile, reference, dynamically load, invoke, or package workspace
  code without a separately accepted integration contract and actual caller.
- AI Centre and skill packages may produce evidence, candidates, or drafts only.
  Provider selection, activation, external mutation, and human approval remain
  outside the workspace.
- Document extraction and report rendering are future integration seams.
  Activation requires a reviewed contract, migration/coexistence plan,
  representative parity and security/licence evidence, a caller,
  rollback/recovery, and operator acceptance.
- Historical `CollisionSpike` names inside dated workspace evidence identify the
  predecessor only. Every current or future application integration contract
  targets Pegasus, and no workspace currently has a Pegasus adapter, caller,
  deployment, or acceptance.
- Dated AI evidence remains reachable through the Collision Brain
  [provider evaluation and first-party source register](ai-centre/services/collision-brain/docs/provider-evaluation.md)
  and the qualified [19 July 2026 sample-corpus inventory](ai-centre/ml-ops/reports/01-data-readiness/01-sample-corpus-inventory.md).
  Neither record selects a provider, authorises an experiment, or proves a model,
  caller, deployment, or acceptance.

Generated output, packages, caches, nested repository metadata and CI, private
datasets, local settings, copied corpora, sample case material, and model weights
remain excluded. Updating a source import requires a reviewed provenance change
and regenerated current manifest; never infer upstream acceptance from a local
build.
