# Phase 6 - Bespoke End-to-End Platform Enhanced Plan

## Positioning

Phase 6 is not a second, separate build. It is the mature operating model that grows out of the Phase 2 case-intake MVP. Phase 2 proves the intake, extraction, review, storage, and EVA-ready submission path. Phase 6 extends that foundation into a resilient case management platform covering the full lifecycle from instruction receipt through engineer pack, reporting, invoicing support, compliance, monitoring, and legacy decommissioning.

## Source files used

- `phase_bespoke_system.md`
- `phase_new_system.md`
- `collision_project_context_pack.zip` especially `03_TARGET_OPERATING_MODEL.md`, `07_EVA_INTEGRATION.md`, `10_WORKFLOW_STATES_AND_ORCHESTRATION.md`, `12_SECURITY_PRIVACY_AND_GOVERNANCE.md`, `16_MONITORING_OBSERVABILITY_AND_RUNBOOKS.md`
- `collision-engineers-context-pack.zip` especially `05_target_workflow.md`, `07_ui_dashboard_spec.md`, `08_technical_architecture.md`, `09_data_model_and_schemas.md`, `12_compliance_governance_and_risk.md`
- `handover.docx`
- `EVA User Guide.pdf`
- `Sentry_API_Complete_Guide.md` and `evaapidocs.pdf`
- `CE Communication Style & Tone Profile.docx`
- `Backup of CE Job Sheet 260429.xlsm`
- `Mapped Principals.xlsx`

## Phase 6 target outcome

By the end of Phase 6, Collision Engineers should have a single case management platform that:

1. Receives and classifies instruction emails and related evidence.
2. Stores every relevant file in a controlled structure with metadata.
3. Extracts and validates case fields with source evidence and confidence.
4. Presents a human review queue for uncertain or incomplete cases.
5. Matches late-arriving images, estimates, engineer reports, and supplemental documents to the right case.
6. Produces controlled EVA/Sentry submissions and records every request/response.
7. Generates engineer packs with correct evidence ordering, notes, and references.
8. Supports engineers through a portal or PWA.
9. Maintains provider/principal/garage mappings centrally.
10. Provides audit trails, dashboards, monitoring, and incident runbooks.
11. Allows the legacy spreadsheet and CE Document Mapper to be retired safely once parity is proven.

## Platform modules

### 1. Communication and intake layer

**Purpose:** turn Outlook/shared-inbox activity into durable work items.

**Capabilities:**

- Microsoft Graph inbox connector.
- Polling fallback if webhook subscriptions are not practical.
- Shared mailbox coverage for relevant Collision Engineers mailboxes.
- Email classification: instruction, images, estimate, engineer report, chaser response, invoice, unrelated.
- Attachment extraction and safe temporary processing.
- Deduplication using Graph message ID, internet message ID, attachment checksum, VRM/reference matches.
- Related-email correlation for image-only or estimate-only follow-ups.
- Chaser sending using approved Collision Engineers tone and templates.

**Step-by-step build:**

1. Register Graph app and define mailbox access model.
2. Build read-only email ingestion worker.
3. Store message metadata and attachment metadata in the database.
4. Add duplicate detection rules.
5. Add classification rules using deterministic patterns first.
6. Add optional AI classification only after deterministic rules have measurable limits.
7. Add chaser template generation but require user approval before sending.
8. Add sent-chaser tracking and response linking.

### 2. Case management core

**Purpose:** make the case record the single operational source of truth.

**Capabilities:**

- Case database with canonical entities.
- Workflow state machine.
- Review queue.
- Audit log.
- Role-based permissions.
- Provider and garage configuration.
- Status dashboards and SLA cues.

