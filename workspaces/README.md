# Pegasus source workspaces

These directories are source-only imports for independent maintenance, review, and validation. They are not projects in `Pegasus.slnx`, application callers, runtime services, package references, deployment units, or authority for Pegasus business policy.

## Imported sources

| Workspace | Durable role | Source provenance | Imported source manifest |
| --- | --- | --- | --- |
| `document-extraction/` | CollisionDocNet document/email extraction libraries and CLI | Local source snapshot `../collisiondocnetconverter`; no `.git` metadata was present, so branch, remote, and commit are unavailable | 259 files, 2,269,899 bytes, SHA-256 `33dc6843164c3ff5d2cc2d98828cc41380297660b13976281bb6aefe048a6190` |
| `report-renderer/` | Deterministic CollisionRenderer report-rendering source | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collisionrenderer` | 151 files, 1,466,968 bytes, SHA-256 `ba35495e807e253165658834345f32390d6e960c93cbc209733e2b0b6b1716c9` |
| `ai-centre/` | AI model, agent, evaluation, training, and AI-service strategy | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `collision-ai-centre` | 141 files, 774,932 bytes, SHA-256 `eaa397007d67335f2ed269fd69671901aefeb85152b4d71bea90a817379a4975` |
| `ai-centre/skills/` | Source skill packs and their pack-validation tools, colocated under their owning AI Centre workspace | `collisionengineers/collisionsuite`, branch `main`, commit `acd3b0c28b59b60cfdbd8504daf0f5e8603bb59d`, path `active/collision-agent-skills` | 180 files, 2,864,936 bytes, SHA-256 `7126bf0c21c4c22fc8cc531c307cd31fe33913eb813632f27cfa2a357741027a` |

The manifest hash is SHA-256 over each UTF-8 relative path immediately followed by its byte payload,
in ordinal path order. AI Centre excludes `skills/`, `ml-ops/data/`, nested `.github/`, caches,
and build outputs; the separately listed skills manifest also excludes nested `.github/`, caches,
and removed `assets/style-examples/` or `fixtures/style-examples/` sample material. It proves this
import snapshot only; it is not an upstream commit identity or runtime acceptance evidence.

## Ownership and activation

- `Pegasus.Core` remains the sole owner of Pegasus business policy. Infrastructure, Web, and Worker remain the only application projects and composition roots.
- Workspace validation runs independently. The application build must not compile, reference, load, invoke, publish, or deploy workspace code.
- AI Centre owns model/provider experiments, evaluation and training strategy. It does not select a Pegasus AI provider, activate `Send to AI`, or own case, report, correspondence, valuation, or approval policy.
- Agent skills are source packages for independent review and pack validation. They are not autonomous application callers and cannot mutate Pegasus or external services.
- Document extraction and report rendering remain future library-integration seams. Activation requires an accepted contract, migration/coexistence plan, representative parity evidence, security/licence approval, an actual caller, rollback/recovery, and operator acceptance. Manual EVA handoff remains supported until each EVA function is independently replaced.

Generated output, packages, caches, source-repository metadata and nested CI workflows, private datasets, local settings, copied corpora, sample case material, and model weights are excluded. Updating an import requires a new reviewed provenance row and manifest; never copy a nested `.git` directory or infer upstream acceptance from a clean local build.
