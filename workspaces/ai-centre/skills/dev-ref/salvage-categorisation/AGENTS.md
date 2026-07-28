# AGENTS.md — salvage-categorisation (dev wrapper)

Guidance for AI agents and developers maintaining the **salvage-categorisation** skill.

## What this is

Decision support for defensible salvage-category reasoning (ABI Code of Practice: Cat A/B/S/N). It supports judgement; it does not replace the appropriately qualified person's decision or current ABI CoP.

## Layout (wrapper vs upload)

```
salvage-categorisation-dev/            <- this dev shell — NEVER uploaded
  README.md   AGENTS.md   tests/
  salvage-categorisation/              <- the CLEAN skill = ships to cowork/Desktop
    SKILL.md
    references/   (ABI CoP guidance + category decision table)
    scripts/      (salvage-category evaluation)
```

## Hard rules

- **No automatic oracle.** Category allocation always depends on the evidence, current ABI CoP, and AQP judgement — never assert a category without that basis.
- Keep the decision table aligned with the current ABI Code of Practice.

## Dependencies

Invokes `total-loss-assessment`, `vehicle-valuation`, and `ce-house-style` at point of need (see `SKILL.md`).

## Shipping

Run the evaluator tests from this `-dev` shell.
Zips are built ONLY via `tools/pack_skill.py` (repo root).
