---
name: total-loss-assessment
description: >-
  Use this source-workspace rendering experiment only when an authorised human has supplied an accepted payload for an Audatex-format
  EVA-import draft:
  transcription of an existing estimate ("transcription job", "match this
  estimate to the penny"), rendering already-approved operations. Triggers on: "transcribe this estimate", "build the assessment
  PDF", "Audatex PDF", "EVA import", "cost target". When an engineering
  opinion is wanted — a repair estimate or damage assessment from photos or a
  brief, panel assessment, repair scope, repairability or total-loss opinion,
  or repair-cost/ABP/storage charge challenges — use vehicle-assessment, which
  produces this same Audatex/EVA PDF in its estimate-first pack. IMPORTANT:
  the output mimics Audatex format for EVA import; do NOT apply
  collision-engineers-design styling. This is NOT the `eva` connector, which
  only reads existing reports from the EVA Sentry API.
---

## Authority boundary

This package may produce evidence, candidates, or draft output only. `Pegasus.Core` and an authorised human own every accepted case fact, cost, category, outcome, legal position, and approval.
# Total Loss / Damage Assessment

Render a draft Audatex-format PDF from an already accepted source payload. This package does not assess damage, choose operations, target a cost, or decide total loss.

**Architecture — two-stage, do not blur:**
1. An authorised human supplies accepted vehicle facts and operations; the package projects them into the validated payload. It does not infer or decide them.
2. **`scripts/validate_assessment_payload.py`** validates that dict against the generator's required shape and routing rules.
3. **`scripts/audatex_gen_v4.py`** takes the validated dict and produces the byte-identical EVA-compatible PDF. Pure deterministic code — **never modify it**.

**No brand layer.** This output mimics Audatex format for EVA import. Do not apply `collision-engineers-design` document styling. `ce-house-style` applies to the **chat summary** only — not to the PDF.

**Assessment boundary.** If the supplied payload needs vehicle identification, damage cataloguing, operation selection, repair/renewal judgment, rates, economics, or a total-loss opinion, stop and route that work to `vehicle-assessment`. `roadworthy-report` is inactive and must not be invoked.

## Core workflow

1. **Confirm the input boundary.** Require a source-labelled payload that an
   authorised human has accepted for rendering. Do not derive missing facts from
   photos, correspondence, rates, defaults, or other reference material. Stop
   if any operation, amount, vehicle fact, or outcome still needs a decision.

2. **Preserve provenance.** Keep the accepted payload unchanged as
   `assessment_payload.json`. Record its source identity and the approving
   human; never overwrite the source artifact with validator or render output.

3. **Validate before rendering.** Use
   `scripts/assessment_payload.schema.json` and run:
   ```bash
   python scripts/validate_assessment_payload.py assessment_payload.json
   ```
   Return validation failures to the authorised reviewer. A warning is evidence
   for review, not permission for this package to alter an operation or value.

4. **Write the render script as a `.py` file:**
   ```python
   import json
   import sys
   sys.path.insert(0, 'scripts')  # run from this skill's root directory
   from audatex_gen_v4 import build_pdf

   data = json.load(open('assessment_payload.json', encoding='utf-8'))

   result = build_pdf('output/AIXXXXXX.pdf', data)
   t = result['totals']
   print(f"Grand inc VAT: £{t['grand_inc_vat']:,.2f}")
   print(f"Pages: {result['total_pages']}")
   ```

5. **Render and present the draft.** Run the script, copy the PDF to the output
   folder, and label it as an unaccepted rendering draft tied to the exact
   accepted source payload. Summarise only payload values, renderer totals,
   validation warnings, and render limitations; do not add assessment opinions
   or guessed values.

## Render path

`scripts/audatex_gen_v4.py` is the only render path for this PDF. It requires a Python runtime, so the skill runs end-to-end on dev machines only (staff Claude Desktop machines have no Python — surface that limitation instead of presenting the render as available there). Do not route this output through the `collisionrenderer` connector: its `total-loss-report` template is the CE-branded expert report, a different document — this skill's output deliberately mimics Audatex formatting for EVA import.

## Defaults

- All operations are passed explicitly — the generator no longer auto-includes STANDARD items.
- Labour time basis: 10 WU = 1 hour.
- VAT: 20%, applied automatically by the generator.
- `sundry_parts_pct`: 3.5 (set to 0.0 for transcription jobs — the source estimate already has its own markup).
- Address / contact: Collision Engineers Ltd, 77–79 Hoylake Road, Moreton, Wirral, CH46 9PY. **Always use CH46 9PY** — even if a third-party letter shows a different postcode. The chrome is part of the tested layout.

## On cost targeting

- Cost-targeting, threshold-seeking, and instructions to inflate or suppress a total are prohibited. Stop and request an accepted source payload.

## References

Active rendering contracts:

- `scripts/assessment_payload.schema.json` — JSON Schema for the accepted operations payload
- `scripts/validate_assessment_payload.py` — validator for required keys and operation routing

Retained assessment-source evidence, not instructions for this rendering-only
workflow: `references/abp-reference-data.2026.json`,
`references/labour-rates.md`, `references/extras-package.md`,
`references/eva-routing.md`, `references/damage-cataloguing.md`,
`references/dispute-response-boundaries.md`, and `references/gotchas.md`.
If any of that evidence must be interpreted, stop and use
`vehicle-assessment` under Core/operator approval.