**Recommended state model:**

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
eva_submitted
eva_imported
assigned_to_engineer
engineer_pack_ready
report_uploaded
completed
failed_retryable
failed_terminal
archived
```

**Step-by-step build:**

1. Implement database schema and event log.
2. Define all legal state transitions.
3. Add transition guardrails: required documents, required fields, reviewed flag, duplicate check.
4. Add actions triggered by state change: queue parse, create Box folder, create EVA payload, notify reviewer.
5. Add manual override with mandatory reason.
6. Add daily reconciliation to identify stuck cases.
7. Add read-only legacy job-sheet import/export during migration.

### 3. Parsing and extraction layer

**Purpose:** extract consistent, source-attributed fields from documents.

**Capabilities:**

- Bridge/reuse CE Document Mapper rules initially.
- Central parser service for PDF, DOC, DOCX, MSG, EML, and images.
- OCR only when needed and within defined limits.
- Field-level evidence map.
- Confidence and status values per field.
- Provider-specific extraction settings.
- AI extraction as a secondary assist, not an uncontrolled replacement.

**Step-by-step build:**

1. Lift CE Document Mapper rules into a testable service boundary or adapter.
2. Preserve canonical field order and export semantics from `Final Format Example 02.json`.
3. Store raw extracted text separately from normalized fields.
4. Record source location for every field where practical: document ID, page/line/block/snippet.
5. Add validation rules for VRM, dates, mileage, VAT status, address, and provider reference.
6. Add comparison view for conflicting sources: instruction vs estimate vs engineer report.
7. Add active-learning loop: reviewer correction updates provider rules or flags candidate training examples.

### 4. File storage layer

**Purpose:** centralize source files and generated packs without losing original evidence.

**Capabilities:**

- Box folder creation and lookup.
- File upload with metadata and checksums.
- Original-file preservation.
- Controlled naming conventions.
- Signed or limited-access links for engineers.
- Retention and archival policy.

**Step-by-step build:**

1. Define folder structure by case reference/provider/VRM.
2. Build Box API adapter.
3. Add idempotent folder creation using case ID and Box folder ID.
4. Upload original files without modification.
5. Add metadata: case ID, provider, VRM, document type, source email ID, checksum, upload user/time.
6. Add generated artefact storage: engineer pack, EVA payload, report, invoice support files.
7. Add failure mode: local temporary queue, retry, and user-visible “Box upload failed” status.

### 5. EVA/Sentry integration layer

**Purpose:** replace manual re-keying with controlled, auditable EVA submissions.

**Capabilities:**

- Sentry API token manager.
- Instruction submission.
- Location updates.
- Notes.
- Claim updates.
- Report submission when the workflow reaches that maturity.
- Request/response audit log.
- Payload preview before submission.

**Step-by-step build:**

1. Confirm production/sandbox credentials and endpoint access.
2. Build token manager with refresh before expiry.
3. Map canonical case fields to `/Instruction/Inspection` request.
4. Add JSON payload preview and validation in UI.
5. Store every payload, response, status code, and correlation ID.
6. Add idempotency/deduplication guard before each submission.
7. Add retry rules for network/auth issues and terminal failure rules for invalid data.
8. Add later endpoints: notes, location update, claim update, report submission.

### 6. Engineer portal / PWA

**Purpose:** give engineers a controlled view of their assigned cases and evidence.

**Capabilities:**

- Engineer case list.
- Engineer pack viewer.
- Image carousel and document viewer.
- Notes and report upload.
- Optional photo capture/upload.
- Offline-first behaviour for field work, if needed.

**Step-by-step build:**

1. Define engineer roles and permissions.
2. Build read-only engineer pack web view first.
3. Add report upload with file validation.
4. Add notes and inspection completion status.
5. Add mobile/PWA shell.
6. Add offline sync only after core online portal is proven.

### 7. Reporting, analytics and continuous improvement

**Purpose:** make operational performance visible.

**Capabilities:**

- Throughput dashboards.
- Cases awaiting images/estimate/review/EVA submission.
- Provider-level extraction accuracy.
- Chaser response time.
- EVA submission success/failure rate.
- Ageing and duplicate VRM alerts inherited from spreadsheet logic.

**Step-by-step build:**

1. Define KPI dashboard.
2. Add data export for review.
3. Add daily stuck-case report.
4. Add provider extraction accuracy report.
5. Add audit report by case and by user.
6. Add incident runbooks for system failures.

## Phase 6 milestones

### Milestone 6.1 - Architecture and platform hardening

**Entry criteria:** Phase 2 MVP live or close to live, with working intake/review/EVA-ready flow.

**Tasks:**

1. Confirm final architecture: custom web app, low-code hybrid, or custom backend with low-code admin surfaces.
2. Lock canonical data model.
3. Lock workflow states and transition rules.
4. Confirm authentication/SSO plan.
5. Confirm environments: development, test, production.
6. Confirm logging and audit approach.

**Exit criteria:** signed architecture note, data model, workflow state diagram, environment plan, and security baseline.

### Milestone 6.2 - Full communication layer

**Tasks:**

1. Extend inbox connector to all agreed mailboxes.
2. Add response threading and chaser tracking.
3. Add templates following CE communication style.
4. Add approval flow before sending external messages.
5. Add audit events for every message action.

**Exit criteria:** chasers and responses are trackable by case, and staff can see communication history without searching Outlook manually.

### Milestone 6.3 - Full EVA/Sentry operations

**Tasks:**

1. Implement instruction submission.
2. Implement location update.
3. Implement note submission.
4. Implement claim update where needed.
5. Implement report submission if operationally required.
6. Add duplicate prevention and reconciliation.

**Exit criteria:** all high-value EVA transactions required by the business are supported with human preview, submission log, and retry/failure handling.

### Milestone 6.4 - Engineer portal

**Tasks:**

1. Build engineer case list.
2. Build pack viewer.
3. Add document/image viewer.
4. Add report upload and completion state.
5. Add mobile/PWA packaging.
6. Run pilot with one or two engineers.

**Exit criteria:** engineer can complete assigned work without relying on manually assembled Box-only context.

### Milestone 6.5 - Legacy decommissioning

**Tasks:**

1. Define parity checklist against job sheet and CE Document Mapper.
2. Run parallel operation for agreed period.
3. Compare output accuracy, processing time, and failure cases.
4. Freeze spreadsheet to read-only once all critical workflows are covered.
5. Archive old provider presets and job-sheet data.
6. Retain rollback route for a defined period.

**Exit criteria:** spreadsheet and standalone mapper are no longer required for normal production processing.

## Major risks and controls

| Risk | Control |
|---|---|
| Platform tries to automate too much too early | Keep human review as a gate for uncertain/externally-submitted actions. |
| Provider formats vary or change | Provider settings and review correction loop. |
| EVA API access differs from documentation | Build EVA adapter with feature flags and fallback manual/export route. |
| Staff adoption fails | Shadow current workflow first; keep UI close to holding-pen mental model. |
| Evidence is misplaced | Box metadata, checksums, source email links, audit events. |
| AI produces untraceable outputs | Field-level evidence map, confidence, and human approval. |
| Security/privacy gaps | Least privilege, audit, retention, encrypted secrets, and incident runbooks. |

## Phase 6 acceptance criteria

Phase 6 is complete when:

1. A case can be received, stored, extracted, reviewed, submitted to EVA, assigned, packed, reported, and archived without using the legacy spreadsheet as the operational source of truth.
2. Every external message, file operation, field extraction, user edit, and EVA transaction has an audit event.
3. Staff can identify all cases awaiting action from the dashboard.
4. Engineers can access required evidence from the engineer interface.
5. Provider/principal/garage settings are maintainable without developer involvement.
6. The system has documented runbooks, monitoring, acceptance tests, and rollback procedures.
