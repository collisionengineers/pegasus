# 8.6 Engineer Pack Generator and Template Manager

## Purpose

Generate a structured, reviewable pack for engineers containing the instruction details, source documents, images, estimates, missing-information flags and relevant notes.

## Why this matters

The project should assist intake and evidence preparation rather than automate expert judgement. A good engineer pack reduces time spent searching for documents while preserving the engineer's role as final expert decision-maker.

## Step-by-step plan

### Step 1 — Define pack contents

1. Case summary.
2. Vehicle details.
3. Claimant/client details.
4. Incident circumstances.
5. Inspection details/address.
6. Mileage/VAT fields.
7. Source documents with links.
8. Image schedule and thumbnails.
9. Estimate/Audatex summary if available.
10. Missing information and warnings.
11. Reviewer notes.
12. Activity/audit summary.

### Step 2 — Define template variants

1. Desktop/image-based inspection.
2. Physical inspection.
3. Audit/quality report.
4. Engineer Report companion document.
5. Total-loss/valuation support pack.
6. Repair estimate review pack.

### Step 3 — Build source-linked sections

1. Every field in the pack should link back to the source file or evidence snippet where possible.
2. Mark manually corrected fields.
3. Mark Engineer Report overwritten fields.
4. Mark estimated/assumed values.
5. Do not present generated summaries as expert conclusions.

### Step 4 — Add image handling

1. Attach or link relevant vehicle images.
2. Identify likely odometer/dashboard images.
3. Identify damage close-ups and general vehicle views.
4. Flag poor-quality, duplicate or missing required images.
5. Allow staff to manually reorder images for EVA/report requirements.

### Step 5 — Add approval workflow

1. Admin creates/reviews the pack.
2. Engineer reviews the pack.
3. Engineer can request more information.
4. Engineer approves use of pack for report work.
5. System logs approval and any pack changes.

### Step 6 — Output formats

1. Internal web view.
2. Markdown/HTML for internal review.
3. PDF for archive or handover.
4. Optional Word template output where required by existing process.

## Deliverables

- Engineer pack template schema.
- Template manager.
- Pack generation service.
- Source-link/evidence system.
- Approval workflow.

## Acceptance criteria

- A case can produce a readable engineer pack in one click after review.
- All pack fields show source/evidence where available.
- The pack clearly distinguishes automation output from engineer conclusions.
- Engineers can request missing information through the system.

## Risks and controls

| Risk | Control |
|---|---|
| Pack looks like a final report | Label as internal preparation material unless signed off. |
| Source links break | Store stable file IDs and hashes. |
| Engineers ignore warnings | Put warnings near the relevant field/image, not only at the end. |

## Suggested priority

P1/P2. High operational value after the intake/review foundations are reliable.
