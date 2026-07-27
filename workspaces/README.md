# Pegasus source workspaces

These directories are source-only imports for independent maintenance, review, and validation. They are not projects in `Pegasus.slnx`, application callers, runtime services, package references, deployment units, or authority for Pegasus business policy.

## Imported sources

| Workspace | Durable role | Source provenance | Imported source manifest |
| --- | --- | --- | --- |
| `document-extraction/` | CollisionDocNet document/email extraction libraries and CLI | Local source snapshot `../collisiondocnetconverter`; no `.git` metadata was present, so branch, remote, and commit are unavailable | 259 files, 2,288,020 bytes, SHA-256 `b83f9d8df250ca754f8ce232d848855ddf0bba39690c5d8799763baa6140ecee` |
| `report-renderer/` | Deterministic CollisionRenderer report-rendering source | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collisionrenderer` | 154 files, 1,469,273 bytes, SHA-256 `a19b1a5f153d8839a6d24377e4b845cd9720be3813c1934ec26d1e418a59e7d8` |
| `ai-centre/` | AI model, agent, evaluation, training, and AI-service strategy | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `collision-ai-centre` | 111 files, 556,581 bytes, SHA-256 `2cedabcb0f63f657691a8c4d15b3891ff5c0fb1af2065a1b925576513a406046` |
| `agent-skills/` | Source skill packs and their pack-validation tools | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collision-agent-skills` | 172 files, 9,346,070 bytes, SHA-256 `9874a0cfbf7a0a2b42c0beff6d32ea5c7cc3caebba26850fadcc9728b4e8caa3` |

The manifest hash is computed over each imported relative path and byte payload in ordinal path order. It proves this import snapshot only; it is not an upstream commit identity or runtime acceptance evidence.

## Ownership and activation

- `Pegasus.Core` remains the sole owner of Pegasus business policy. Infrastructure, Web, and Worker remain the only application projects and composition roots.
- Workspace validation runs independently. The application build must not compile, reference, load, invoke, publish, or deploy workspace code.
- AI Centre owns model/provider experiments, evaluation and training strategy. It does not select a Pegasus AI provider, activate `Send to AI`, or own case, report, correspondence, valuation, or approval policy.
- Agent skills are source packages for independent review and pack validation. They are not autonomous application callers and cannot mutate Pegasus or external services.
- Document extraction and report rendering remain future library-integration seams. Activation requires an accepted contract, migration/coexistence plan, representative parity evidence, security/licence approval, an actual caller, rollback/recovery, and operator acceptance. Manual EVA handoff remains supported until each EVA function is independently replaced.

Generated output, packages, caches, source-repository metadata, private datasets, local settings, copied corpora, sample case material, and model weights are excluded. Updating an import requires a new reviewed provenance row and manifest; never copy a nested `.git` directory or infer upstream acceptance from a clean local build.
