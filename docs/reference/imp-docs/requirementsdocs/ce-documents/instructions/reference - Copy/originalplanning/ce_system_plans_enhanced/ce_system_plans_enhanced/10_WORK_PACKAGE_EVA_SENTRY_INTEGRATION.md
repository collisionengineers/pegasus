# Work Package 10 - EVA/Sentry Integration

## Purpose

Create a controlled integration boundary for EVA/Sentry that reduces manual re-keying while preserving human review, auditability, and safe retry behaviour.

## Source files

- `EVA User Guide.pdf`
- `Sentry_API_Complete_Guide.md`
- `evaapidocs.pdf`
- `phase_new_system.md`
- `phase_bespoke_system.md`
- `collision_project_context_pack.zip` / `07_EVA_INTEGRATION.md`
- `Final Format Example 02.json`

## Integration principle

Build an EVA adapter. The rest of the platform should not call EVA endpoints directly. The adapter handles authentication, token refresh, payload mapping, validation, retries, error classification, logging, and idempotency.

## EVA/Sentry endpoints to support

Prioritise in this order:

1. Authentication: `/Connect/token`.
2. Instruction creation: `/Instruction/Inspection`.
3. Note submission: `/Note/SubmitNote`.
4. Location update: `/Claim/LocationUpdate`.
5. Claim update: `/Claim/Update`.
6. Authority status update: `/Claim/AuthorityStatusUpdate` if operationally needed.
7. Report submission: `/Report/SubmitReport` for Phase 6.
8. Report retrieval endpoints for reconciliation/visibility if needed.

## Step-by-step implementation

### Step 1 - Confirm integration mode

Decide one of:

- Direct API submission.
- EVA-ready JSON generation only.
- Manual import/export workflow.
- Robotic/manual fallback.

For Phase 2, it is acceptable to generate and validate an EVA-ready payload before enabling live submission.

### Step 2 - Build token manager

1. Store Client ID/Secret securely.
2. Request token using form-urlencoded auth endpoint.
3. Store token in memory only.
4. Refresh before expiry.
5. Never log secrets or full token.
6. Emit auth failure event without exposing credentials.

### Step 3 - Field mapping

Map canonical fields to EVA/Sentry fields. Draft mapping:

| Canonical field | EVA/Sentry field candidate | Notes |
|---|---|---|
| Work Provider / provider code | `RequestFrom`, `PrincipalName` or configured code | Confirm with vendor/settings. |
| VRM | `VehReg` | Normalize uppercase/no spaces unless EVA expects display spacing. |
| Reference | `ExternalRef` and/or `ClmNo` | Confirm which field matches CE/provider reference. |
| Claimant Name | `InsName` or claim/customer field | Confirm exact role. |
| Vehicle Model | `VehDesc` | May need truncation to max length. |
| Incident Date | `DtIncident` | Convert to API DateTime. |
| Inspection Address | `InspLocName/Add/Town/City/County/PCode` | Split carefully. |
| Accident Circumstances | `Cause` or `NotesStr` | Confirm field length. |
| VAT Status | `VatStat` or claim update VAT field | Accepted values vary: Yes/No/n%. |
| Mileage | report or notes field depending endpoint | Confirm endpoint support. |
| Mileage Unit | report or notes field depending endpoint | Confirm endpoint support. |

### Step 4 - Payload validator

Before submission, validate:

1. Required identifying combination present.
2. Field lengths fit documented limits.
3. Dates valid and API-formatted.
4. Accepted enumerations valid.
5. File attachments available and base64-encoded if needed.
6. No duplicate prior submission for same case unless user explicitly resubmits.
7. Payload has been reviewed/approved.

### Step 5 - Payload preview UI

Show:

- Summary fields.
- Raw JSON.
- Required/optional field validation.
- Source of each mapped field.
- Files attached.
- Warnings.
- Submit button gated by user permission.

### Step 6 - Submission and logging

On submit:

1. Generate idempotency key.
2. Store payload with version and status `pending`.
3. Call EVA adapter.
4. Store request metadata, status code, response body, response ID.
5. Update case state.
6. Emit audit event.
7. If success, store EVA reference/ID.
8. If failure, classify as retryable or terminal.

### Step 7 - Error classification

| Error | Classification | Action |
|---|---|---|
| Token expired | Retryable | Refresh token and retry. |
| Network timeout | Retryable | Backoff retry. |
| 400 invalid payload | Terminal until user fixes | Show validation error. |
| 401 unauthorized | Retryable then terminal | Refresh token once; alert if still failing. |
| 404 claim not found for update/note | Terminal or matching issue | Send to review. |
| 409 duplicate/conflict | Review required | Link to prior submission or resolve duplicate. |
| 500 server error | Retryable | Backoff and alert if repeated. |

### Step 8 - Phase 6 report submission

Only after instruction flow is stable:

1. Map engineer report fields.
2. Attach report file(s).
3. Map valuation, parts, impact image, and report text if required.
4. Add engineer identity/role controls.
5. Add strict preview and approval.
6. Reconcile EVA response.

## Acceptance criteria

1. EVA payload can be generated from a reviewed case.
2. Payload preview clearly shows all fields and warnings.
3. No payload can be submitted without Work Provider and required identifiers.
4. Token refresh is automatic and safe.
5. Every request and response is logged with correlation to case.
6. Duplicate submissions are prevented or explicitly confirmed.
7. Failures route to retry or review with clear reason.
