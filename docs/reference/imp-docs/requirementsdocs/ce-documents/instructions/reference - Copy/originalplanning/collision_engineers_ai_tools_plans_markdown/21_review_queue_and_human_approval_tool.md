# Review Queue and Human Approval Tool

Generated: 2026-05-22

**Type:** Workflow UI/tool
**Priority:** Immediate

## Objective

Create the controlled surface where staff approve extracted fields, evidence matches, chaser drafts, valuation choices, and EVA submissions.

## Why it matters for Collision Engineers

Human oversight is the central safety boundary across the context packs. Without a review queue, automation either remains a black box or cannot safely progress from extraction to EVA.

## Proposed shape

A review UI displays work-item state, field evidence, confidence, flags, suggested actions, and approve/reject/correct controls. Every decision writes an audit event.

## Candidate tools / MCP methods / skill actions

- `list_review_items(filters)`
- `get_review_item(work_item_id)`
- `approve_field(field_id)`
- `correct_field(field_id, value)`
- `approve_evidence_match(match_id)`
- `approve_eva_submission(work_item_id)`
- `approve_draft_message(draft_id)`

## Inputs

- Work item
- field evidence
- match suggestions
- draft messages
- payload previews

## Outputs

- Reviewer decision
- corrected field value
- approval timestamp
- audit event
- new workflow state

## Guardrails

- Reviewer identity required.
- High-impact steps need explicit approval.
- No hidden auto-submission in pilot.
- Corrections become labelled data.
- Original source values remain visible.

## MVP implementation path

1. Build review page for extracted fields and missing items.
2. Add approve/correct actions.
3. Add EVA payload preview approval.
4. Add chaser draft approval.
5. Add dashboard counts.

## Test / acceptance criteria

- Reviewer can correct a field and audit logs show before/after.
- Approved payload submits once.
- Rejected match returns to unmatched queue.
- No action without permission.

## Risks and open questions

- Staff adoption.
- Too many low-confidence items if extraction poor.
- UI complexity.

## Project source basis

- context_pack_2/11_EXCEPTION_HANDLING_AND_HUMAN_REVIEW.md
- context_pack_1/05_target_workflow.md

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
