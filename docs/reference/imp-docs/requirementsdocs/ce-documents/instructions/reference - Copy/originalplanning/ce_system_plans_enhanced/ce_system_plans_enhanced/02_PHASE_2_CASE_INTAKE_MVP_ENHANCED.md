# Phase 2 - Case Intake, Holding Pen and EVA-Ready MVP

## Purpose

Build the first production-grade replacement for the manual intake/control workflow. This phase should not attempt to replace every part of Collision Engineers' operations. It should create a controlled system that can receive or upload case material, store evidence, extract structured fields, show staff what is missing, prepare an EVA-ready payload, and preserve an auditable trail.

This phase is the practical successor to the existing job sheet and CE Document Mapper, not a separate greenfield system.

## Relationship to Phase 6

Phase 2 and Phase 6 overlap in the original plans. The corrected boundary is:

- **Phase 2**: prove the operational core with human review by default.
- **Phase 6**: expand that core into a broader bespoke platform with mature workflow automation, mobile/engineer tooling, reporting, advanced integrations and decommissioning.

Do not build two separate systems. Build Phase 2 so that Phase 6 can extend it.

## Relevant source files

Required source references before implementation:

- `phase_new_system.md` - original Phase 2 scope.
- `phase_bespoke_system.md` - overlapping Phase 6 architecture to reuse, not duplicate.
- `collision_project_context_pack/02_CURRENT_STATE_AND_MANUAL_PROCESS.md` - current Outlook/PDF/images/spreadsheet/Box/EVA workflow.
- `collision_project_context_pack/03_TARGET_OPERATING_MODEL.md` - target workflow layers and work item statuses.
- `collision_project_context_pack/05_OUTLOOK_INTAKE.md` - intake/correlation/duplicate handling.
- `collision_project_context_pack/06_BOX_STORAGE_AND_METADATA.md` - Box structure and metadata.
- `collision_project_context_pack/08_DATA_EXTRACTION_AND_AI_STRATEGY.md` - extraction/OCR/AI confidence model.
- `collision_project_context_pack/09_DATA_MODEL_AND_JSON_CONTRACTS.md` - canonical data model.
- `collision_project_context_pack/10_WORKFLOW_STATES_AND_ORCHESTRATION.md` - state machine and queues.
- `collision_project_context_pack/11_EXCEPTION_HANDLING_AND_HUMAN_REVIEW.md` - review queue design.
- `collision_project_context_pack/13_TESTING_QA_ACCEPTANCE_CRITERIA.md` - tests and release gates.
- `collision_project_context_pack/14_IMPLEMENTATION_ROADMAP.md` - incremental delivery sequence.
- `handover.docx` - job sheet, CE Document Mapper, mapping methods, Audit Mode, EVA notes.
- `claudechat.md` - Document Mapper behaviour and latest development decisions.
- `Backup of CE Job Sheet 260429.xlsm` - live-like job sheet structure.
- `Backup of Conditional Formatting 260202.txt` - duplicate/date highlighting rules to preserve as dashboard rules.
- `Mapped Principals.xlsx` - provider/principal list and known problematic mappings.
- `Final Format Example 02.json` - current JSON output contract.
- `EVA User Guide.pdf` - manual EVA entry flow and photo ordering.
- `Sentry_API_Complete_Guide.md` / `evaapidocs.pdf` - API endpoints and accepted payload shapes.
- `CE Communication Style & Tone Profile.docx` - tone for automated chasers and staff-facing communication.
- `Collision Engineers Whiteboard.jam` - manual visual workflow reference.

## Non-goals for Phase 2

Do not include these in the first production MVP:

1. Full auto-submission to EVA without human approval.
2. Fully autonomous claims decisions or report conclusions.
3. Full engineer mobile app/offline sync.
4. Replacement of every job sheet macro on day one.
5. Deep valuation automation unless a simple data-capture placeholder is needed.
6. New final report authorship beyond engineer-pack preparation.
7. Decommissioning of the spreadsheet or CE Document Mapper before parallel-running evidence exists.

## Phase 2 target outcome

