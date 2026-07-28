# 8.7 Image Quality and Required Photo Coverage Assistant

## Purpose

Help staff confirm that vehicle images are present, usable, correctly matched to the case and sufficient for an image-based assessment or engineer review.

## Why this matters

The EVA workflow depends on images being present and ordered correctly. Missing odometer, registration, VIN, damage close-ups or general vehicle views can delay assessment and create rework.

## Step-by-step plan

### Step 1 — Define required image categories

1. Front vehicle view.
2. Rear vehicle view.
3. Nearside view.
4. Offside view.
5. Damage close-up(s).
6. Odometer/dashboard image.
7. VIN/plate image where relevant.
8. Interior/SRS/airbag image where relevant.
9. Estimate or Audatex screenshot/PDF if applicable.
10. Any provider-specific required image type.

### Step 2 — Build manual tagging first

1. Let staff tag images by category.
2. Store tags against file IDs.
3. Allow multiple tags per image.
4. Support “not required” and “not available” outcomes with reasons.
5. Use manual tags as training/evaluation data for future image assistance.

### Step 3 — Add lightweight automated checks

1. Detect duplicate images using checksums and visual similarity.
2. Flag tiny, blurred, corrupt or unsupported images.
3. Read file metadata where safe and useful.
4. Detect obvious mismatch indicators such as unrelated file names or repeated folders.
5. Avoid making liability, fraud or roadworthiness conclusions.

### Step 4 — Add image matching to cases

1. Match images by source email/thread.
2. Match images by folder location.
3. Match by VRM/reference in filename or nearby text.
4. Match by time proximity to instruction receipt.
5. Route ambiguous matches to review.

### Step 5 — Add missing-image prompts

1. Display missing categories in the review queue.
2. Generate a draft chaser message in Collision Engineers’ usual concise tone.
3. Include exact missing items.
4. Require staff approval before sending.
5. Track whether missing-image chasers have been sent.

### Step 6 — Connect to engineer pack and EVA preparation

1. Use tagged images in the engineer pack.
2. Preserve required image order where EVA/report workflows need it.
3. Carry missing-image warnings into the engineer pack.
4. Keep original images immutable; store crops/edits as derived files.

## Deliverables

- Required-image checklist.
- Image tagging UI.
- Duplicate/quality checks.
- Image-to-case matching logic.
- Missing-image chaser templates.

## Acceptance criteria

- Staff can see which required image categories are present/missing.
- Duplicate or poor-quality images are flagged.
- Ambiguous image matches do not auto-attach silently.
- Engineer packs include image category tags and warnings.

## Risks and controls

| Risk | Control |
|---|---|
| Image assistant overstates what it knows | Use “flag” and “suggest” language only. |
| Privacy-sensitive image metadata | Minimise metadata use and document it in DPIA. |
| Mis-matched images | Human review required for ambiguous matches. |

## Suggested priority

P1/P2. Strong operational value, especially for image-based assessment workflows.
