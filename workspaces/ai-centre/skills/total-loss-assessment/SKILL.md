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

**Next step (when applicable).** A completed damage assessment is the source input for a taxi/private-hire roadworthy report — if the user then needs that, use the `roadworthy-report` skill. For the post-repair market value of a Cat-marked vehicle, `vehicle-valuation` (with its write-off comparable search) establishes the figure.

## Core workflow

1. **Identify the vehicle.** Look for the registration plate, badge/grille design, instrument cluster (mileage + warning lights), VIN plate. Cross-reference badging with VIN — badge retrofits are common on premium cars. State the VIN-decoded vehicle in your reply.

2. **Catalogue the damage.** Walk through every photo. For each visible damage point, note which panel, severity (scuff / dent / torn / destroyed), and whether it is repairable or needs renewal. Watch for non-obvious damage — see `references/damage-cataloguing.md`.

3. **Ask clarifying questions only if material.** If something would meaningfully change the assessment (PAV figure, renew vs repair on a borderline panel, full mirror assembly vs cap only) — ask before building. Group questions: 1–3 binary or short multi-choice, sent together. Don't ask things you can sensibly default and state the assumption.

4. **Decide the labour rate.** See `references/labour-rates.md` and the structured values in `references/abp-reference-data.2026.json`. Wrong rate creates a 25%+ error — ask before building if genuinely ambiguous.

5. **Build the operations list.** See `references/eva-routing.md` for operation types and the critical `specialist_wu` trap. See `references/extras-package.md` and `references/abp-reference-data.2026.json` for the default ABP 2026 package.

6. **For external repair-cost or total-loss challenges,** read
   `references/dispute-response-boundaries.md` before drafting. Use this only after the assessment
   evidence has set the repair scope and economics.

7. **Write the payload as `assessment_payload.json` and validate it before rendering.** Use `scripts/assessment_payload.schema.json` for the shape and run:
   ```bash
   python scripts/validate_assessment_payload.py assessment_payload.json
   ```
   Fix every error before rendering. Treat `specialist_wu` warnings as a routing check, not as ignorable noise.

8. **Write the render script as a `.py` file:**
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

9. **Run the script, copy the PDF to the output folder, and present it.**

10. **Summarise in chat:** identify the vehicle, list the damage, give a breakdown by section, state the PAV ratio if relevant, and explicitly flag anything you estimated or guessed (part numbers, WU judgements, renewal vs repair decisions). Plain English, no padding.

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

- `references/abp-reference-data.2026.json` — structured ABP 2026 rates, materials, extras, parts charges, conditions, and exclusions
- `references/labour-rates.md` — ABP 2026 rates prose guide (standard, prestige, VM-approval, worked combinations)
- `references/extras-package.md` — ABP 2026 default extras prose guide (always-include + conditional)
- `references/eva-routing.md` — operation types, the `specialist_wu` big trap, routing rules
- `references/damage-cataloguing.md` — what to look for when walking through photos
- `references/dispute-response-boundaries.md` — safe repair-cost and total-loss challenge responses
- `references/gotchas.md` — real mistakes from previous sessions; read before building
- `scripts/assessment_payload.schema.json` — JSON Schema for the operations payload shape
- `scripts/validate_assessment_payload.py` — stdlib validator for required generator keys and operation routing rules