At the end of Phase 2, staff should be able to:

1. Receive or upload instruction documents, emails and images.
2. See a dashboard/holding pen of all current work items.
3. Know whether each item has instruction, images, estimate, provider, VRM and required dates.
4. Open a case detail page with source files, extracted fields, missing items, confidence, notes and audit events.
5. Correct extracted fields and approve the item.
6. Generate an EVA-ready JSON/API payload and/or import package.
7. Store source files and generated outputs in Box with traceable metadata.
8. Send or draft chaser communications using approved tone/templates.
9. Preserve the current job sheet/Document Mapper as fallback during pilot.

## Core entities to implement first

Phase 2 should use a canonical model rather than binding directly to EVA or to the old Document Mapper JSON. Required MVP entities:

| Entity | Required MVP fields |
|---|---|
| `WorkItem` | ID, status, created/updated timestamps, source email/upload metadata, current assigned reviewer, current error. |
| `Case` | Work provider/principal, VRM, vehicle model, claimant name, reference/claim number, incident date, instruction date, inspection date, inspection address, accident circumstances, VAT status, mileage, mileage unit. |
| `Document` | ID, work item ID, role, filename, source type, MIME type, checksum, Box file ID/URL, extraction status. |
| `ImageEvidence` | Document ID, order, role/label, thumbnail, checksum, source email/upload ID, match status. |
| `Provider` | Name/code, EVA code, Box code, mailbox/source rules, mapping rules, chaser contacts, image-location rules. |
| `Garage` | Name, address, email, phone, figures flag, source notes. |
| `ExtractedField` | Field key, raw value, normalized value, confidence, status, method, source/evidence pointer, reviewer correction. |
| `ReviewFlag` | Type, severity, message, action required, resolved state. |
| `EvaSubmission` | Payload version, payload JSON, submission mode, response JSON, status, retry count. |
| `AuditEvent` | Work item ID, actor, action, timestamp, before/after values where relevant. |

## MVP status model

Use explicit statuses. Do not rely on implicit spreadsheet placement.

```text
received
awaiting_related_files
files_stored
extraction_started
extracted
validation_failed
needs_review
reviewed
ready_for_eva
eva_payload_generated
eva_submitted
eva_imported
failed_retryable
failed_terminal
archived
```

The dashboard should expose the important business groupings:

- **Awaiting information**: missing instruction, images, estimate, VRM, provider, date, or other required evidence.
- **Needs review**: extracted data exists but needs human correction/approval.
- **Ready for EVA**: approved canonical data can be exported or submitted.
- **Submitted/imported**: payload sent or imported, with response retained.

## Phase 2 work breakdown

### Step 1 - Discovery and baseline lock

1. Review the latest job sheet (`Backup of CE Job Sheet 260429.xlsm`) and confirm columns, sections and current staff use.
2. Confirm which fields from `Final Format Example 02.json` are required for EVA and which are internal-only.
3. Confirm which providers from `Mapped Principals.xlsx` must be included in the first pilot.
4. Extract the `Principals` and `Garages` sheets from the job sheet into CSV/JSON seed files.
5. Build a sample corpus: at least 20-50 anonymised documents/emails/images covering common providers, scanned/poor OCR cases, missing images, duplicates, separate image emails and engineer reports.
6. For each sample, create expected canonical output and expected dashboard status.
7. Confirm EVA integration mode: Sentry API, JSON import, or manual export first.
8. Confirm Box root folder and intended permission model.
9. Confirm the pilot mailbox/folder and whether Graph webhook or polling will be used.
10. Record open decisions in a decision log.

Exit criteria:

- Sample corpus exists.
- Provider/garage seed data extracted.
- MVP field inventory approved.
- EVA route selected for the first spike.
- Box and mailbox access requirements documented.

### Step 2 - Canonical schema and persistence

