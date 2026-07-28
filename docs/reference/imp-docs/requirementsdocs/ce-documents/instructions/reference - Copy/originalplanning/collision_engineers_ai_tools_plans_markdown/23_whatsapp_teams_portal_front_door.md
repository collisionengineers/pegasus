# WhatsApp / Teams / Portal Front Door

Generated: 2026-05-22

**Type:** Channel interface + skills gateway
**Priority:** Medium

## Objective

Provide staff with a controlled chat/portal surface to invoke case summaries, chaser drafts, valuation explanations, and missing-info checks without exposing raw backend tools.

## Why it matters for Collision Engineers

The Phase 3 plan mentions Slack/Teams/internal portal. The project context shows WhatsApp is operationally common, but production WhatsApp automation requires care. Teams/internal portal is safer for staff-facing tools.

## Proposed shape

A front door maps natural-language requests into approved tools with permissions, confirmations, and audit.

## Candidate tools / MCP methods / skill actions

- `ask_case_question(identifier, question)`
- `draft_chaser_from_chat(identifier)`
- `summarize_missing_info(identifier)`
- `explain_valuation_from_chat(identifier)`
- `open_review_item(identifier)`

## Inputs

- Staff identity
- chat request
- case identifier
- approved tool outputs

## Outputs

- Structured response
- draft message
- review link
- audit event

## Guardrails

- No direct external sending without approval.
- User permissions enforced.
- No broad mailbox/Box browsing.
- Clarify ambiguous case identifiers.

## MVP implementation path

1. Start with internal portal or Teams bot.
2. Support case summary and missing-info first.
3. Add draft generation.
4. Consider WhatsApp Business only after governance.

## Test / acceptance criteria

- User can retrieve summary by VRM.
- Ambiguous VRM asks for selection.
- Draft generated but not sent.
- Audit logs user/tool invocation.

## Risks and open questions

- WhatsApp API/licensing.
- Identity mapping.
- Staff may expect the bot to perform unsafe actions.

## Project source basis

- phase_ai_tools.md
- handover.docx
- Collision Engineers Whiteboard.jam

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
