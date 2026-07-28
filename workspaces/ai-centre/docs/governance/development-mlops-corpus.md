# Development and ML-operations corpus

- **Status:** Local development contract
- **Location:** repository-root `corpus/ai-centre/`
- **Git role:** ignored, immutable input; never repository content

AI Centre development and ML-operations work uses the approved local source snapshot from the repository-root `corpus/ai-centre/` subtree. This is the common path for evaluation, dataset inventory, extraction experiments and reproducible ML-operations pipelines; workspace-local `ml-ops/data/` paths are not used.

## Source mapping

| Former source location | Pegasus local corpus location |
| --- | --- |
| `ml-ops/data/private/raw/Reports-selected/` | `corpus/ai-centre/raw/Reports-selected/` |
| `ml-ops/data/private/raw/Documents/` | `corpus/ai-centre/raw/Documents/` |

References to a narrower case or knowledge-library path must be resolved beneath `corpus/ai-centre/`. Code and manifests should accept the corpus root as configuration rather than embed a workstation-specific absolute path.

## Use

The local snapshot may be read for development and ML-operations activities already covered by the recorded data-use authority, including inventory, extraction, deduplication, dataset construction, evaluation and approved model experiments. It is evidence input, not product authority or application state.

Raw files remain immutable. Derived datasets must be reproducible from versioned manifests and must be written outside `corpus/`, normally to root `artifacts/`. Tests and documentation use synthetic or redacted examples rather than copying operational content into tracked files.

Provisioning or refreshing `corpus/ai-centre/` is an owner-controlled local data operation. Repository automation must fail closed when a required corpus path or manifest is absent and must never download, commit, rename, rewrite or delete corpus content.
