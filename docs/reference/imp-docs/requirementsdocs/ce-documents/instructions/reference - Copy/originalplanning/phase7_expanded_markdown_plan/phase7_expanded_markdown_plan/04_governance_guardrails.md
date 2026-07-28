# Governance Guardrails for Phase 7

## Core policy

Phase 7 should expand automation while preserving the central Collision Engineers principle: technical opinions, fraud conclusions, roadworthiness conclusions, valuation conclusions and final reports remain human-owned.

## Mandatory human approval points

Human approval should remain mandatory for:

1. Low-confidence evidence matches.
2. Case merge/split decisions.
3. Any adverse risk/fraud interpretation.
4. Engineer pack lock/finalise step.
5. Final report issue.
6. External chaser/status message sending, unless a low-risk template has explicit approval.
7. Any EVA import when required fields are missing, conflicting or inferred.

## AI output wording rules

Use:

- “requires review”;
- “possible mismatch”;
- “possible duplicate”;
- “field not found”;
- “evidence missing”;
- “confidence below threshold”;
- “risk indicator present”.

Avoid:

- “fraudulent”;
- “fake”;
- “invalid claim”;
- “unroadworthy”;
- “repair unreasonable”;
- “liability established”;
- “expert conclusion”.

## Data protection controls

Before live operation of high-risk modules:

1. Identify personal data types in instructions, images, emails and extracted JSON.
2. Document lawful basis and purpose limitation.
3. Minimise fields extracted and logged.
4. Decide retention periods for source files, extracted data, AI prompts, outputs and logs.
5. Complete a DPIA or equivalent privacy assessment where required.
6. Review third-party AI/OCR/vendor terms and data retention.
7. Keep production/test/demo data separated.

## API security controls

Any partner-facing API or portal must include:

- partner identity and authentication;
- object-level authorisation per case/partner;
- least-privilege scopes;
- rate limits;
- request/response audit logs;
- schema validation;
- upload malware/type/size controls;
- idempotency keys;
- error responses that do not leak unrelated case data;
- key rotation and deactivation process.
