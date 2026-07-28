# Valuation Explanation and Dispute Response Skill

Generated: 2026-05-22

**Type:** Writing skill + evidence retriever
**Priority:** High

## Objective

Draft CE-style explanations for valuations, uplifts, or disputes using approved valuation evidence, market comparables, DVLA/DVSA data, and engineer-approved values.

## Why it matters for Collision Engineers

The Phase 3 plan includes “Explain valuation.” The communication style profile emphasises independent, factual, evidence-based, non-emotive reasoning. This skill can reduce drafting time while preserving human sign-off.

## Proposed shape

A writing skill retrieves valuation evidence, listing statistics, vehicle history, and approved value fields, then produces internal or external draft wording with citations to the companion valuation report.

## Candidate tools / MCP methods / skill actions

- `draft_valuation_explanation(work_item_id, audience)`
- `draft_valuation_dispute_response(work_item_id, dispute_points)`
- `summarize_comparable_listings(work_item_id)`
- `insert_ce_style(plain_draft)`
- `flag_missing_valuation_evidence(work_item_id)`

## Inputs

- Approved valuation evidence
- market listing report
- DVLA/DVSA data
- vehicle condition/mileage notes
- dispute email/body

## Outputs

- Draft explanation
- source evidence list
- missing evidence flags
- suggested attachments

## Guardrails

- Do not invent comparables.
- Do not approve uplift automatically.
- Use calm CE tone.
- Do not imply independence if sources are insufficient.
- Engineer/manager approval before sending.

## MVP implementation path

1. Use valuation evidence report as source.
2. Create two templates: short explanation and dispute response.
3. Add source checklist.
4. Add staff approval flow.

## Test / acceptance criteria

- Draft references actual listing/evidence data.
- Tone matches CE profile.
- No unsupported claims.
- Dispute points answered or flagged missing.

## Risks and open questions

- Sparse evidence makes draft weak.
- Market data freshness.
- Legal sensitivity in disputes.

## Project source basis

- phase_ai_tools.md
- CE Communication Style & Tone Profile.docx
- Sentry_API_Complete_Guide.md

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
