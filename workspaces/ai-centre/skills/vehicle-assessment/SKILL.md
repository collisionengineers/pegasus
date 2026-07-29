---
name: vehicle-assessment
description: >-
  Use this skill when a Collision Engineers engineer needs a repair estimate or vehicle damage
  assessment from photos, documents, registration/VIN, or a brief. Builds the line-by-line
  costed repair estimate — operations, WUs, ABP labour rate, parts, paint, extras — each line
  justified, wrapped in an evidence-labelled engineer pack: damage catalogue, concealed-damage
  risk, repair-vs-renew, total-loss economics and PAV ratio, provisional salvage,
  roadworthiness. Triggers on: "repair estimate", "estimate the damage", "assess this vehicle",
  "vehicle assessment", "damage assessment", "look at these photos", "is it repairable", "repair
  or total loss", "repair scope", "review this estimate", "repair-cost challenge", "ABP rate" —
  even if the skill is not named. The Audatex/EVA estimate payload is always built and
  validated; the CE-branded and Audatex/EVA PDFs render by default on full assessments. For
  transcription of an existing estimate or a sole-ask Audatex-format PDF, use
  total-loss-assessment.
---

## Authority boundary

This package may produce evidence, candidates, or draft output only. `Pegasus.Core` and an authorised human own every accepted case fact, cost, category, outcome, legal position, and approval.
# Vehicle Assessment

A source-workspace evidence and candidate-estimate experiment for photo/document-led vehicle assessment — the
engineer supplies whatever they have: photos, documents, registration/VIN, estimate lines, PAV,
incident notes. This skill drafts a source-labelled candidate line-by-line repair estimate for human review — operations, WUs, labour rate, parts, paint, extras, each line justified — and wraps it
in an evidence-led engineer pack. The deliverables rule is stated once, in the Deliverables section below.

**Target voice** (use it throughout): *"The photographs show X. The likely impact path means Y
needs checking. On the present evidence Z remains unconfirmed. The next evidence required is
A/B/C. Current manufacturer methods should govern any structural, bonded, aluminium, composite,
restraint, ADAS or HV operation."*

**Label every material conclusion** as one of: `official/live source`, `maintained reference`,
`case evidence`, or `inference` — see `references/source-governance.md`.

## Core workflow

1. **Take in the instruction and evidence.** Read `references/photo-and-evidence-intake.md`.
   Record the instruction fields, classify each supplied item, and note what is missing. Ask
   clarifying questions only where the answer would materially change the assessment — group
   them (1–3 short questions, sent together) and state defaulted assumptions instead of asking.

2. **Identify the vehicle.** Registration/VIN, DVLA/DVSA, MOT, mileage, tax/SORN, recall,
   marker, and identity facts come from the `vehicle-history-check` skill — never from memory or
   photos alone. Cross-check badging against VIN (badge retrofits are common) and state the
   identification basis in the pack.

3. **Catalogue the visible damage.** Walk every photo using
   `references/damage-cataloguing.md` — panel, severity, repair/renew view, side determination,
   non-obvious damage signals, unroadworthy phrase bank.

4. **Reason the impact path.** Read `references/vehicle-body-repair-principles.md` and
   `references/structural-and-alignment-evidence.md`. Separate contact damage from likely
   transferred damage; keep visible damage separate from concealed-damage risk; state what strip,
   measurement, or geometry evidence is still required.

5. **Escalate affected systems.** Check the impact zone against
   `references/post-impact-escalation-matrix.v1.json` and read
   `references/post-impact-system-checks.md` (steering/suspension, tyres/wheels, brakes/ABS,
   SRS, driveline, cooling/exhaust, electrical) and `references/adas-ev-hv-prompts.md`
   (ADAS calibration, EV/HV risk, MET scope) for the matched zones.

