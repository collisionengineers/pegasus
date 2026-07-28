# Visible VRM and Image Case Matcher

Generated: 2026-05-22

**Type:** Vision + matching tool
**Priority:** Medium

## Objective

Use visible registration plates, image metadata, sender/time, and work-item context to match separately received vehicle photos to the right case.

## Why it matters for Collision Engineers

Current pain points include images arriving separately, weak filenames, and risk of misfiled evidence. The whiteboard and context packs show Outlook/WhatsApp/image-heavy workflows.

## Proposed shape

This tool augments evidence matching by reading visible VRM when possible, matching to canonical work items, and routing uncertain images to review.

## Candidate tools / MCP methods / skill actions

- `extract_visible_registration(image_id)`
- `match_image_to_work_item(image_id)`
- `find_unmatched_images(reference_or_sender?)`
- `approve_image_match(image_id, work_item_id)`
- `reject_image_match(image_id, reason)`

## Inputs

- Image file
- email/WhatsApp metadata
- candidate work items
- VRM/reference from image/text
- time proximity

## Outputs

- Suggested case match
- confidence and reasons
- visible VRM evidence
- review-required status

## Guardrails

- Never auto-match low confidence.
- Use multiple signals, not VRM alone.
- Keep human approval history.
- Do not alter original files.
- Log false matches for retraining.

## MVP implementation path

1. Build image OCR for plates as experimental.
2. Combine with sender/time and open work-item candidates.
3. Create unmatched images queue.
4. Add reviewer approval interface.

## Test / acceptance criteria

- Separate image email is linked correctly in test corpus.
- Conflicting VRM causes review.
- No candidate remains unmatched.
- Duplicate images do not create duplicate case evidence.

## Risks and open questions

- Number plate visibility is inconsistent.
- Private plates and blur/angle problems.
- False positives are operationally risky.
- WhatsApp metadata may be limited.

## Project source basis

- context_pack_1/04_current_workflow_and_pain_points.md
- context_pack_2/05_OUTLOOK_INTAKE.md
- Collision Engineers Whiteboard.jam

## External reference basis

- OpenAI vision/image input documentation: https://developers.openai.com/api/docs/guides/images-vision
