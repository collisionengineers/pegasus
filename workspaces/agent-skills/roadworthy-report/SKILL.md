---
name: roadworthy-report
description: >-
  Use this skill whenever a Collision Engineers engineer needs to generate an HS
  (Hackney Solutions) roadworthy report from an existing accident damage
  assessment report. Triggers on: "roadworthy report", "HS report",
  "re-insurance report", "Hackney Solutions", "taxi report", "roadworthy
  certificate", "generate the roadworthy", "produce the HS", or any
  instruction to create a roadworthy / re-insurance report from an engineer's
  damage report — even if the skill is not named. IMPORTANT: Do not ask
  clarifying questions — generate from the engineer's report alone.
---

# HS Roadworthy Report

Generate HS (Hackney Solutions) roadworthy / re-insurance reports from engineers' accident damage assessment reports (for taxi and private-hire licensing authorities).

**Foundations:** consult `collision-engineers-design` for letterhead layout; `ce-house-style` applies to the short confirmation message. This skill's `references/field-mapping.md` defines exactly which 14 fields change.

**Upstream assessment:** the accident damage assessment is normally produced by the `total-loss-assessment` skill before this roadworthy report is generated.

## Critical rules

1. **Do not ask clarifying questions.** Generate the report from the engineer's report alone. If a value is missing, use the fallback in `references/field-mapping.md`.
2. **Only the 14 highlighted fields change.** Nothing else in the template is ever modified — not the wording, not the engineer's name, not the qualifications, not the paragraphs, not the footer, not the fonts.
3. **Our Ref is always the vehicle registration number.** No exceptions.
4. **Work on a copy.** Never edit `HS_roadworthy_report_template.docx` directly.

## Workflow

1. Read the engineer's report (PDF or docx) and extract the field values using `references/field-mapping.md`.
2. Write the 14-field payload as `roadworthy_input.json`. Use the keys in `references/field-mapping.md`: `registration`, `your_ref`, `header_date`, `accident_date`, `instructions_received_date`, `make`, `model`, `vin`, `cat_s`, and `damage_location`. The renderer forces the fixed values `Status=Repaired`, `Passed MOT (taxi)=TBC`, and `Legal Status=Roadworthy`.
3. Render with the deterministic local renderer:
   ```bash
   python scripts/render_roadworthy.py roadworthy_input.json --output-dir output/
   ```
   The renderer changes only approved placeholder tokens in `word/header1.xml` and `word/document.xml`. It refuses to run if `assets/HS_roadworthy_report_template.docx` is absent or missing the required placeholders. This is the only render path: if it fails, surface the error and stop — do not hand-edit the DOCX XML and do not invent a replacement HS template.
4. Save as `HS_roadworthy_<REGISTRATION>.docx` in the output directory and present.
5. Confirm with a short message. Remind the user to drag vehicle images in manually. **Do not list every field that was filled in.**

## References

- `references/field-mapping.md` — the 14 fields, their sources, and fallbacks
- `scripts/render_roadworthy.py` — validates the 14-field payload and renders a prepared HS DOCX template, failing closed if the real template is unavailable

## Style examples

Example reports in `assets/style-examples/`: `rr.pdf` and `rr1.pdf` — reference for layout and wording.