1. Define `canonical_case_intake.schema.json` using the entities above.
2. Define `extracted_field.schema.json` with value, normalized value, confidence, source and validation status.
3. Define `eva_payload.schema.json` or placeholder mapping if the exact API/import route is not yet confirmed.
4. Implement a database or durable store for work items, documents, extracted fields and events.
5. Add immutable event logging for every status transition.
6. Add schema versioning: canonical schema version, extraction version, validation rules version and EVA mapping version.
7. Add data import/export utilities so existing Document Mapper JSON can be inserted into the canonical model.

Exit criteria:

- One sample can be loaded into the canonical model and inspected.
- Schema validation rejects malformed data.
- Event log captures create/update/review/export transitions.

### Step 3 - File intake and upload path

Implement upload first, then mailbox automation. This reduces integration risk.

1. Build a web upload page or internal upload endpoint for PDF/DOC/DOCX/MSG/EML/images/ZIP if allowed.
2. On upload, create a work item and document records.
3. Compute SHA-256 checksums.
4. Store originals in Box using the Phase 2 folder convention.
5. Record Box IDs and URLs.
6. Add duplicate warning if checksum or VRM/reference matches an existing work item.
7. Preserve a manual override to attach files to an existing work item.

Exit criteria:

- Staff can upload an instruction and images.
- Box folder is created.
- Source files are preserved unmodified.
- Work item appears on dashboard.

### Step 4 - Document Mapper bridge and extraction baseline

1. Treat CE Document Mapper as the first proven extraction engine where possible.
2. Port or wrap its supported import behaviours: PDF, DOC, DOCX, MSG/EML, image extraction, provider presets, engineer-report overwrite semantics, date/mileage normalization and JSON field order.
3. Preserve relevant mapping methods from the latest documented state: Single Label, Two Labels, Fixed Position, Fixed Position + Label, Single Label +/-, Email Date, Manual Input, and any later method documented in the transcript.
4. Keep `providers.json` compatibility by writing a converter from Document Mapper provider presets to the new provider-rule model.
5. Add extraction output layers: raw text, normalized fields, evidence/source map, confidence/status.
6. Use deterministic parsing first; add OCR only where direct text fails or a scanned document is detected.
7. Add AI extraction only as a controlled fallback/cross-check, not as the only source of truth.

Exit criteria:

- At least one mapped provider extracts into canonical fields.
- Field-level status shows accepted/missing/conflict/needs_review.
- Staff can see raw source/evidence for every extracted value.

### Step 5 - Holding pen and review UI

1. Build dashboard cards/rows for work items.
2. Mirror the job sheet's two operational sections: awaiting information and ready for EVA.
3. Add filters: status, provider, age, due date, missing item, reviewer, duplicate flag.
4. Recreate key conditional-formatting behaviours as system flags: old cases, current/past due dates and duplicate VRMs ignoring spaces.
5. Build case detail page with:
   - extracted fields,
   - source documents,
   - images,
   - notes,
   - missing items,
   - review flags,
   - audit timeline,
   - Box links,
   - EVA payload preview.
6. Add editable fields with before/after audit capture.
7. Add approve/reject/request-info actions.
8. Add manual association of unmatched images/documents to existing work items.

Exit criteria:

- A reviewer can correct and approve a sample case.
- Corrections are audited.
- Missing information is visible and actionable.

### Step 6 - Evidence matching and missing-information rules

1. Implement matching signals: VRM, claim/reference number, sender, subject, thread/conversation ID, filenames, timestamps and provider-specific rules.
2. Use high/medium/low confidence bands.
3. Auto-attach only high-confidence matches.
4. Send medium/low matches to review with suggested associations.
5. Generate `MissingItem` records for missing instruction, images, estimate, VRM, provider, claimant, incident date, inspection address, mileage, or other provider/EVA-required fields.
6. Allow users to mark a missing item as resolved, not applicable, or chased.

Exit criteria:

- Separate image email can be linked to the correct work item.
- Ambiguous matches do not auto-attach.
- Missing items are visible and auditable.

### Step 7 - Box storage implementation

1. Create Box folder strategy for pilot work items.
2. Apply Box metadata template fields: work item ID, role, case reference, sender, received date, checksum, status.
3. Separate source, extracted, review, EVA and log files.
4. Ensure original files are never overwritten.
5. Add repair job for metadata failures.
6. Keep human-facing Box URLs in the case detail page.

