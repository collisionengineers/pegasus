# Pegasus source workspaces

These directories are independently buildable source imports. They are not
projects in `Pegasus.slnx`, application callers, runtime services, deployment
units, or owners of Pegasus business policy.

## Current sources and provenance

| Workspace | Durable role and documentation | Source provenance | Imported source manifest |
| --- | --- | --- | --- |
| `document-extraction/` | CollisionDocNet document/email extraction libraries and CLI; [workspace owner](document-extraction/README.md) | Local source snapshot `../collisiondocnetconverter`; no `.git` metadata was present, so branch, remote, and commit are unavailable | 202 files, 2,283,131 bytes, SHA-256 `14ea5449c8267aa258d5f20c74f90c33f1d4653160126cece31e0d1e2ea5d040` |
| `report-renderer/` | Deterministic CollisionRenderer report-rendering source; [workspace owner](report-renderer/README.md) | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collisionrenderer` | 108 files, 605,734 bytes, SHA-256 `53ef2dc18b305ee22ddeb8188023ea2f4e9b1fbfecdeb477a7326cebfb846648` |
| `ai-centre/` | AI model, evaluation, training, provider, and AI-service experimentation; [workspace owner](ai-centre/README.md) | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `collision-ai-centre` | 70 files, 475,287 bytes, SHA-256 `f2e3a96d7a621d56305b07c64bf546e3a94cf31012345bc53fe61103acd90181` |
| `ai-centre/skills/` | Source skill packages and pack-validation tools; [package index](ai-centre/skills/README.md) | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collision-agent-skills` | 212 files, 3,042,785 bytes, SHA-256 `d43f3905a2a50b61a86a023dc4951843d0e212062972076b7f27683f616540e8` |

The manifest hashes each tracked file's UTF-8 relative path immediately followed
by its current byte payload, in ordinal path order. The AI Centre row excludes
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

Generated output, packages, caches, nested repository metadata and CI, private
datasets, local settings, copied corpora, sample case material, and model weights
remain excluded. Updating a source import requires a reviewed provenance change
and regenerated current manifest; never infer upstream acceptance from a local
build.
