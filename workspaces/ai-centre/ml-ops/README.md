# ML operations

This area holds the framework for locally controlled datasets, evaluation, training, promotion, and
model governance. It also preserves the existing AI/ML strategy and detailed report library.

## Layout

- `strategy/` — original strategic assessment and commercial/technical planning.
- `reports/` — detailed evidence-led opportunity, governance, and delivery reports.
- external ignored root `corpus/` — immutable private evaluation inputs; never repository content.
- `datasets/` — versioned dataset recipes, schemas, manifests, cards, and synthetic fixtures.
- `pipelines/` — deterministic ingest, extraction, deduplication, minimisation, and build code.
- `training/` — reproducible training entry points and configs.
- `evaluation/` — baselines, sealed-suite definitions, safety slices, and regression gates.
- `registry/` — portable promotion manifests and artifact hashes.

Management's historical authorisation permits bounded evaluation use of approved source material;
it does not permit importing the corpus or complete Box/Outlook archives. Dataset recipes and
manifests must preserve external custody, provenance, and reproducibility. Track only model cards,
configuration, and immutable artifact references here; generated results belong under root
`artifacts/`.

Start with [the data-use authorisation](../docs/governance/data-authorisation.md),
[data boundaries](../docs/governance/data-boundaries.md), and the
[workspace delivery plan](../PLAN.md).
