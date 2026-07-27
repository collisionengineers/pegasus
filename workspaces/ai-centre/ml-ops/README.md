# ML operations

This area holds the framework for locally controlled datasets, evaluation, training, promotion, and
model governance. It also preserves the existing AI/ML strategy and detailed report library.

## Layout

- `strategy/` — original strategic assessment and commercial/technical planning.
- `reports/` — detailed evidence-led opportunity, governance, and delivery reports.
- `data/private/raw/` — inspected source snapshot, authorised for use and repository inclusion.
- `datasets/` — versioned dataset recipes, schemas, manifests, cards, and synthetic fixtures.
- `pipelines/` — deterministic ingest, extraction, deduplication, minimisation, and build code.
- `training/` — reproducible training entry points and configs.
- `evaluation/` — baselines, sealed-suite definitions, safety slices, and regression gates.
- `registry/` — portable promotion manifests and artifact hashes.

Collision Engineers has authorised the current corpus and its complete Box and Outlook archives for
use, sharing, dataset construction, training, fine-tuning, and evaluation. Dataset recipes and
manifests must still preserve provenance and reproducibility. Model artifacts may be versioned here
when practical or referenced from an artifact registry by immutable hash.

Start with [the data-use authorisation](../docs/governance/data-authorisation.md),
[data boundaries](../docs/governance/data-boundaries.md), and the
[phased ML plan](reports/07-roadmap-and-pilots/02-phased-delivery-plan.md).
