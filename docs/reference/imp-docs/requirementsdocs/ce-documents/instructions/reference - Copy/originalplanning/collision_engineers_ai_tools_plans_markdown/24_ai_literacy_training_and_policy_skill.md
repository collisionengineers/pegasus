# AI Literacy and Internal Training Skill

Generated: 2026-05-22

**Type:** Training skill/content module
**Priority:** Medium-Low

## Objective

Provide short interactive training for staff on when to trust AI outputs, how to review evidence, privacy boundaries, and how to escalate uncertain cases.

## Why it matters for Collision Engineers

The Phase 3 plan includes AI literacy modules. This is important because the system will produce confident-looking drafts and extracted fields that still require review.

## Proposed shape

A lightweight internal learning skill uses CE examples and quizzes. It can be embedded in the portal and linked from review UI warnings.

## Candidate tools / MCP methods / skill actions

- `start_training_module(topic)`
- `quiz_user(topic)`
- `explain_ai_limit(case_example)`
- `show_policy(topic)`
- `record_training_completion(user_id, module)`

## Inputs

- Training content
- CE examples
- policy snippets
- user ID

## Outputs

- Micro-lesson
- quiz result
- completion log
- policy acknowledgement

## Guardrails

- No gamified pressure to approve AI.
- Use CE-specific examples.
- Update when policy changes.
- Keep completion records if needed.

## MVP implementation path

1. Modules: extraction confidence, data privacy, valuation evidence, outbound drafts, human sign-off.
2. Add five-minute lessons.
3. Add quiz and acknowledgement.

## Test / acceptance criteria

- Staff complete pilot training.
- Quiz catches misunderstanding.
- Reviewers know how to inspect field evidence.
- Policy versions tracked.

## Risks and open questions

- Keeping content current.
- Training fatigue.
- Need manager ownership.

## Project source basis

- phase_ai_tools.md
- context_pack_2/12_SECURITY_PRIVACY_AND_GOVERNANCE.md

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
