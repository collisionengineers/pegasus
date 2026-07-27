# Autotrader Valuation Agent Skill

Generated: 2026-05-22

**Type:** Skill + tool adapter
**Priority:** Immediate

## Objective

Turn the Autotrader valuation proof of concept into a repeatable, evidence-backed valuation workflow with standard and uplift modes.

## Why it matters for Collision Engineers

Valuation is one of the most commercially useful Phase 3 AI functions. EVA includes valuation fields such as market, retail, trade, salvage, private sale, mileage/condition adjustments, and valuation research. The EVA user guide also requires staff to enter valuation information using vehicle data and mileage.

## Proposed shape

A skill calls vehicle intelligence first, queries Autotrader or valuation data sources, extracts comparable listings, computes a defensible range, stores evidence, and produces an approval-ready valuation note.

## Candidate tools / MCP methods / skill actions

- `run_standard_valuation(vrm, mileage, incident_date, region?)`
- `run_uplift_valuation(vrm, mileage, incident_date, uplift_basis)`
- `compare_listings(listing_set)`
- `create_valuation_evidence_report(work_item_id)`
- `write_eva_valuation_fields(work_item_id, approved_value_set)`

## Inputs

- VRM
- make/model/year
- mileage/unit
- incident date
- condition flags
- region radius
- valuation mode

## Outputs

- Standard valuation
- uplift valuation
- comparable listing table
- mean/median/percentiles
- excluded outliers
- narrative explanation
- EVA valuation payload fields

## Guardrails

- Never auto-approve uplift.
- Keep listing URLs/screenshots/raw data where terms permit.
- Disclose if mileage is estimated.
- Do not use stale listing evidence without timestamp.
- Separate market evidence from expert conclusion.

## MVP implementation path

1. Define listing query template.
2. Implement listing normalisation.
3. Add outlier filter and percentile range.
4. Produce Markdown evidence report.
5. Add approval gate before EVA fields are written.

## Test / acceptance criteria

- Same VRM query produces comparable list.
- Outlier exclusion is logged.
- Uplift explanation references actual evidence.
- EVA payload maps to ValMarket/ValRetail/ValTrade/ValResearch appropriately.

## Risks and open questions

- Autotrader connector limitations/terms.
- Sparse rare-vehicle comparables.
- Write-off/adverse history not covered by listings alone.
- Market volatility.

## Project source basis

- phase_ai_tools.md
- evaapidocs.pdf
- Sentry_API_Complete_Guide.md
- EVA User Guide.pdf

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
