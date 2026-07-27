# 8.1 Work Item State Store and Job Sheet Replacement

## Purpose

Create a central operational state store for every instruction/case so that the spreadsheet, CE Document Mapper, Box folders, EVA setup and future portal/API work all reference the same record.

## Why this matters

The current job sheet is operationally important but fragile: it depends on Excel Desktop, VBA, stable columns, shared OneDrive/SharePoint access and a Z-drive/folder setup. A controlled state store removes the spreadsheet as the system of record while still allowing Excel-style views and exports.

## Step-by-step plan

### Step 1 — Inventory current job sheet columns and VBA behaviours

1. Catalogue all current columns from the Jobs sheet.
2. Record which columns are user-entered, formula-driven, VBA-managed or purely display-related.
3. Document the current row creation, folder creation and row movement behaviours.
4. Identify any hidden helper rows, table locators and icon/template ranges used by the workbook.
5. Separate operational data from spreadsheet presentation.

### Step 2 — Define the canonical work item schema

1. Start with the existing CE Document Mapper fields:
   - Work Provider
   - VRM
   - Vehicle Model
   - Claimant Name
   - Reference
   - Incident Date
   - Instruction Date
   - Inspection Date
   - Inspection Address
   - Accident Circumstances
   - VAT Status
   - Mileage
   - Mileage Unit
2. Add operational fields:
   - Internal CE reference
   - EVA reference
   - Box folder ID/path
   - Source channel
   - Source message ID
   - Current state
   - Assigned admin
   - Assigned engineer
   - Due date
   - SLA status
   - Missing information flags
   - Review status
   - Export/import status
3. Add technical fields:
   - Work item ID
   - Created/updated timestamps
   - Idempotency key
   - Source file checksums
   - Last successful automation step
   - Error category
   - Retry count

### Step 3 — Define the state machine

1. Use states such as:
   - received
   - awaiting_related_files
   - files_stored
   - extraction_started
   - extracted
   - validation_failed
   - needs_review
   - reviewed
   - ready_for_eva
   - eva_submitted
   - eva_imported
   - failed_retryable
   - failed_terminal
   - archived
2. Define allowed transitions between states.
3. Define which user roles can move a case between states.
4. Define which transitions can be automated and which require review.
5. Log every transition as an audit event.

### Step 4 — Select the initial persistence layer

1. For a quick controlled build, use a small database such as SQLite/PostgreSQL rather than embedding the state in Excel.
2. Keep a clean API/service layer in front of the database.
3. Store source documents in Box/file storage, not in the database.
4. Store only IDs, paths, hashes, metadata and extracted values in the database.
5. Ensure every file can be traced back to a work item.

### Step 5 — Provide an Excel-compatible operational view

1. Build a read-only/exportable job sheet view for staff who still prefer spreadsheet scanning.
2. Allow filtering by date, principal, VRM, missing information and status.
3. Preserve the existing repeated-reg/date-ageing visual logic as dashboard filters or highlights.
4. Do not let the spreadsheet become the source of truth again.

### Step 6 — Migrate gradually

1. Start with a parallel run: write new cases to both the old job sheet and the state store.
2. Compare row counts, references, due dates and folder links daily.
3. Resolve differences and update the mapping rules.
4. Switch new-case creation to the state store once parity is proven.
5. Keep the job sheet as a reporting/export view during transition.

## Deliverables

- Work item schema.
- State machine definition.
- Audit event schema.
- Database/service prototype.
- Excel export/view.
- Migration checklist.

## Acceptance criteria

- Every new imported case has one unique work item ID.
- Every source email/file/folder is linked to that work item.
- The state of a case can be understood without opening Excel.
- The old job sheet can be reproduced as an export/view.
- A failed automation step is visible, classified and recoverable.

## Risks and controls

| Risk | Control |
|---|---|
| Staff lose the familiar spreadsheet view | Keep Excel-style views and exports during transition. |
| Data split between old and new systems | Parallel-run comparison and clear cutover date. |
| State machine becomes over-complex | Start with essential states only; add later. |
| Folder paths change | Store stable Box IDs plus display paths. |

## Suggested priority

P0. This is the main foundation for most other Phase 7 opportunities.
