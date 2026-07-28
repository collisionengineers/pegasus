# Engineer Support, Report Drafting and EVA/Sentry Integration

## Objective

Support engineers and admins with structured briefings, source-grounded summaries and draft report payloads, without automating technical judgement or final sign-off.

## Split the original Engineer Support Agent into three modules

### 1. Engineer Pack Automation

Generates a structured pack when a case is ready:

- instruction summary;
- claimant/insured and VRM details;
- incident date and reference;
- image/evidence list;
- missing items;
- estimate/repair evidence summary;
- valuation evidence summary;
- discrepancies;
- engineer questions;
- source links.

### 2. Engineer Copilot

A read-only, source-grounded assistant over the case record and internal knowledge base.

Allowed:

- summarise the case;
- list missing evidence;
- point to relevant images/documents;
- retrieve internal provider notes;
- explain how a field was extracted.

Not allowed:

- final opinion;
- roadworthiness conclusion;
- causation conclusion;
- valuation conclusion;
- legal/fraud allegation;
- unsupported technical repair instructions.

### 3. Report Drafting Assistant

Drafts factual report sections and structured payloads. The engineer must edit/approve before final issue or submission.

## EVA/Sentry implementation path

The Sentry API material indicates support for claim instructions, notes, claim updates, submit report and report retrieval. Therefore, the plan should not assume EVA is unavailable or rely on screen scraping. It should build a controlled API bridge.

### Stage 1 — EVA setup support

Generate a validated data pack for manual entry:

- Reg No;
- principal code;
- case ID/PO;
- insured/client name;
- external claim no;
- incident date;
- inspected on/current date;
- inspect-at address or `Image-based Assessment`;
- speedo/mileage and mileage source notes;
- required photo ordering instructions.

### Stage 2 — API payload drafting

Draft Sentry payloads but do not submit automatically:

- `Instruction/Inspection` for new instruction workflows;
- `Note/SubmitNote` for approved case notes/chasers;
- `Claim/Update` for limited claim fields such as excess/VAT where appropriate;
- `Report/SubmitReport` only when engineer sign-off is captured.

### Stage 3 — approved API submission

Enable API submission only after:

- credentials are securely stored;
- token refresh is implemented;
- endpoint behaviour is tested on sample/sandbox data;
- required field mapping is validated;
- engineer approval workflow is mandatory for reports;
- failed submissions go to review queue;
- every request/response is logged.

## Photo order correction

The EVA user guide contains a non-obvious photo upload rule: first two images are handled separately and should be an overall vehicle image showing the reg and a close-up damaged-area image, then all images are dragged in including those first two again. The engineer/evidence pack should explicitly represent this order and not assume a simple one-pass image list.

## Report drafting guardrails

Every draft section should be labelled by type:

- factual extracted data;
- sourced evidence summary;
- AI-drafted wording needing review;
- engineer-only conclusion placeholder.

## Acceptance tests

- Pack generation succeeds without AI if all structured fields are present.
- Copilot answers include source links and refuse unsupported engineering conclusions.
- Draft report payload cannot be submitted without engineer sign-off.
- Missing required Sentry fields cause validation error, not silent omission.
- EVA photo ordering guidance is visible in engineer/admin workflow.
- Report retrieval is client-side filtered by report list where the API has no direct search endpoint.
