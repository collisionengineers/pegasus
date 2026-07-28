# salvage-categorisation

ABI Code of Practice salvage-category decision support — Cat A, B, S, and N — covering structural vs non-structural damage, high-voltage battery, fire, water, motorcycle, repairability, AQP judgement, and salvage value/rate disputes.

## What it provides

- **`SKILL.md`** — the categorisation workflow and dispute reasoning.
- **`references/`** — ABI CoP guidance and the category decision table.
- **`scripts/`** — salvage-category evaluation.
- **`tests/`** (in this `-dev` shell) — fixtures for the evaluator.

## Uses

`total-loss-assessment` (repair scope/economics), `vehicle-valuation` (where category or prior marker affects value), and `ce-house-style` (external dispute wording).

## Status

Production-ready; packaged in normal dist builds. Decision support, not an automatic oracle — allocation depends on evidence, current ABI CoP, and AQP judgement.

## Layout

`README.md` and `AGENTS.md` live in this `-dev` wrapper (never uploaded); the uploadable skill is the nested `salvage-categorisation/` folder. See `AGENTS.md` for maintenance notes.
