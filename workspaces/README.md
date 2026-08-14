# Pegasus source workspaces

These directories are independently buildable source imports. They are not
projects in `Pegasus.slnx`, application callers, runtime services, deployment
units, or owners of Pegasus business policy.

## Integration status register

| Workspace | Role | Integration status | Activation conditions | Owner |
| --- | --- | --- | --- | --- |
| `document-extraction/` | CollisionDocNet document/email extraction libraries and CLI | **Potential integration — significant development, testing, and evaluation required** | Accepted Core adapter contract; migration/coexistence plan; representative parity, security, and licence evidence; real caller; rollback/recovery; operator acceptance | [Workspace owner](document-extraction/README.md) |
| `report-renderer/` | Deterministic CollisionRenderer report-rendering source | **Planned integration — no Pegasus caller, deployment, or acceptance** | Accepted Core render contract; migration/coexistence plan; representative parity, security, and licence evidence; real caller; rollback/recovery; operator acceptance | [Workspace owner](report-renderer/README.md) |

## Source provenance

| Workspace | Source provenance | Imported source manifest |
| --- | --- | --- |
| `document-extraction/` | Local source snapshot `../collisiondocnetconverter`; no `.git` metadata was present, so branch, remote, and commit are unavailable | 202 files, 2,232,305 bytes, SHA-256 `e5d3bd118e567d54c2a793a0e75a4f3c528da62bd1caa9289f48297c9c96b5f2` |
| `report-renderer/` | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collisionrenderer` | 108 files, 604,228 bytes, SHA-256 `a3b9b665b23b08b9dd61276d48b9f3a3c551a005213225e7941d0adf6d504471` |

The manifest hashes each tracked Git index path in UTF-8 immediately followed
by its staged blob payload, in ordinal path order. A manifest proves source
identity, not application integration, deployment, or acceptance.

Each manifest describes the snapshot **at import time**. Current file counts
come from `git ls-files`; do not recalculate an import record merely because a
workspace later diverges.

## Ownership and activation

- `Pegasus.Core` owns every business rule and accepted case outcome. Web and
  Worker are the application composition roots; Infrastructure implements Core
  ports.
- Workspace validation is independent. Application build, publish, and deploy
  must not compile, reference, dynamically load, invoke, or package workspace
  code without a separately accepted contract and actual caller.
- Historical `CollisionSpike` names inside dated workspace evidence identify
  the predecessor only. Current or future application integration contracts
  target Pegasus.
- Generated output, packages, caches, nested repository metadata and CI,
  private datasets, local settings, copied corpora, sample case material, and
  model weights remain excluded. Updating a source import requires a reviewed
  provenance change; never infer upstream acceptance from a local build.