Exit criteria:

- Every work item has a Box folder or documented failure.
- Every stored source file has checksum and role.
- Generated extraction/review/EVA outputs are stored separately from source evidence.

### Step 8 - EVA-ready payload generation

1. Map canonical fields to EVA/Sentry API/import fields.
2. Build a payload preview page.
3. Validate payload before submission/export.
4. Store payload version and payload JSON in Box and database.
5. Start with `Generate EVA Payload` and manual/human-approved submission unless the API sandbox has been verified.
6. If using Sentry API, implement token retrieval, short-lived token refresh and endpoint adapters.
7. Prevent duplicate EVA records with work item ID, payload version and any EVA external reference/idempotency mechanism.

Exit criteria:

- Approved work item can generate an EVA payload.
- Payload is stored with source references.
- Duplicate submission is blocked or requires explicit human confirmation.

### Step 9 - Chaser drafting and communication controls

1. Add chaser template library using `CE Communication Style & Tone Profile.docx`.
2. Generate draft text for missing images/estimate/instruction, not automatic send initially.
3. Support email first; WhatsApp/Teams/Twilio can be Phase 6 unless already operationally required.
4. Log every draft/send event.
5. Allow provider/garage contacts from provider settings or `Garages` sheet.

Exit criteria:

- Reviewer can generate a concise, tone-aligned chaser.
- Chaser event appears in the audit timeline.
- Chasers are linked to missing items.

### Step 10 - Pilot, parallel run and rollout

1. Run the MVP in shadow mode against selected providers.
2. Compare extracted fields to current human outputs.
3. Track correction rates by provider/template/field.
4. Move to assisted mode: staff approve and export/submit from the new system.
5. Keep job sheet and CE Document Mapper as fallback.
6. Only after stable acceptance metrics should the job sheet be reduced to read-only/control use.

Exit criteria:

- Pilot provider set has measured field accuracy.
- Staff can complete cases using the new system without losing the legacy fallback.
- Operational owner signs off move from shadow to assisted mode.

## Acceptance criteria

Phase 2 is complete only when:

1. Work items can be created from upload and/or pilot Outlook intake.
2. Source documents and images are stored in Box with metadata and checksums.
3. The dashboard shows current work items, missing items, duplicates and ready-for-EVA cases.
4. Document extraction produces canonical JSON with field-level confidence/evidence.
5. Reviewers can correct fields and approve cases.
6. EVA payload generation works for the pilot provider set.
7. Every meaningful action is logged.
8. Test corpus has passing acceptance tests.
9. Security/privacy review has approved pilot data handling.
10. Staff have a documented fallback route if automation fails.

## Risks and controls

| Risk | Control |
|---|---|
| New system loses job sheet nuance. | Baseline job sheet columns, Principals/Garages sheets, chaser notes and conditional rules before build. |
| Provider mapping breaks because Document Mapper behaviour is lost. | Build provider-rule converter and keep Document Mapper as reference/fallback. |
| Images arrive separately and get misattached. | Use confidence-based matching and human review for medium/low confidence. |
| EVA duplicate records. | Use idempotency keys, payload versions, search/check-before-submit and human confirmation. |
| AI guesses fields. | Require evidence/source map, null for missing values, validation and human review. |
| Staff reject the system as too complex. | Keep Phase 2 UI close to existing holding pen and add capability incrementally. |
| Privacy/security concerns. | Least privilege, audit trail, data minimisation, controlled Box storage and secrets vault. |

## Phase 2 deliverables

- Canonical data schema `0.1.0-alpha.1`.
- Provider/garage seed data.
- Work item database/state store.
- Upload/intake workflow.
- Box storage integration.
- Extraction engine/Document Mapper bridge.
- Holding pen and case detail UI.
- Evidence matching and missing-item logic.
- Review/approval flow.
- EVA payload generator.
- Chaser draft templates.
- Audit event log.
- Test corpus and acceptance suite.
- Rollout/fallback runbook.

