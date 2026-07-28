# 8.5 EVA/Sentry API Adapter and Import Control

## Purpose

Create a controlled adapter between the internal work item model and EVA/Sentry API endpoints, replacing manual EVA entry where the API supports the required flow.

## Why this matters

The EVA/Sentry documentation shows JSON-based endpoints for instructions, notes, claim updates, location updates, authority updates, report submission and report retrieval. A dedicated adapter keeps this logic isolated, testable and auditable.

## Step-by-step plan

### Step 1 — Map canonical fields to EVA fields

1. Work Provider → RequestFrom/principal mapping via provider config.
2. VRM → VehReg.
3. Reference → ExternalRef, ClmNo or other provider-specific reference field.
4. Vehicle Model → VehDesc where appropriate.
5. Incident Date → DtIncident.
6. Inspection Address → inspection or claim location fields depending on context.
7. VAT Status → VatStat/ClmVatStat where appropriate.
8. Mileage/Mileage Unit → report or note fields if supported by the selected endpoint.
9. Accident Circumstances → Cause/NotesStr where appropriate.

### Step 2 — Implement authentication safely

1. Store client ID and client secret outside code.
2. Request tokens through the documented token endpoint.
3. Treat the five-minute token expiry as a normal operating constraint.
4. Refresh before expiry with a safety buffer.
5. Never store bearer tokens in logs.

### Step 3 — Build endpoint-specific clients

1. Instruction client for creating new instructions.
2. Location update client for inspection/repairer/claimant locations.
3. Note client for safe status/message posting.
4. Claim update client for excess/VAT where required.
5. Report submission client if/when report output automation is approved.
6. Report retrieval client for released report list and file retrieval.

### Step 4 — Add validation before submission

1. Confirm required provider/EVA codes.
2. Confirm VRM and reference.
3. Confirm date format.
4. Confirm address line lengths and required postcode fields.
5. Confirm attachment size/type/base64 rules.
6. Confirm only approved fields are submitted automatically.

### Step 5 — Add idempotency and retry controls

1. Use internal work item ID + provider reference + VRM as an idempotency basis.
2. Detect duplicate/conflict responses.
3. Retry only retryable errors.
4. Do not retry blindly on bad request/validation failures.
5. Preserve request/response summaries for audit without leaking tokens.

### Step 6 — Start with assisted import

1. Generate the EVA payload.
2. Show it to the reviewer.
3. Let the reviewer submit from the system.
4. Log the user who approved submission.
5. Move to fully automated submission only for stable provider/config combinations.

## Deliverables

- EVA field mapping document.
- Sentry API adapter.
- Secure credential handling.
- Submission validation layer.
- Request/response audit logging.
- Assisted-import review screen.

## Acceptance criteria

- A reviewed case can be submitted to the appropriate EVA endpoint without manual re-keying.
- Failed submissions are classified and routed to review.
- Tokens and secrets are not exposed in logs or UI.
- Duplicate submissions are prevented.

## Risks and controls

| Risk | Control |
|---|---|
| Wrong endpoint or field mapping | Assisted submission and provider-specific config tests. |
| Duplicate EVA instructions | Idempotency keys and response checking. |
| Token leakage | Secret vault, redacted logs and short-lived in-memory tokens. |
| API contract changes | Version adapter and keep sandbox tests. |

## Suggested priority

P1 once the state store, provider library and review queue exist.
