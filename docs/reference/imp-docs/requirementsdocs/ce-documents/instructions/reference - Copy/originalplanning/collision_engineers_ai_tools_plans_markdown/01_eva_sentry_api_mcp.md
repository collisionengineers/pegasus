# EVA / Sentry API MCP

Generated: 2026-05-22

**Type:** MCP server + deterministic adapter
**Priority:** Immediate

## Objective

Expose EVA/Sentry actions as narrow, validated tools so agents and internal workflows can instruct claims, submit notes, update claims, submit reports, retrieve reports, and validate payloads without handling raw credentials or free-form API calls.

## Why it matters for Collision Engineers

The Sentry guide describes a JSON REST API with JWT auth, short token expiry, write endpoints for Instruction/Inspection, Claim updates, LocationUpdate, AuthorityStatusUpdate, SubmitNote, SubmitReport, and report retrieval. This is the cleanest route to move from manual EVA setup/import to controlled automation.

## Proposed shape

A server-side MCP wrapping an EVA client. The MCP receives canonical case data or a specific approved payload, validates it, injects auth, submits to EVA, stores the response, and returns a typed result. The agent never receives the client secret and cannot call arbitrary URLs.

## Candidate tools / MCP methods / skill actions

- `validate_eva_instruction_payload(payload)`
- `instruct_claim(work_item_id)`
- `submit_note(work_item_id, note, files?)`
- `update_claim_vat_or_excess(work_item_id, updates)`
- `submit_report(work_item_id, approved_report_payload)`
- `get_available_reports(filters)`
- `get_report(report_id)`
- `preflight_duplicate_check(vrm, claim_no, eva_ref?)`

## Inputs

- Canonical work item
- Approved EVA payload
- VRM
- claim number
- EVA ref
- attachments as Box/file IDs or base64 only when required

## Outputs

- EVA response model
- submission status
- EVA record ID where available
- validation errors
- stored request/response audit entry

## Guardrails

- Never submit to production EVA without a workflow approval flag until sandbox tests are proven.
- Token stored server-side only; refresh before expiry.
- Use idempotency checks even if EVA does not provide native idempotency.
- Preserve payload and response for audit.
- Agent can preview payload but cannot edit hidden auth or system fields.

## MVP implementation path

1. Build a Python/FastAPI EVA client from the Sentry guide.
2. Implement schema validation for Instruction/Inspection and SubmitReport first.
3. Wrap as MCP tools.
4. Add sandbox/test environment config.
5. Integrate with the review queue so approved canonical data generates a payload preview, then submits after approval.

## Test / acceptance criteria

- Unit tests for token refresh and schema validation.
- Sandbox submission test for one known instruction.
- Duplicate-prevention test.
- Bad-request routing test.
- Audit log assertion that request and response are stored.

## Risks and open questions

- EVA sandbox availability may be limited.
- Some fields may be conditionally required in practice beyond the guide.
- Duplicate detection may require EVA-side lookup support or a local idempotency table.

## Project source basis

- Sentry_API_Complete_Guide.md
- evaapidocs.pdf
- EVA User Guide.pdf
- context_pack_2/07_EVA_INTEGRATION.md

## External reference basis

- Model Context Protocol documentation: https://modelcontextprotocol.io/docs/getting-started/intro
- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
