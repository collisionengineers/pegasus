# Case Summary and Status Skill

Generated: 2026-05-22

**Type:** Natural-language skill
**Priority:** Immediate

## Objective

Given a VRM, claim reference, or work-item ID, return a concise internal summary of case status, documents received, missing information, latest activity, deadlines, and next action.

## Why it matters for Collision Engineers

Staff need quick case awareness without opening the full job sheet, Box folder, EVA, and emails. The Phase 3 plan explicitly includes a case summary skill.

## Proposed shape

A retrieval skill calls the case data store, Box metadata, EVA status, and audit log, then produces a short structured summary.

## Candidate tools / MCP methods / skill actions

- `summarize_case(identifier)`
- `list_case_blockers(identifier)`
- `show_latest_activity(identifier)`
- `show_documents_received(identifier)`
- `next_best_action(identifier)`

## Inputs

- VRM
- claim reference
- work item ID
- case data store
- event log
- review status

## Outputs

- Status summary
- missing items
- received files
- latest actions
- suggested next step
- links to source systems

## Guardrails

- Internal only unless explicitly drafted for external recipient.
- Include uncertainty and missing data.
- Do not create final technical opinions.
- Use source links/IDs.

## MVP implementation path

1. Build tool calls into case store and Box/EVA adapters.
2. Template summary into sections.
3. Add Teams/portal/chat access.
4. Add permissions by user role.

## Test / acceptance criteria

- Summary matches known case state.
- Missing items correct.
- No hallucinated files/actions.
- Source links present.

## Risks and open questions

- Requires case data store.
- Permissions must prevent overbroad access.
- Old spreadsheet-only data may be incomplete.

## Project source basis

- phase_ai_tools.md
- context_pack_1/05_target_workflow.md
- context_pack_2/19_AGENT_HANDOFF_CONTEXT.md

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
