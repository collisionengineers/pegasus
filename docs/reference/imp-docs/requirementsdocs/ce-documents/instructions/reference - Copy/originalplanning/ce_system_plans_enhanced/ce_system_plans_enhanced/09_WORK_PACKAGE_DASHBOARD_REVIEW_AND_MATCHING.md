# Work Package 09 - Dashboard, Review Queue, and Evidence Matching

## Purpose

Create the operational UI that replaces the job sheet holding pen while retaining the human oversight that prevents bad automation.

## Source files

- `phase_new_system.md`
- `Backup of CE Job Sheet 260429.xlsm`
- `Backup of Conditional Formatting 260202.txt`
- `collision-engineers-context-pack.zip` / `07_ui_dashboard_spec.md`, `05_target_workflow.md`
- `collision_project_context_pack.zip` / `11_EXCEPTION_HANDLING_AND_HUMAN_REVIEW.md`
- `handover.docx`

## Dashboard sections

### 1. Awaiting information

Equivalent to the current spreadsheet holding area.

Columns:

- Received date.
- Age.
- VRM.
- Provider/principal.
- Client/claimant.
- Vehicle.
- Missing items.
- Due/chase date.
- Last chaser sent.
- Notes.
- Assigned user.
- Status.

### 2. Ready for review

Cases with extracted data that need human confirmation.

Flags:

- Low confidence field.
- Missing required field.
- Provider unknown.
- Duplicate possible.
- VRM/reference conflict.
- Address decision required.

### 3. Ready for EVA

Reviewed cases with valid EVA payload.

Columns:

- Provider.
- VRM.
- Reference.
- Required documents present.
- Payload status.
- Last submitted.
- Submit action.

### 4. Failed/retry

Cases blocked by errors.

Columns:

- Error type.
- Integration.
- Retry count.
- Last error message.
- Next action.

## Step-by-step implementation

### Step 1 - Recreate legacy visibility

1. Implement list view matching the spreadsheet mental model.
2. Add duplicate VRM cue based on space-insensitive comparison.
3. Add date-age cues for 3-week and 4-week age thresholds.
4. Add current/past due-date cue.
5. Add “missing” column derived from MissingItem records.
6. Add notes column and audit-backed note history.

### Step 2 - Add upload/intake view

1. Drag-and-drop upload area.
2. Multi-file upload.
3. Manual select case or create new case.
4. Document classification results.
5. Field extraction preview.
6. Commit/cancel.
7. Immediate review flag creation for missing/conflicting fields.

### Step 3 - Build case detail page

Sections:

1. Case summary.
2. Extracted fields with evidence.
3. Documents and images.
4. Missing items and chasers.
5. Matching suggestions.
6. Timeline/audit log.
7. EVA payload preview.
8. Engineer pack preview.

Field panel behaviour:

- Show raw and normalized values.
- Show confidence/status.
- Show source document and snippet.
- Allow user edit.
- Require reason for critical overrides.
- Track engineer-report overwrite separately.

### Step 4 - Add matching suggestions

When an image, estimate, or engineer report arrives without direct case link:

1. Extract VRM candidates.
2. Extract reference candidates.
3. Extract claimant/vehicle/provider candidates.
4. Search open cases.
5. Score candidates.
6. Show top candidates with reason:
   - “VRM exact match.”
   - “Reference exact match.”
   - “Provider and claimant match.”
   - “Same email thread.”
7. Allow user to attach to selected case or create new case.

### Step 5 - Add missing information management

Missing item workflow:

1. System creates missing item when required evidence/field absent.
2. Dashboard shows case in Awaiting information.
3. User opens chaser draft.
4. User approves/sends.
5. System records chaser date.
6. Incoming response is linked where possible.
7. User or system closes missing item when evidence received.

### Step 6 - Add review queue actions

Review actions:

- Confirm all fields.
- Edit field.
- Select source value from conflict list.
- Mark missing item waived.
- Attach document to different case.
- Split incorrectly merged case.
- Create provider mapping task.
- Mark ready for EVA.

### Step 7 - Add user experience safeguards

1. Do not hide uncertainty.
2. Never silently auto-submit uncertain cases to EVA.
3. Make empty Work Provider a blocker for export/submission.
4. Show source evidence for every AI-assisted decision.
5. Preserve manual override routes.
6. Keep UI close to current staff mental model first; add advanced features later.

## Acceptance criteria

1. Staff can see all cases awaiting information without opening Excel.
2. Staff can identify why a case is blocked.
3. Staff can review extracted values and evidence in one page.
4. Late-arriving files can be attached to the correct case with confidence reasons.
5. Duplicate cases/VRMs are visible.
6. A case cannot be marked ready for EVA until required fields and files pass validation or are explicitly waived.
