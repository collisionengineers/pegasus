# A.N.D.I.E Damage Review Workbench Extension

Generated: 2026-05-22

**Type:** Vision/LLM workbench
**Priority:** Later

## Objective

Develop the existing A.N.D.I.E concept into a controlled engineer-assistance workbench that organises photos, damage areas, severity prompts, and questions for review without issuing final conclusions.

## Why it matters for Collision Engineers

The handover lists an AI damage assessment platform (A.N.D.I.E) as WIP. The broader Phase 3 image and engineer-assistance tools can be assembled into this workbench after intake foundations are stable.

## Proposed shape

A workbench combines image schedule, damage-area detection, estimate flags, vehicle data, and engineer notes into a review surface. AI suggests prompts and descriptions; engineers approve technical wording.

## Candidate tools / MCP methods / skill actions

- `prepare_damage_review(work_item_id)`
- `suggest_damage_area_from_images(work_item_id)`
- `summarize_damage_photos(work_item_id)`
- `compare_estimate_to_visible_damage(work_item_id)`
- `draft_engineer_questions(work_item_id)`

## Inputs

- Vehicle images
- estimate summary
- instruction circumstances
- vehicle data
- engineer notes

## Outputs

- Damage review workspace
- image clusters
- review prompts
- draft non-final descriptions
- questions for engineer

## Guardrails

- No final liability, causation, roadworthiness, or fraud conclusion.
- Engineer approves all technical wording.
- AI output labelled as review prompt.
- Do not conceal uncertainty.

## MVP implementation path

1. Use image schedule and estimate QA outputs.
2. Create damage-area prompt set.
3. Add side-by-side photo/estimate view.
4. Add engineer approval/editing.

## Test / acceptance criteria

- Engineer can review images faster.
- Prompts are useful and not overconfident.
- No final conclusions generated.
- Audit of approved wording.

## Risks and open questions

- High risk if over-automated.
- Computer vision may miss damage.
- Needs substantial engineer calibration.

## Project source basis

- handover.docx
- phase_ai_tools.md
- EVA User Guide.pdf

## External reference basis

- OpenAI vision/image input documentation: https://developers.openai.com/api/docs/guides/images-vision
