---
name: salvage-categorisation
description: Provides ABI Code of Practice salvage category decision support for Cat A, Cat B, Cat S, and Cat N, including structural/non-structural damage, high-voltage battery, fire, water, motorcycle, repairability, AQP, salvage value/rate disputes, and category-dispute reasoning. Use when the user asks for salvage category, write-off category, Cat S/Cat N/Cat B/Cat A, AQP support, structural versus non-structural categorisation, salvage value, salvage rate, contracted salvage rate, a TPI imposing salvage, or a response to a salvage-category or salvage-rate challenge.
---

# Salvage Categorisation

## Overview

Support defensible salvage category reasoning for Collision Engineers. This skill is decision
support, not an automatic oracle: category allocation depends on the evidence, current ABI Code
practice, and appropriate qualified person judgement.

Use `total-loss-assessment` for repair-scope and repair-economics evidence. Use `vehicle-valuation`
where the category or prior marker affects value. Use `ce-house-style` for external dispute wording
after the category reasoning is complete.

## Workflow

1. Identify the vehicle, incident, damage evidence, photos, estimate, repairability decision, and any
   proposed category.
2. Separate **repair economics** from **salvage category**. A vehicle can be uneconomic to repair
   without the category being decided by cost alone.
3. Read `references/salvage-decision-table.v1.json` and, where useful, run
   `python scripts/evaluate_salvage_category.py input.json` to check the structured category path.
4. Read `references/abi-category-decision-tree.md` and make a provisional category path.
5. Read `references/structural-non-structural-checklist.md` for any Cat S/Cat N distinction.
6. Read `references/hv-battery-cases.md` for BEV/PHEV/hybrid damage, quarantine, water, fire, or HV
   battery uncertainty.
7. Read `references/fire-water-motorcycle-cases.md` for fire, smoke, flood, contamination, and
   motorcycle-specific issues.
8. Read `references/aqp-boundaries.md` before presenting the answer. State missing evidence where
   the category cannot be finalised.
9. If drafting an external response, read `references/query-response-boundaries.md` and then use
   `ce-house-style` for tone and structure.

## Output

Provide:

- provisional or final category recommendation
- evidence supporting the category
- evidence against alternatives
- missing evidence or inspection needed
- repair-economics note if relevant
- safe external wording if requested

## Boundaries

- Do not decide category from repair cost alone.
- Do not copy ABI Code text wholesale or reproduce matrices verbatim.
- Do not treat the AQP questionnaire as a categorisation method; it is competency/training
  background.
- Do not finalise a category where structural, HV, water, fire, or motorcycle evidence is missing.

## References

- `references/abi-category-decision-tree.md` - category path and evidence tests
- `references/salvage-decision-table.v1.json` - structured versioned decision inputs and rules
- `references/structural-non-structural-checklist.md` - Cat S/Cat N distinction
- `references/hv-battery-cases.md` - BEV/PHEV/hybrid and HV battery issues
- `references/fire-water-motorcycle-cases.md` - fire, water, smoke, and motorcycles
- `references/aqp-boundaries.md` - qualified-person and evidence boundaries
- `references/query-response-boundaries.md` - safe external response framing
