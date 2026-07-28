# ML operations

This area holds the framework for locally controlled datasets, evaluation, training, promotion, and
model governance. It also preserves the existing AI/ML strategy and detailed report library.

## Layout

- `strategy/` — original strategic assessment and commercial/technical planning.
- `reports/` — detailed evidence-led opportunity, governance, and delivery reports.
- repository-root `corpus/ai-centre/` — immutable local development and ML-operations inputs; never repository content.
- `datasets/` — versioned dataset recipes, schemas, manifests, cards, and synthetic fixtures.
- `pipelines/` — deterministic ingest, extraction, deduplication, minimisation, and build code.
- `training/` — reproducible training entry points and configs.
- `evaluation/` — baselines, sealed-suite definitions, safety slices, and regression gates.
- `registry/` — portable promotion manifests and artifact hashes.

Management's recorded authorisation permits bounded development and evaluation use of approved
source material under `corpus/ai-centre/`; it does not permit committing the corpus or complete
Box/Outlook archives. Dataset recipes and manifests must preserve corpus custody, provenance, and
reproducibility. Track only model cards, configuration, and immutable artifact references here;
generated results belong under root `artifacts/`.

Start with [the data-use authorisation](../docs/governance/data-authorisation.md),
[development corpus contract](../docs/governance/development-mlops-corpus.md),
[data boundaries](../docs/governance/data-boundaries.md), and the
[workspace delivery plan](../PLAN.md).
