# Work Package 05 - Data Model and Workflow

## Purpose

Define the canonical case model, workflow states, event log, and configuration objects used by both Phase 2 and Phase 6.

## Source files

- `phase_new_system.md`
- `phase_bespoke_system.md`
- `Final Format Example 02.json`
- `Backup of CE Job Sheet 260429.xlsm`
- `Mapped Principals.xlsx`
- `handover.docx`
- `collision_project_context_pack.zip` / `03_TARGET_OPERATING_MODEL.md`, `09_DATA_MODEL_AND_JSON_CONTRACTS.md`, `10_WORKFLOW_STATES_AND_ORCHESTRATION.md`
- `collision-engineers-context-pack.zip` / `09_data_model_and_schemas.md`

## Required canonical fields

Carry forward the CE Document Mapper export fields as the first canonical field set:

1. Work Provider
2. VRM
3. Vehicle Model
4. Claimant Name
5. Reference
6. Incident Date
7. Instruction Date
8. Inspection Date
9. Inspection Address
10. Accident Circumstances
11. VAT Status
12. Mileage
13. Mileage Unit

The platform can add more fields, but these must remain stable for compatibility with existing mapping/export expectations.

## Core entities

### Case

Represents the operational work item.

Minimum fields:

- `case_id`
- `work_provider`
- `provider_code`
- `principal_eva_code`
- `principal_box_code`
- `vrm_normalized`
- `vrm_display`
- `claim_reference`
- `claimant_name`
- `vehicle_model`
- `incident_date`
- `instruction_date`
- `inspection_date`
- `inspection_address`
- `accident_circumstances`
- `vat_status`
- `mileage`
- `mileage_unit`
- `current_status`
- `review_status`
- `eva_status`
- `box_folder_id`
- `created_at`, `updated_at`, `archived_at`

### Document

Represents any source file or generated file.

Minimum fields:

- `document_id`
- `case_id`
- `source_email_id`
- `source_attachment_id`
- `document_type` (`instruction`, `image`, `estimate`, `engineer_report`, `invoice`, `other`)
- `original_filename`
- `stored_filename`
- `mime_type`
- `checksum_sha256`
- `box_file_id`
- `box_url`
- `received_at`
- `uploaded_at`
- `parser_status`
- `ocr_used`
- `raw_text_id`

### ExtractedField

Represents a field value plus evidence.

Minimum fields:

- `field_id`
- `case_id`
- `field_key`
- `raw_value`
- `normalized_value`
- `status` (`found`, `missing`, `conflict`, `low_confidence`, `user_confirmed`, `user_edited`)
- `confidence`
- `source_document_id`
- `evidence_snippet`
- `evidence_page`
- `evidence_line_or_block`
- `extraction_method`
- `provider_rule_id`
- `reviewed_by`, `reviewed_at`

### Image

Represents extracted/uploaded images and ordering decisions.

Minimum fields:

- `image_id`
- `case_id`
- `document_id`
- `box_file_id`
- `original_order`
- `selected_eva_order`
- `is_overview_image`
- `is_damage_closeup`
- `contains_vrm_candidate`
- `orientation`
- `quality_flags`

### Provider

Represents principal/provider configuration.

Minimum fields:

- `provider_id`
- `name`
- `detected_phrases`
- `eva_code`
- `box_code`
- `inbox`
- `instructions_notes`
- `drag_into_eva`
- `sent_mino`
- `images_location_rule`
- `image_based_or_address_rule`
- `sending_report_rule`
- `is_engineer_report_provider`
- `active`

### Garage

Represents garage/repairer contact and figures settings.

Minimum fields:

- `garage_id`
- `name`
- `address`
- `email`
- `phone`
- `figures_rule`
- `provider_ids`

### MissingItem

Represents outstanding evidence or data.

- `missing_item_id`
- `case_id`
- `type` (`images`, `estimate`, `instruction`, `client_id`, `address`, `provider_mapping`, `other`)
- `description`
- `status` (`open`, `chaser_sent`, `received`, `waived`)
- `last_chased_at`
- `resolved_at`

### AuditEvent

Immutable event log.

- `event_id`
- `case_id`
- `actor_type` (`system`, `user`, `integration`)
- `actor_id`
- `event_type`
- `event_payload`
- `correlation_id`
- `created_at`

