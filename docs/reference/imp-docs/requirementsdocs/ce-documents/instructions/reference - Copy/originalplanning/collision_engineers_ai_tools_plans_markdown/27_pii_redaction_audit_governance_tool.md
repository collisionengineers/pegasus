# PII Redaction, Audit, and Data Governance Tool

Generated: 2026-05-22

**Type:** Governance tool
**Priority:** Immediate

## Objective

Provide controls for personal data minimisation, redaction for training/evaluation, access logging, retention tagging, and audit evidence.

## Why it matters for Collision Engineers

Vehicle claims contain personal data, addresses, images, VRMs, phone numbers, emails, and sensitive claim context. AI processing and cloud OCR require a governance layer.

## Proposed shape

A governance service tags sensitive fields, redacts samples for training, logs tool access, manages retention states, and supports DPIA/vendor review requirements.

## Candidate tools / MCP methods / skill actions

- `detect_sensitive_fields(document_id)`
- `redact_for_training(document_id)`
- `redact_json_fields(work_item_id, policy)`
- `export_audit_trail(work_item_id)`
- `apply_retention_tag(work_item_id)`
- `list_external_ai_processing_runs(period)`

## Inputs

- Documents
- images
- canonical JSON
- audit events
- user/tool invocations
- policy config

## Outputs

- Redacted document/text
- PII inventory
- audit report
- retention tags
- third-party processing log

## Guardrails

- Do not rely on redaction without human spot checks for sensitive samples.
- Keep raw originals secured.
- Record which external services processed which files.
- Least privilege access.

## MVP implementation path

1. Define PII field inventory.
2. Add redaction for JSON and text first.
3. Add image redaction later if needed.
4. Add processing log for cloud OCR/LLM calls.

## Test / acceptance criteria

- Emails/phones/addresses redacted in sample text.
- Audit trail exports all major events.
- External processing log complete.
- Access test by role.

## Risks and open questions

- Image redaction is harder.
- Over-redaction may reduce training value.
- Policy decisions need owner/legal input.

## Project source basis

- context_pack_2/12_SECURITY_PRIVACY_AND_GOVERNANCE.md
- phase_ai_tools.md

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
