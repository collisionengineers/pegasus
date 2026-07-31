# Pegasus source workspaces

These directories are independently buildable source imports. They are not
projects in `Pegasus.slnx`, application callers, runtime services, deployment
units, or owners of Pegasus business policy.

## Integration status register

The table below is the sole register for each workspace's role, current
integration status, activation conditions, and owner. Source identity is
recorded separately below and does not prove integration.

| Workspace | Role | Integration status | Activation conditions | Owner |
| --- | --- | --- | --- | --- |
| `document-extraction/` | CollisionDocNet document/email extraction libraries and CLI | **Potential integration — significant development, testing, and evaluation required** | Accepted Core adapter contract; migration/coexistence plan; representative parity, security, and licence evidence; real caller; rollback/recovery; operator acceptance | [Workspace owner](document-extraction/README.md) |
| `report-renderer/` | Deterministic CollisionRenderer report-rendering source | **Planned integration — no Pegasus caller, deployment, or acceptance** | Accepted Core render contract; migration/coexistence plan; representative parity, security, and licence evidence; real caller; rollback/recovery; operator acceptance | [Workspace owner](report-renderer/README.md) |
| `ai-centre/` | AI model, evaluation, training, provider, and AI-service experimentation | **Planned integration — no Pegasus caller, deployment, or acceptance** | Accepted AI integration contract owned by Core; governed proposal/review caller; evaluation, security, provider, deployment, rollback, and operator-acceptance evidence | [Workspace owner](ai-centre/README.md) |
| `ai-centre/skills/` | Application-facing AI-agent skill packages and pack-validation tools | **Application-facing agent skills — not repository-development workflow** | Separate application-skill integration contract, agent caller, evaluation, deployment, and human-approval evidence; never a repository-development authority | [Package index](ai-centre/skills/README.md) |

## Source provenance

| Workspace | Source provenance | Imported source manifest |
| --- | --- | --- |
| `document-extraction/` | Local source snapshot `../collisiondocnetconverter`; no `.git` metadata was present, so branch, remote, and commit are unavailable | 202 files, 2,232,305 bytes, SHA-256 `e5d3bd118e567d54c2a793a0e75a4f3c528da62bd1caa9289f48297c9c96b5f2` |
| `report-renderer/` | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collisionrenderer` | 108 files, 604,228 bytes, SHA-256 `a3b9b665b23b08b9dd61276d48b9f3a3c551a005213225e7941d0adf6d504471` |
| `ai-centre/` | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `collision-ai-centre` | 70 files, 464,490 bytes, SHA-256 `c3df715e8989e0129c8b1710ffe2f15f3142041544e8c578ee45b015e7ce002b` |
| `ai-centre/skills/` | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collision-agent-skills` | 212 files, 3,017,636 bytes, SHA-256 `1258fcbfd8b420c425e2f9b953c8dc0531b69730878012172bd33709ff01f9d5` |

The manifest hashes each tracked Git index path in UTF-8 immediately followed
by its staged blob payload, in ordinal path order. The AI Centre row excludes
`skills/`, `ml-ops/data/`, nested `.github/`, caches, and build outputs. The
skills row also excludes nested `.github/`, caches, and
`assets/style-examples/` or `fixtures/style-examples/`. Source `_dev/` trees are
represented under `dev-ref/<skill-name>/`. A manifest proves source identity,
not application integration, deployment, or acceptance.

## Ownership and activation

The register above is the sole workspace integration-status authority. Local
workspace READMEs retain implementation, build, test, package, and evidence
details and link back to their row rather than restating integration status.

- `Pegasus.Core` owns every business rule and accepted case outcome.
  Infrastructure, Web, and Worker are the application composition roots.
- Workspace validation is independent. Application build, publish, and deploy
  must not compile, reference, dynamically load, invoke, or package workspace
  code without the separately accepted contract and actual caller recorded in
  the register.
- AI Centre and skill packages may produce evidence, candidates, or drafts only.
  Provider selection, activation, external mutation, and human approval remain
  outside the workspace.
- Historical `CollisionSpike` names inside dated workspace evidence identify the
  predecessor only. Current or future application integration contracts target
  Pegasus, and no workspace currently has a Pegasus adapter, caller,
  deployment, or acceptance.
- Dated AI evidence remains reachable through the Collision Brain
  [provider evaluation and first-party source register](ai-centre/services/collision-brain/docs/provider-evaluation.md)
  and the qualified [19 July 2026 sample-corpus inventory](ai-centre/ml-ops/reports/01-data-readiness/01-sample-corpus-inventory.md).
  Neither record selects a provider, authorises an experiment, or proves a
  model, caller, deployment, or acceptance.

Generated output, packages, caches, nested repository metadata and CI, private
datasets, local settings, copied corpora, sample case material, and model weights
remain excluded. Updating a source import requires a reviewed provenance change
and regenerated current manifest; never infer upstream acceptance from a local
build.
