# Audatex Estimate Parser and QA Assistant

Generated: 2026-05-22

**Type:** Parser + review assistant
**Priority:** Medium

## Objective

Parse Audatex/repair estimates and flag review prompts such as ADAS, alignment, SRS, supplements, unusual labour/paint/parts values, and total-loss indicators.

## Why it matters for Collision Engineers

Audatex appears repeatedly in the project context and invoice templates. Estimate parsing is a strong automation target, but the assistant should prompt engineer review rather than decide repair validity.

## Proposed shape

A parser extracts estimate fields and line items. A QA assistant checks deterministic keyword/line-item rules and explains why an engineer should review a point.

## Candidate tools / MCP methods / skill actions

- `parse_estimate(document_id)`
- `extract_estimate_totals(document_id)`
- `detect_supplementary_estimate(document_id)`
- `flag_adas_alignment_srs(document_id)`
- `compare_estimate_to_instruction(work_item_id)`
- `summarize_estimate_for_pack(work_item_id)`

## Inputs

- Audatex/repair estimate PDF
- instruction fields
- vehicle data
- repairer details

## Outputs

- Estimate totals
- line-item groups
- supplement marker
- review flags
- estimate summary

## Guardrails

- Do not override Audatex calculations.
- Do not assert estimate is correct/incorrect without engineer approval.
- Use review-prompt language.
- Respect Audatex licensing/API routes.

## MVP implementation path

1. Create parser for common Audatex PDFs.
2. Extract totals and key line-item words.
3. Add flags for ADAS/calibration, alignment, SRS, manual misc lines, high estimate value.
4. Include in engineer pack.

## Test / acceptance criteria

- Estimate totals match manual labels.
- Review flags trigger on known samples.
- False positives measured.
- Mismatch with instruction VRM triggers review.

## Risks and open questions

- Audatex formats vary.
- Tables may extract poorly.
- Licence/access restrictions.
- Manual line-item interpretation requires engineering context.

## Project source basis

- context_pack_1/10_ai_modules_and_prompts.md
- Standard Audatex Invoice.docx
- Sentry_API_Complete_Guide.md

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
