# Pegasus source workspaces

These directories are source-only imports for independent maintenance, review, and validation. They are not projects in `Pegasus.slnx`, application callers, runtime services, package references, deployment units, or authority for Pegasus business policy.

## Imported sources

| Workspace | Durable role | Source provenance | Imported source manifest |
| --- | --- | --- | --- |
| `document-extraction/` | CollisionDocNet document/email extraction libraries and CLI | Local source snapshot `../collisiondocnetconverter`; no `.git` metadata was present, so branch, remote, and commit are unavailable | 259 files, 2,272,746 bytes, SHA-256 `591bc1b2326476bd03076f5b47fc5e98884d7b3b2f9ed3cf295ef674a59504be` |
| `report-renderer/` | Deterministic CollisionRenderer report-rendering source | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collisionrenderer` | 112 files, 706,303 bytes, SHA-256 `097084e76ec2c3e029a506a3eb8211372e6d2920c4c0be72b45234058cef6887` |
| `ai-centre/` | AI model, agent, evaluation, training, and AI-service strategy | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `collision-ai-centre` | 143 files, 785,487 bytes, SHA-256 `f4ee10db78056277af497488b27658a1fb4cf74e28dfb2fc271c8522c63b378c` |
| `ai-centre/skills/` | Source skill packs and their pack-validation tools, colocated under their owning AI Centre workspace | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collision-agent-skills` | 224 files, 3,060,177 bytes, SHA-256 `ff3b5288204a703cb6eb4da898148dda7750b974812b32daac1d2049b66bc26e` |

The manifest hash is SHA-256 over each committed blob's UTF-8 relative path
immediately followed by its byte payload, in ordinal path order. AI Centre
excludes `skills/`, `ml-ops/data/`, nested `.github/`, caches, and build
outputs; the separately listed skills manifest also excludes nested `.github/`,
caches, and `assets/style-examples/` or `fixtures/style-examples/` sample
material. The source `ce-cost-defence.skill` archive is represented by its
extracted `ce-cost-defence/` payload, and source `_dev/` trees are represented
under `dev-ref/<skill-name>/`. The manifest proves the current committed import
snapshot only; it is not an upstream commit identity or runtime acceptance
evidence.

## Ownership and activation

- `Pegasus.Core` remains the sole owner of Pegasus business policy. Infrastructure, Web, and Worker remain the only application projects and composition roots.
- Workspace validation runs independently. The application build must not compile, reference, load, invoke, publish, or deploy workspace code.
- AI Centre owns model/provider experiments, evaluation and training strategy. It does not select a Pegasus AI provider, activate `Send to AI`, or own case, report, correspondence, valuation, or approval policy.
- Agent skills are source packages for independent review and pack validation. They are not autonomous application callers and cannot mutate Pegasus or external services.
- Document extraction and report rendering remain future library-integration seams. Activation requires an accepted contract, migration/coexistence plan, representative parity evidence, security/licence approval, an actual caller, rollback/recovery, and operator acceptance. Manual EVA handoff remains supported until each EVA function is independently replaced.

Generated output, packages, caches, source-repository metadata and nested CI workflows, private datasets, local settings, copied corpora, sample case material, and model weights are excluded. Updating an import requires a new reviewed provenance row and manifest; never copy a nested `.git` directory or infer upstream acceptance from a clean local build.