6. **Build the repair estimate — the spine of the assessment.** Read
   `references/estimate-construction.md` and build to its costing posture — the maximum
   defensible estimate: every line that named evidence, an ABP condition, or a labelled
   inference can justify goes in with its status flag; omitting justifiable scope is as much a
   failure as inventing it. Turn the catalogue and escalations into one
   line-by-line operations list: repair/renew per panel
   (`references/repair-renew-decision-matrix.md`, `references/material-and-joining-cautions.md`,
   `references/refinish-and-corrosion-protection.md`); labour rate (`references/labour-rates.md`
   — state the location evidence behind any regional-uplift call); paint and blend routing;
   parts with the unpriced convention; ABP extras (`references/extras-package.md`,
   `references/abp-reference-data.2026.json`). Give every line a one-line justification and a
   status flag. Express the list as `assessment_payload.json` (`references/eva-routing.md`,
   `references/gotchas.md`) and validate it — **the validated payload is the estimate**; run
   the estimate sanity checks now. Where scope depends on OEM/Thatcham methods, route to
   `manufacturer-methods-evidence`.

7. **Set the economics on the estimate.** The repair total is only ever the sum of the stated
   lines. Compare it to every candidate PAV (engineer-adopted or `vehicle-valuation` — never
   invented; guide screens count as case-evidence candidates) with the ratio and any
   instructed-ceiling arithmetic — read `references/total-loss-and-salvage-routing.md`. An
   instructed ceiling caps authorisation, never costing.

8. **Check competence boundaries.** Read `references/aqp-competence-boundaries.md`. Structural
   condemnation, final salvage category, HV make-safe, fire/water, motorcycle, and HGV
   conclusions stay provisional pending engineer/AQP review.

9. **Deliver the estimate-first pack.** Follow `references/assessment-output-structure.md` —
   the estimate table is section 1; the closing summary's Repair total is always a figure.

10. **Render per the deliverables rule below.** Render checks (connector health, template
    shape, house-style lint) happen now, at render time — never before the assessment content
    exists.

## Deliverables

**The estimate is the deliverable; documents are renderings of it.**

- **Always** — the estimate-first pack in chat, built around the line-by-line estimate table
  projected from the validated `assessment_payload.json`. Never optional; never depends on
  rendering. A user opt-out of documents opts out of the PDFs only — acknowledge it and still
  deliver the pack with the costed table.
- **Default on full assessments** (skip only on narrow single-question asks — e.g. a rate
  query — or explicit opt-out): the **Audatex/EVA PDF** and the **CE-branded PDF**, built as
  below.
- **On request** — external dispute/addendum wording: read
  `references/addendum-and-dispute-response.md` for the evidence gates and document structure,
  then draft through `ce-house-style` once the technical position is set.

**Audatex/EVA PDF.** Read `references/eva-routing.md` and `references/gotchas.md` first. The
validated `assessment_payload.json` already exists from workflow step 6
(shape: `scripts/assessment_payload.schema.json`), so rendering is cheap. Validate:

```bash
python scripts/validate_assessment_payload.py assessment_payload.json
```

then render with the frozen generator from the skill root:

```python
import json, sys
sys.path.insert(0, 'scripts')
from audatex_gen_v4 import build_pdf
data = json.load(open('assessment_payload.json', encoding='utf-8'))
result = build_pdf('output/AIXXXXXX.pdf', data)
print(f"Grand inc VAT: £{result['totals']['grand_inc_vat']:,.2f}")
```

`scripts/audatex_gen_v4.py` is frozen — **never modify it**; fix the input payload instead. This
output deliberately mimics Audatex formatting for EVA import: no CE branding, and it does not
route through `collisionrenderer`. It requires a local Python runtime with `reportlab` — on
hosts without Python (staff Claude Desktop machines), still build and present the validated
payload and surface the render limitation plainly rather than skipping this deliverable
silently. If the instruction was solely "build the Audatex PDF" with no broader assessment, use
the `total-loss-assessment` skill, which owns that workflow end to end.

**CE-branded PDF.** `collisionrenderer` only, rendered once on the template matching the
settled outcome — `templateId: expert-report` while unsettled, `total-loss-report` /
`repairable-contract-repair-report` once the outcome is settled. Build the camelCase payload
per the mapping in `references/assessment-output-structure.md` — including the estimate
datatable and the closing summary datatable — keep PDF-rendered prose within `ce-house-style`
(lint with its `scripts/lint_house_style.py
--payload` where Python is available; otherwise apply its banned-terms list in-context), then
connector `validate`, then `render`. This is the only render path for the branded document: if
the connector is unavailable, present the validated payload and stop — do not fall back to any
other renderer.

## Routing to specialist skills

