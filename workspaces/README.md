# Pegasus source workspaces

These directories are independently buildable source imports. They are not
projects in `Pegasus.slnx`, application callers, runtime services, deployment
units, or owners of Pegasus business policy.

## Current sources and provenance

| Workspace | Durable role and documentation | Source provenance | Imported source manifest |
| --- | --- | --- | --- |
| `document-extraction/` | CollisionDocNet document/email extraction libraries and CLI; [workspace owner](document-extraction/README.md) | Local source snapshot `../collisiondocnetconverter`; no `.git` metadata was present, so branch, remote, and commit are unavailable | 202 files, 2,232,109 bytes, SHA-256 `857fba11192810507247721bd90178ce8d8d8fe82db54a98a46b8f87a43b297b` |
| `report-renderer/` | Deterministic CollisionRenderer report-rendering source; [workspace owner](report-renderer/README.md) | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collisionrenderer` | 108 files, 604,228 bytes, SHA-256 `a3b9b665b23b08b9dd61276d48b9f3a3c551a005213225e7941d0adf6d504471` |
| `ai-centre/` | AI model, evaluation, training, provider, and AI-service experimentation; [workspace owner](ai-centre/README.md) | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `collision-ai-centre` | 70 files, 464,422 bytes, SHA-256 `cdc6454be3eb7801440d0a94b4bf50ceba9c18609af534d838a43bdb56281b14` |
| `ai-centre/skills/` | Source skill packages and pack-validation tools; [package index](ai-centre/skills/README.md) | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collision-agent-skills` | 212 files, 3,024,035 bytes, SHA-256 `d8dafb8b791105804468f2ad13c664b37f42e326a38b933c35c6fee77ad059cd` |

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

Generated output, packages, caches, nested repository metadata and CI, private
datasets, local settings, copied corpora, sample case material, and model weights
remain excluded. Updating a source import requires a reviewed provenance change
and regenerated current manifest; never infer upstream acceptance from a local
build.
