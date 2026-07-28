# Engineer Pack Generator

Generated: 2026-05-22

**Type:** Skill + document generator
**Priority:** High

## Objective

Generate a structured internal engineer pack containing case facts, instruction summary, documents/images, estimate summary, missing/uncertain information, review flags, and questions for the engineer.

## Why it matters for Collision Engineers

The context packs define engineer pack generation as the MVP output. This aligns with the safest project positioning: AI prepares the pack; the engineer makes the judgment.

## Proposed shape

A skill assembles canonical data and evidence summaries into Markdown/HTML/PDF, with Box links and a clear separation between facts, flags, and suggested questions.

## Candidate tools / MCP methods / skill actions

- `generate_engineer_pack(work_item_id)`
- `preview_engineer_pack(work_item_id)`
- `export_pack_markdown(work_item_id)`
- `export_pack_pdf(work_item_id)`
- `lock_pack_after_review(work_item_id)`

## Inputs

- Canonical work item
- source document summaries
- image schedule
- estimate summary
- review flags
- audit log

## Outputs

- Engineer pack
- pack version
- source links
- missing-info section
- questions for engineer
- export files

## Guardrails

- No final opinion.
- Distinguish source facts from AI-generated summaries.
- Human review before issuing externally.
- Keep pack versions.

## MVP implementation path

1. Start with Markdown output.
2. Add HTML/PDF export.
3. Include image schedule and file links.
4. Add approval/lock state.
5. Feed later report drafting workflows.

## Test / acceptance criteria

- Pack contains all required sections.
- Source facts match canonical data.
- Unknowns are not hidden.
- Versioning works after edits.

## Risks and open questions

- Long documents may need summarisation safeguards.
- Source links must remain accessible.
- PDF formatting overhead.

## Project source basis

- context_pack_1/06_mvp_demo_case_intake_engineer_pack.md
- context_pack_1/10_ai_modules_and_prompts.md

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