## Workflow state machine

### Recommended states

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

### State transitions

| From | To | Trigger | Guard |
|---|---|---|---|
| none | received | Email/upload/import detected | Source metadata captured. |
| received | awaiting_related_files | Instruction exists but images/estimate required | Missing item record created. |
| received | files_stored | Required file stored | Box or local controlled storage succeeds. |
| files_stored | extraction_started | Parser queued | Document type known. |
| extraction_started | extracted | Parser completes | Raw text and extraction output stored. |
| extracted | validation_failed | Critical validation fails | Example: missing VRM, no provider, invalid date. |
| extracted | needs_review | Low confidence, conflict, missing data | Review flags created. |
| extracted | reviewed | No review required | All required values found and no conflicts. |
| needs_review | reviewed | User confirms/edits | Required fields resolved. |
| reviewed | ready_for_eva | EVA payload valid | Payload preview generated. |
| ready_for_eva | eva_submitted | User submits or approved automation submits | Duplicate guard passes. |
| eva_submitted | eva_imported | EVA success response/reconciliation | External reference captured. |
| eva_imported | assigned_to_engineer | Assignment action | Engineer selected. |
| assigned_to_engineer | engineer_pack_ready | Pack generated | Required docs/images present. |
| engineer_pack_ready | report_uploaded | Engineer uploads report | Report file stored. |
| report_uploaded | completed | Admin closes | Required completion checks pass. |
| any active | failed_retryable | Integration/transient failure | Retry policy applies. |
| any active | failed_terminal | Irrecoverable failure | Human action required. |
| completed | archived | Retention/archive job | Retention rule satisfied. |

## Step-by-step implementation

### Step 1 - Define schema contracts

1. Create JSON Schema for Case, Document, ExtractedField, MissingItem, AuditEvent, Provider, Garage.
2. Add schema version numbers.
3. Write sample JSON objects using `Final Format Example 02.json` as the base.
4. Confirm date and mileage normalized formats.
5. Confirm field names for EVA mapping.

### Step 2 - Build database

1. Create relational tables for all core entities.
2. Add indexes on VRM, claim reference, provider, status, created date, source email ID, checksum.
3. Add uniqueness constraints for dedupe: email message ID, attachment checksum within case, EVA submission idempotency key.
4. Add soft-delete/archive fields rather than hard delete.
5. Add immutable audit table.

### Step 3 - Build state transition service

1. Implement transition function that validates legal state changes.
2. Require a reason for manual overrides.
3. Emit audit event for every transition.
4. Queue follow-on jobs where needed.
5. Add state reconciliation job for stuck cases.

### Step 4 - Configure required fields

Create provider-level required field rules. For example:

- Global required for EVA: Work Provider, VRM, Reference, Claimant Name or Insured/Claimant equivalent, Incident Date, Instruction Date.
- Required for engineer pack: instruction, images, evidence summary.
- Required for image-based inspection: at least ordered images and inspection address decision.

### Step 5 - Add review flags

Review flags should be explicit records, not just UI warnings:

- Missing field.
- Low confidence extraction.
- Conflicting values across documents.
- Duplicate VRM.
- Mismatched VRM between instruction and estimate.
- Missing images.
- Provider unmapped.
- OCR poor quality.
- EVA payload invalid.

### Step 6 - Build audit event taxonomy

Minimum event types:

- `case.created`
- `email.ingested`
- `document.stored`
- `document.parsed`
- `field.extracted`
- `field.edited`
- `review.flag_created`
- `review.completed`
- `chaser.sent`
- `chaser.response_received`
- `box.folder_created`
- `eva.payload_created`
- `eva.submitted`
- `eva.response_received`
- `engineer_pack.generated`
- `report.uploaded`
- `case.completed`

## Acceptance criteria

1. Every case has a current state and a full event history.
2. Every extracted field can be traced to a source document or user edit.
3. Workflow transitions are deterministic and guarded.
4. The system can reproduce the legacy job sheet holding-pen sections from status/missing-item data.
5. Provider, garage, and mapping settings can be imported from legacy sources and edited centrally.
6. API payloads are generated from canonical case records, not ad hoc UI fields.
