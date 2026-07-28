# 8.12 Security, DPIA and Vendor Governance

## Purpose

Create a formal governance layer for personal data, AI-assisted processing, partner APIs, Box/Outlook integrations, external vendors and security controls.

## Why this matters

The system processes personal data, vehicle/claim details, images, emails and potentially risk indicators. The governance model must be established before high-risk analytics, external partner APIs or AI-assisted review are expanded.

## Step-by-step plan

### Step 1 — Map data and processing

1. List all personal data categories.
2. List all vehicle/claim data categories.
3. List source systems: Outlook, Box, EVA, Job Sheet, mapper, partner feeds, portal.
4. List AI-assisted processing steps.
5. List human decision points.
6. List processors/sub-processors and vendors.

### Step 2 — Complete a DPIA before high-risk functions

1. Complete a DPIA for AI-assisted extraction/review if personal data is processed.
2. Complete a specific DPIA section for risk indicators/fraud-like scoring.
3. Document necessity, proportionality and alternatives.
4. Document mitigation controls.
5. Review and update the DPIA when processing changes materially.

### Step 3 — Define role boundaries

1. AI may classify, extract, flag and draft.
2. AI may not make final fraud, liability, roadworthiness, valuation or expert-report conclusions.
3. Staff/engineers remain accountable for approvals and expert judgement.
4. Make this visible in UI labels and workflow gates.

### Step 4 — Secure secrets and credentials

1. Store API credentials in a secret manager or equivalent secured store.
2. Do not embed credentials in code, spreadsheets or provider configs.
3. Rotate credentials.
4. Limit access by role.
5. Redact secrets from logs.

### Step 5 — Secure APIs and portal features

1. Use strong authentication.
2. Enforce object-level access controls.
3. Rate-limit external APIs.
4. Validate all uploads and payloads.
5. Log access and changes.
6. Perform security review before partner API exposure.

### Step 6 — Define retention and archival rules

1. Define retention periods by file/case type.
2. Align Box/file storage retention with legal and business requirements.
3. Define cold storage/archive process.
4. Keep searchability without unnecessary data exposure.
5. Provide deletion/rectification workflows where required.

## Deliverables

- Data map.
- DPIA.
- Vendor/sub-processor register.
- Security control checklist.
- API security standard.
- Retention/archival policy.

## Acceptance criteria

- High-risk AI/risk-scoring functions do not go live without DPIA sign-off.
- External partner APIs follow a documented security standard.
- Secrets are not stored in code or spreadsheets.
- Human approval boundaries are explicit in the workflow.

## Risks and controls

| Risk | Control |
|---|---|
| High-risk processing without governance | DPIA gate before deployment. |
| Partner API exposes another client's data | Object-level access checks and tenancy tests. |
| Vendor terms permit unwanted data use | Vendor review and contractual constraints. |
| Security controls are treated as an afterthought | Build them into P0/P1 architecture. |

## Suggested priority

P0/P1. Required before external APIs, portals, analytics, AI scoring or broad automation.