| Need | Skill |
|---|---|
| Registration/VIN, DVLA/DVSA, MOT, mileage, tax, recalls, markers, clone/identity | `vehicle-history-check` (canonical intake — do not re-derive these facts) |
| PAV or post-repair market value | `vehicle-valuation` |
| OEM/Thatcham method evidence: blending, painted sensors, wheel/tyre, steering, structural measurement, bench, calibration, method disputes | `manufacturer-methods-evidence` |
| Cat A/B/S/N, AQP support, structural/non-structural status, HV/fire/water/motorcycle salvage, salvage-rate disputes | `salvage-categorisation` |
| Diminution in value (report or rebuttal) | `diminution-report` / `diminution-rebuttal` — the pack flags the trigger only |
| HS taxi/private-hire roadworthy report from a completed assessment | `roadworthy-report` |
| Audatex-format EVA-import PDF as the sole ask, no broader assessment wanted | `total-loss-assessment` |
| Voice/tone for any external wording or PDF-rendered prose | `ce-house-style` |

## Boundaries

- Do not invent part numbers, exact repair methods, VIN-decoded facts, PAV, mileage,
  official-source conclusions, or a final salvage category from photos alone.
- State when a position rests on desktop/photo evidence and name the inspection, diagnostic,
  geometry, strip, source lookup, or AQP review still required.
- OEM/Thatcham material is pointer-only: no procedures, diagrams, dimensions, cut lines, weld
  counts, tolerances, or step text.
- ABP figures are a maintained reference with effective/verified metadata — verify against the
  current ABP guide when rates are legally or commercially material
  (https://www.abpclub.co.uk/publications).
- Salvage reasoning stays provisional unless the evidence is AQP-grade; check the current ABI
  Code before external reliance.
- Privacy: do not republish registrations, VINs, addresses, claim references, faces, locations,
  or EXIF/GPS from supplied evidence beyond what the assessment requires; fixtures and examples
  use synthetic identifiers only.

## References

**Read on every job (the estimate chain):**

- `references/photo-and-evidence-intake.md` — instruction fields, photo/document checklists, evidence labels, privacy
- `references/source-governance.md` — conclusion labels, source hierarchy, stale-source and date-scoped-rate rules
- `references/damage-cataloguing.md` — photo walkthrough, side determination, non-obvious damage, unroadworthy phrases
- `references/estimate-construction.md` — the estimate spec: candidate costing posture, canonical table, status flags, justification standard, ceiling rule, sanity checks
- `references/labour-rates.md` — ABP 2026 rates prose guide
- `references/extras-package.md` — ABP 2026 extras and conditions
- `references/abp-reference-data.2026.json` — structured ABP 2026 rates, extras, parts charges, exclusions
- `references/eva-routing.md` — Audatex/EVA output: operation types and routing traps
- `references/gotchas.md` — verified mistakes from previous sessions; read before building any output
- `references/assessment-output-structure.md` — pack section order and branded-PDF payload mapping

**Read when triggered:**

- `references/vehicle-body-repair-principles.md` — when reasoning the impact path or repair sequence
- `references/structural-and-alignment-evidence.md` — when structural involvement or geometry evidence is in question
- `references/repair-renew-decision-matrix.md` — when a repair-vs-renew call is borderline
- `references/material-and-joining-cautions.md` — when UHSS, aluminium, plastics, composites, or adhesives are in scope
- `references/post-impact-system-checks.md` — when the impact zone touches mechanical/safety systems
- `references/adas-ev-hv-prompts.md` — when ADAS, EV/HV, or MET scope may apply
- `references/refinish-and-corrosion-protection.md` — when refinish scope, blend, or corrosion reinstatement needs detail
- `references/post-impact-escalation-matrix.v1.json` — when matching impact zones to system escalations
- `references/aqp-competence-boundaries.md` — when a conclusion may exceed desktop competence
- `references/total-loss-and-salvage-routing.md` — when economics, thresholds, or provisional salvage are in play
- `references/addendum-and-dispute-response.md` — when a dispute response or addendum is requested
- `scripts/validate_assessment_payload.py` / `scripts/assessment_payload.schema.json` — Audatex payload gate
- `scripts/validate_abp_reference_data.py` — ABP structured-data check
- `scripts/validate_escalation_matrix.py` — escalation-matrix structure check
