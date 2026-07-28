# Security, Governance and Audit Controls

## Why this matters

Collision Engineers’ work is insurance/legal-facing and contains personal data, vehicle data, images, valuation evidence, correspondence and expert conclusions. AI output must be defensible, reviewable and source-grounded.

## Principles

1. Preserve original evidence unchanged.
2. Record every important action.
3. Keep engineer judgement under engineer control.
4. Keep external communications reviewed unless an explicit allow-list policy exists.
5. Keep AI outputs labelled and traceable.
6. Use least-privilege access for mailboxes, files, cases and tools.
7. Separate internal drafts from external sent outputs.

## Audit event model

```yaml
audit_event:
  id: string
  case_id: string
  actor_type: user | automation | ai_agent | external_system
  actor_id: string
  event_type: file_imported | field_extracted | ai_draft_created | user_edited | task_created | email_drafted | email_sent | api_submitted | approval_recorded | status_changed | error
  timestamp: datetime
  source_ids: [string]
  tool_name: string | null
  input_summary: string
  output_summary: string
  confidence: number | null
  approved_by: string | null
  external_action: boolean
  raw_request_id: string | null
  raw_response_id: string | null
```

## Human approval requirements

Always require human approval for:

- final report issue;
- report submission to EVA/Sentry;
- valuation conclusion;
- causation/roadworthiness/fraud/liability conclusion;
- disputed technical response;
- case merge/split;
- deletion or permanent archive of evidence;
- external communications outside approved templates.

## AI output labels

Every AI-generated item should have a visible status:

- `draft_ai_generated`
- `reviewed_by_admin`
- `reviewed_by_engineer`
- `edited_after_ai`
- `approved_for_send`
- `approved_for_report`
- `rejected`

## Data protection controls

- Complete a DPIA before processing live case data through new AI services.
- Confirm vendor data retention, model-training opt-out, data residency, subprocessors and support access.
- Minimise prompts: pass only data needed for the task.
- Avoid uploading unnecessary images, personal addresses or correspondence to broad/general AI tools.
- Encrypt secrets and API tokens; never store EVA/Sentry credentials in plain text.
- Maintain deletion/retention rules by case type and client requirement.

## Test controls

- Red-team prompts that ask the agent to send an email, change final valuation, delete evidence, or invent a report conclusion.
- Verify refusal/approval gating.
- Verify audit logs capture every tool call.
- Verify source links exist for draft factual statements.
- Verify low-confidence extraction is routed to review.
