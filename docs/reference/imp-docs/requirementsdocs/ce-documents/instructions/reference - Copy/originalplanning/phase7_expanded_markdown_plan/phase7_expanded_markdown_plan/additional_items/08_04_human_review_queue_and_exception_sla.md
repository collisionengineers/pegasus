# 8.4 Human Review Queue and Exception SLA

## Purpose

Create a review interface where staff approve, correct or reject extracted data before it is used for EVA import, partner updates, report drafting or engineer packs.

## Why this matters

The project context repeatedly points to human sign-off as a core control. The system can reduce manual work, but it must not silently submit weak, ambiguous or incomplete data.

## Step-by-step plan

### Step 1 — Define review triggers

1. Missing Work Provider.
2. Missing VRM.
3. Missing Reference.
4. Missing or uncertain incident date.
5. Multiple possible claimants/vehicles/references.
6. Provider detected but extraction confidence below threshold.
7. Engineer Report changed key fields.
8. Images/estimates are not matched to the instruction.
9. Duplicate or possible duplicate case found.
10. EVA validation failed.

### Step 2 — Define review categories

1. Data correction.
2. Missing related files.
3. Provider/mapping issue.
4. Image/evidence issue.
5. Duplicate/merge decision.
6. EVA submission issue.
7. Compliance/manual expert judgement required.

### Step 3 — Build the review screen

1. Show detected fields on the left.
2. Show source text and evidence snippets on the right.
3. Show attached files/images below or in a side panel.
4. Highlight fields with low confidence or Engineer Report overrides.
5. Let users correct field values directly.
6. Require a reason for rejecting or overriding system suggestions.

### Step 4 — Add queue controls

1. Filter by provider, date, state, assigned staff member and error category.
2. Sort by SLA/due date.
3. Allow cases to be assigned to a person.
4. Provide bulk actions only for low-risk status changes.
5. Escalate overdue cases.

### Step 5 — Connect review decisions to downstream flow

1. Approve data → ready_for_eva.
2. Correct data → update work item, log correction, then ready_for_eva.
3. Request missing files → awaiting_related_files and generate chaser draft.
4. Reject/terminal failure → failed_terminal with reason.
5. Duplicate found → merge, link or split with audit record.

### Step 6 — Capture performance metrics

1. Number of cases reviewed per day.
2. Average review time.
3. Most common extraction corrections.
4. Provider-specific failure rates.
5. Mapping rules that need improvement.

## Deliverables

- Review queue UI.
- Error category taxonomy.
- Review decision audit log.
- SLA dashboard.
- Correction analytics.

## Acceptance criteria

- No case reaches EVA import unless required fields pass validation or are manually approved.
- Every manual correction is auditable.
- Reviewers can see why a field was extracted.
- Missing-file and duplicate-case workflows are explicit.

## Risks and controls

| Risk | Control |
|---|---|
| Review queue becomes a bottleneck | Prioritise by SLA and automate low-risk checks. |
| Staff blindly approve | Show evidence snippets and require confirmation for critical fields. |
| Corrections do not improve mappings | Feed correction analytics into continuous improvement. |

## Suggested priority

P0/P1. This is the main control layer for safe automation.
