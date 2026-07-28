# ABP Retail / Non-Contract Charge Review Assistant

Generated: 2026-05-22

**Type:** Knowledge skill + parser
**Priority:** Medium-Low

## Objective

Assist engineers/admin with ABP retail/non-contract charge checks by surfacing relevant categories, internal guidance, and evidence from estimates or disputes.

## Why it matters for Collision Engineers

The communication profile references ABP rates as a common evidential standard, and the context pack includes ABP charge-review concepts. This should support technical review, not replace it.

## Proposed shape

A skill reads a disputed estimate/invoice and maps charge lines to internal/ABP categories, then drafts a neutral explanation for human approval.

## Candidate tools / MCP methods / skill actions

- `parse_charge_lines(document_id)`
- `classify_charge_category(line_item)`
- `retrieve_abp_guidance(category)`
- `draft_charge_review_response(work_item_id)`
- `log_charge_decision(work_item_id, line_item, decision)`

## Inputs

- Estimate/invoice line items
- ABP guide/internal notes if licensed
- work provider context
- previous review decisions

## Outputs

- Charge category table
- review prompts
- draft rationale
- source references

## Guardrails

- Do not quote gated ABP material unless licensed/provided.
- Engineer signs off conclusions.
- Use evidence-based and neutral CE style.
- Keep source version/date.

## MVP implementation path

1. Start with internal guidance and non-gated categories.
2. Add line-item parser.
3. Add retrieval from approved ABP/internal documents.
4. Add draft response template.

## Test / acceptance criteria

- Known dispute maps to correct categories.
- Draft uses neutral CE tone.
- Source references included.
- No unsupported charge values invented.

## Risks and open questions

- ABP guide access/licensing.
- Charge terminology variation.
- Risk of overconfident advice.
- Needs engineer calibration.

## Project source basis

- CE Communication Style & Tone Profile.docx
- context_pack_1/11_audatex_abp_industry_context.md

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
