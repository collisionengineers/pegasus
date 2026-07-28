# 7.6 API for External Partners


## Purpose

Expose controlled parts of the Collision Engineers workflow to trusted external partners so they can submit instructions/files and query limited status without email.

## Step-by-step plan

### Step 1 — Define allowed API use cases

Start with:

1. Submit new instruction.
2. Upload files/images to an existing instruction.
3. Query limited case status.
4. Submit additional note.
5. Retrieve issued report link, if authorised.

Exclude initially:

- internal review flags;
- risk indicators;
- engineer notes;
- full audit log;
- cross-partner search;
- direct final report edits;
- unauthorised historical data access.

### Step 2 — Create partner API schema

1. Use canonical fields as the base.
2. Define required identifiers: partner ID, external reference, VRM, claim reference where available.
3. Define file model: filename, extension, content type, checksum and storage result.
4. Version the API from day one.

### Step 3 — Build security model

1. Partner-specific API credentials or OAuth client.
2. Per-partner scopes.
3. Object-level authorisation on every case and file.
4. Rate limits and upload limits.
5. Idempotency key required for submissions.
6. Audit logs for every request.

### Step 4 — Build validation and dedupe

1. Validate schema before work item creation.
2. Check duplicate external reference + VRM + partner.
3. Check file checksums.
4. Return clear errors without exposing unrelated records.

### Step 5 — Build sandbox and documentation

1. Create sandbox endpoint/environment.
2. Provide OpenAPI/Swagger documentation.
3. Provide test credentials.
4. Provide sample JSON payloads.
5. Create support process and SLA.

### Step 6 — Pilot with one partner

1. Select a technically capable partner.
2. Run in shadow mode against portal/email flow.
3. Review failures and duplicate controls.
4. Move to controlled production only after audit/security sign-off.

## Deliverables

- Partner API requirements.
- OpenAPI schema.
- Authentication/authorisation design.
- Sandbox API.
- Partner developer guide.
- API audit/reporting dashboard.

## Acceptance criteria

- Partner can submit a case and files using idempotency.
- Duplicate submissions do not create duplicate work items.
- Partner cannot access another partner’s case.
- API errors are clear and safe.
- Every request is logged with partner identity and correlation ID.

## Risks and controls

| Risk | Control |
|---|---|
| Broken object-level authorisation | Mandatory per-object access tests and code review. |
| Excessive data exposure | Return only external-safe fields. |
| Unbounded uploads | File limits, malware scanning and quotas. |
| Support load | Start with one partner and clear docs. |

## Suggested priority

P3. Build after the customer portal or after at least one high-value partner commits to integration.
