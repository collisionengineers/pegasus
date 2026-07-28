# Review of Phase 7 Plan

## Overall assessment

The Phase 7 plan is directionally strong. The nine items cover the obvious long-term value areas: insurer integration, risk scoring, Audatex, scheduling, portal, API, analytics, warehouse/archival and continuous improvement.

The main weakness is that the current plan lists opportunities without distinguishing:

- whether the idea needs foundational data first;
- whether it creates legal, privacy or expert-evidence risk;
- whether it can be prototyped with the current CE Document Mapper / EVA workflow;
- whether it should be a product feature, a data capability, an operational process, or a governance control.

## Cross-cutting recommendations

### 1. Build a durable work item layer before advanced features

Most Phase 7 items depend on a consistent internal record for each case. That work item should link:

- Outlook message metadata;
- source PDFs and images;
- Box file/folder IDs;
- extracted fields;
- provider/principal mapping;
- human review decisions;
- EVA payloads and responses;
- status transitions;
- audit events.

Without this layer, analytics, scheduling, portal status, risk scoring and APIs will all be brittle.

### 2. Treat “risk scoring” as review triage, not fraud decisioning

The project context repeatedly emphasises that Collision Engineers provides independent technical evidence. AI can flag risk indicators, contradictions, duplicates and anomalies. It should not label a claim fraudulent or produce a conclusion that affects claim handling without human review.

Recommended terminology:

- “Risk indicator”
- “Review flag”
- “Evidence anomaly”
- “Duplicate concern”
- “Requires management/engineer review”

Avoid:

- “Fraud detected”
- “Claim invalid”
- “Automated fraud decision”

### 3. Split integration plans into inbound, outbound and partner-facing

The current plan mentions insurer APIs, Audatex partnerships and partner APIs. These are related but distinct:

- **Inbound integrations:** receiving instructions, documents, images and status changes.
- **Outbound integrations:** submitting notes, updates, reports or status back to EVA/partners.
- **Partner-facing API/portal:** letting external parties push or query their own cases.

Each has different security, authentication, data minimisation and support requirements.

### 4. Keep existing CE Document Mapper learnings

The live CE Document Mapper work has already solved important problems:

- field mapping by provider;
- provider presets;
- engineer-report override logic;
- PDF/DOC/DOCX/EML/MSG extraction;
- batch processing;
- JSON export with fixed field order;
- mileage/date normalization;
- OneDrive-aware export/storage paths.

Future automation should either reuse this logic or migrate it deliberately into a central extraction service. Do not discard it.

### 5. Build source-linked outputs

All advanced modules should preserve evidence links. A dashboard number, risk flag or portal status should be traceable to source emails, source files, extraction output, reviewer action and EVA submission.

### 6. Prioritise human review and operational visibility

The highest immediate value is not sophisticated AI. It is making every case visible, attributable and reviewable, especially:

- missing images;
- unmatched evidence;
- duplicate cases;
- failed EVA imports;
- incomplete field extraction;
- provider-specific mapping gaps;
- stale cases sitting in the holding pen.

## Recommended Phase 7 reframing

Rename Phase 7 as:

> **Phase 7 — Platform Expansion and Intelligence Layer**

Then divide it into substreams:

| Substream | Purpose |
|---|---|
| Integrations | Insurer platforms, Audatex, EVA/Sentry, partner API. |
| External access | Customer portal and controlled partner upload/status features. |
| Intelligence | Risk indicators, predictive scheduling, analytics, knowledge base. |
| Data platform | Warehouse, archival, source-linked records, retention. |
| Operating model | Continuous improvement, governance, monitoring, staff feedback. |

## Minimum foundation before Phase 7 starts

Phase 7 should not begin until these are at least in pilot:

1. Work item state store.
2. Box/source-file traceability.
3. Extraction output with confidence and evidence.
4. Human review workflow.
5. EVA payload generation and response logging.
6. Provider/principal configuration library.
7. Test corpus and regression harness.
8. Data protection and security baseline.
