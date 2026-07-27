# Image Review, Evidence Quality and Matching

## Objective

Improve confidence that vehicle images are complete, usable, matched to the correct case, and prepared in the correct order for EVA/engineer review.

## Correct framing

This is not an autonomous damage-assessment agent. It is an **image quality and evidence completeness assistant**.

## What should be automated

- File type detection.
- Image dimensions and format checks.
- EXIF/metadata capture where available.
- Duplicate detection by hash/perceptual hash.
- Derivative thumbnail generation.
- OCR/plate read attempt where appropriate.
- Odometer image detection/read attempt where appropriate.
- VIN image detection/read attempt where appropriate.
- Required-image checklist by report type.
- EVA upload-order preparation.

## What AI/computer vision can assist with

- Tag likely image type: overall, reg plate, dashboard/odometer, VIN, front, rear, nearside, offside, damaged area, estimate screenshot, irrelevant.
- Flag blurry, dark, obstructed or reflection-heavy images.
- Identify likely mismatch between image VRM and case VRM.
- Identify whether damage zones appear to match the instruction/estimate at a high level.

## What must not be automated as final judgement

- Damage causation.
- Repairability.
- Roadworthiness.
- Fraud/reused-image allegation.
- Whether a report can rely on a specific image if the issue is material.

## Image evidence data model

```yaml
image_evidence:
  id: string
  case_id: string
  original_file_id: string
  image_hash: string
  perceptual_hash: string | null
  captured_at: datetime | null
  uploaded_at: datetime
  source_message_id: string | null
  inferred_tags: [overall_vehicle, registration, odometer, vin, damage_closeup, front, rear, nearside, offside, other]
  extracted_vrm: string | null
  extracted_mileage: integer | null
  quality_flags:
    - blurry
    - low_resolution
    - reflection
    - too_dark
    - cropped_plate
  match_status: matched | suggested | mismatch | unknown
  review_required: boolean
```

## Evidence matching checks

- VRM read from plate image matches case VRM.
- Odometer mileage is consistent with instruction/estimate/MOT/Percayso evidence.
- Images are associated with the same source/provider/thread as the case where possible.
- Duplicate images are not counted as independent evidence.
- Images with personal reflections/faces should be flagged for review before report use.

## Acceptance tests

- Overall vehicle + damage close-up are recognised and placed first for EVA instructions.
- Dashboard photo containing speedo is detected and mileage extracted with confidence score.
- Image with conflicting plate is flagged as mismatch.
- Duplicate image upload does not create duplicate evidence count.
- Low-quality image pack triggers missing/quality review, not final report readiness.
