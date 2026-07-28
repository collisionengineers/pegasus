# CE Communication Style Skill

Generated: 2026-05-22

**Type:** Reusable writing skill
**Priority:** Immediate

## Objective

Create a reusable writing layer for all generated messages so report delivery, chasers, clarifications, disputes, and internal notes follow Collision Engineers’ established tone.

## Why it matters for Collision Engineers

The communication profile is unusually actionable. It defines direct greetings, short report delivery style, factual dispute handling, and avoided language. This should be a shared skill used by every drafting tool.

## Proposed shape

A style-normalisation skill takes a factual draft and rewrites it according to CE tone constraints, preserving facts and source references.

## Candidate tools / MCP methods / skill actions

- `rewrite_in_ce_style(draft, purpose)`
- `draft_report_delivery(recipient, attachment_type)`
- `draft_clarification_response(facts, rationale)`
- `draft_chaser(missing_items)`
- `tone_check(message)`

## Inputs

- Draft text
- purpose
- audience
- facts
- source evidence
- preferred sign-off

## Outputs

- CE-style draft
- tone warnings
- facts preserved
- approval status

## Guardrails

- Do not add facts.
- No excessive apologies, sales language, emojis, or emotive wording.
- Use approval before external send.
- Keep concise unless justification is needed.

## MVP implementation path

1. Convert style profile into prompt/instruction pack.
2. Add reusable skill wrapper.
3. Test on real sent-email examples if available.
4. Integrate with chaser, valuation, query-response, and report-delivery skills.

## Test / acceptance criteria

- Golden examples match tone.
- No factual additions.
- Tone check catches emotional/sales phrasing.
- Staff approval rate.

## Risks and open questions

- Over-standardisation could sound robotic.
- Context-specific nuance.
- Need examples for calibration.

## Project source basis

- CE Communication Style & Tone Profile.docx
- phase_ai_tools.md

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
