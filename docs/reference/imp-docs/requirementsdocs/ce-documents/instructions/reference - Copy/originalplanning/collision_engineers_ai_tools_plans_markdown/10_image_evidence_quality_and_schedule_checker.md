# Image Evidence Quality and Schedule Checker

Generated: 2026-05-22

**Type:** Vision tool + skill
**Priority:** High

## Objective

Check whether vehicle images are present, usable, correctly ordered for EVA, and sufficient for engineer review.

## Why it matters for Collision Engineers

The EVA guide specifies that the first two uploaded images have special meaning: first overall vehicle showing registration, second close-up damage image, then all images including those two. The Phase 3 plan also calls for checking full vehicle/damage close-up and image quality.

## Proposed shape

A vision tool analyses images and creates an image schedule: overall vehicle, visible VRM, damage close-up, side/angle, interior/dashboard mileage, poor quality, duplicate, reflection/privacy issue.

## Candidate tools / MCP methods / skill actions

- `classify_vehicle_image(image_id)`
- `check_image_quality(image_id)`
- `detect_dashboard_mileage(image_id)`
- `detect_visible_vrm(image_id)`
- `generate_eva_photo_order(work_item_id)`
- `create_image_schedule(work_item_id)`
- `flag_reflection_or_sensitive_image(image_id)`

## Inputs

- Images from email/Box/DOC/PDF extraction
- work item vehicle details
- EVA photo order rules

## Outputs

- Image roles
- quality flags
- suggested EVA order
- missing angles
- dashboard mileage evidence
- privacy/reflection warning

## Guardrails

- Do not identify people beyond operational/privacy flags.
- Engineer decides adequacy.
- Do not discard images automatically.
- Handle visible registration as sensitive data.
- Keep confidence per image role.

## MVP implementation path

1. Start with manual-review image schedule plus LLM/vision classifications.
2. Add simple blur/darkness checks.
3. Add dashboard mileage extraction only when confidence is high.
4. Add EVA upload order recommendation.

## Test / acceptance criteria

- Known image set produces first overall and second damage close-up.
- Blurred image flagged.
- Dashboard mileage detection compared with human label.
- Duplicate images detected.

## Risks and open questions

- Vehicle images vary widely.
- Reflections may include people.
- Vision errors must be reviewable.
- OCR on dashboards may be noisy.

## Project source basis

- phase_ai_tools.md
- EVA User Guide.pdf
- OpenAI vision docs

## External reference basis

- OpenAI vision/image input documentation: https://developers.openai.com/api/docs/guides/images-vision
