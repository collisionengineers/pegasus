# Missing-Information Checker and Chaser Drafter

Generated: 2026-05-22

**Type:** Skill + deterministic checklist
**Priority:** Immediate

## Objective

Determine what prevents a case from becoming engineer-ready and draft CE-style chaser messages for missing images, estimates, instructions, mileage, address, or references.

## Why it matters for Collision Engineers

The current job sheet has “Missing” fields and formulas that draft image/estimate chasers. The Phase 3 plan also proposes chaser email/WhatsApp generation.

## Proposed shape

A deterministic checklist evaluates case type and required evidence. A writing skill drafts concise messages using CE tone and provider context, but staff approve/send.

## Candidate tools / MCP methods / skill actions

- `check_missing_information(work_item_id)`
- `generate_chaser_draft(work_item_id, channel)`
- `list_previous_chasers(work_item_id)`
- `suggest_recipient(work_item_id)`
- `mark_chaser_sent(work_item_id, channel, message_id)`

## Inputs

- Canonical work item
- documents/images present
- provider/principal rules
- previous chaser history
- CE tone profile

## Outputs

- Missing checklist
- blocking status
- draft email/WhatsApp
- recipient suggestion
- audit event

## Guardrails

- Do not send without approval.
- No emotional or accusatory language.
- No technical conclusions.
- Use short, clear CE phrasing.
- Include only necessary personal data.

## MVP implementation path

1. Implement checklist for standard desktop damage case.
2. Use job-sheet missing categories.
3. Generate email and WhatsApp variants.
4. Add approval and sent-history tracking.

## Test / acceptance criteria

- Known incomplete case lists correct blockers.
- Draft follows CE style.
- No missing item => no chaser.
- Sent message logged.

## Risks and open questions

- Provider-specific requirements vary.
- Recipient data may be incomplete.
- Chaser wording should not imply acceptance/liability.

## Project source basis

- phase_ai_tools.md
- CE Communication Style & Tone Profile.docx
- Backup of CE Job Sheet 260429.xlsm

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
