# Work Package 11 - Engineer Pack and Reporting

## Purpose

Create a repeatable engineer pack process so engineers receive complete, ordered, and auditable case information without staff manually assembling files each time.

## Source files

- `phase_new_system.md`
- `phase_bespoke_system.md`
- `EVA User Guide.pdf`
- `handover.docx`
- `Standard Audatex Invoice.docx`
- `Website Invoice Template.docx`
- `collision-engineers-context-pack.zip` / `06_mvp_demo_case_intake_engineer_pack.md`

## Engineer pack contents

Minimum Phase 2 pack:

1. Case summary.
2. Work Provider.
3. VRM and vehicle model.
4. Claimant name.
5. Claim/reference number.
6. Incident/instruction/inspection dates.
7. Inspection address or image-based assessment note.
8. Accident circumstances.
9. Instruction document.
10. Ordered images.
11. Estimate if present.
12. Notes/chaser history relevant to the engineer.
13. Missing/uncertain items, if any.
14. Source links to Box files.

Phase 6 additions:

1. Valuation report.
2. MOT/DVLA history.
3. Previous total loss flags.
4. AI damage summary with evidence.
5. Report upload form.
6. Engineer notes.
7. Mobile/PWA capture and upload.

## Image ordering rules

The EVA guide states that the first two photos are handled separately and should be:

1. An overall vehicle image showing the registration.
2. A close-up image of the damaged area.

Then all images are uploaded including those first two again. The pack should make this ordering explicit.

## Step-by-step implementation

### Step 1 - Define pack format

Choose Phase 2 format:

- PDF pack, or
- Web case pack page, or
- Both.

Recommended: start with a web pack page and generate PDF only if engineers require offline/download format.

### Step 2 - Build pack generator

1. Retrieve reviewed case data.
2. Retrieve document/image file links.
3. Apply image ordering.
4. Insert field values and source labels.
5. Insert warnings for unresolved items.
6. Store generated pack as a Box-generated file and a database record.
7. Emit audit event.

### Step 3 - Add manual image ordering UI

1. Display thumbnails.
2. Allow drag reorder.
3. Allow mark as `overall_reg_image` and `damage_closeup_image`.
4. Show warning if first two are not selected.
5. Show warning for duplicate/irrelevant images.
6. Save selected order.

### Step 4 - Add notes for engineer

1. Add free-text engineer note field.
2. Allow staff to carry forward Box notes or case notes.
3. Keep communication style concise and factual.
4. Store note with author/time.
5. Include note in pack.

### Step 5 - Add report upload

Phase 2 may keep report upload in Box manually. Phase 6 should add:

1. Report upload from engineer portal.
2. File type validation.
3. Store report in Box.
4. Link to case.
5. Mark case state `report_uploaded`.
6. Trigger review/completion flow.
7. Optionally submit report to EVA/Sentry when integration is mature.

### Step 6 - Invoice/report artefact support

`Standard Audatex Invoice.docx` and `Website Invoice Template.docx` are useful references for generated invoice/support artefacts, but invoice generation should remain outside Phase 2 unless directly required.

If invoice support is added:

1. Confirm invoice fields.
2. Confirm template variant by provider/customer.
3. Generate preview.
4. Require human approval.
5. Store generated invoice in Box.

## Acceptance criteria

1. Staff can generate an engineer pack from a reviewed case.
2. Pack includes all required source documents and images.
3. First-two-image ordering is visible and editable.
4. Pack stores its generation event and source document list.
5. Engineers can access the pack without being given unnecessary broader access.
6. Report upload/completion state can be tracked in Phase 6.
