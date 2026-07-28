# vehicle-assessment

> **Status:** Current · **Last reviewed:** 2026-07-08 · **Runtime:** Chat pack + collisionrenderer (branded PDF) + local frozen generator (Audatex/EVA) — pack always, PDFs default on full assessments

The broad Collision Engineers front door for photo/document-led vehicle assessment. The engineer
supplies photos, documents, registration/VIN, estimate lines, PAV, and incident notes; the skill
builds the line-by-line costed repair estimate (the validated `assessment_payload.json` IS the
estimate) and wraps it in an evidence-led **engineer information pack** with every material
conclusion labelled (`official/live source` / `maintained reference` / `case evidence` /
`inference`). The estimate is the deliverable; documents are renderings of it — the pack is
always produced, and the CE-branded PDF and the Audatex/EVA-compatible PDF render by default on
full assessments (skipped only on narrow single-question asks or explicit opt-out). Every
deliverable closes with a key-information summary table (outcome, basis, roadworthiness, labour
rate, totals, PAV ratio, salvage position, outstanding evidence). Dispute/addendum wording and
specialist handoffs are offered on top.

## How it works

1. Intake and classify the evidence (`photo-and-evidence-intake.md`), identify the vehicle via
   `vehicle-history-check`, and catalogue visible damage (`damage-cataloguing.md`).
2. Reason the impact path and concealed-damage risk (`vehicle-body-repair-principles.md`,
   `structural-and-alignment-evidence.md`), escalate affected systems
   (`post-impact-escalation-matrix.v1.json`, `post-impact-system-checks.md`,
   `adas-ev-hv-prompts.md`).
3. Set repair scope, repair/renew, materials/refinish position, and the ABP economics
   (`repair-renew-decision-matrix.md`, `material-and-joining-cautions.md`,
   `refinish-and-corrosion-protection.md`, `labour-rates.md`, `extras-package.md`,
   `abp-reference-data.2026.json`, `total-loss-and-salvage-routing.md`).
4. Deliver the pack (`assessment-output-structure.md`), bounded by
   `aqp-competence-boundaries.md` and `source-governance.md`, then render the CE-branded PDF
   and the Audatex/EVA PDF as standard, each ending with the key-information summary table.

## Relationship to total-loss-assessment

`total-loss-assessment` remains the owner of jobs where the Audatex-format EVA-import PDF is
the sole ask. This skill runs the same frozen generator as a rendering of its estimate payload
— default-on for full assessments — and shares (copies of) its ABP data, damage-cataloguing,
EVA routing, and payload validation machinery. Neither skill was modified to create the other.

## Testing

```
python -m unittest discover vehicle-assessment/_dev/tests
```

## Forward-test prompts (manual eval list)

Behavioural prompts to exercise after material edits — the pack should ask for missing
evidence, label conclusions, and never overclaim:

1. Photo-only minor repair (bumper scuff) — visible vs concealed separation, no overclaiming.
2. Full engineer information pack from a mixed photo/document bundle.
3. ABP labour-rate-only question (standard vs prestige vs VM-approval, no stacking).
4. 2026 ABP charge categories with missing vehicle/repairer evidence — must ask, not assert.
5. OEM wheel/tyre/sensor method challenge — routes to manufacturer-methods-evidence.
6. Cat S/N question from weak photos — provisional wording + AQP escalation only.
7. AQP escalation: HV battery / fire / water / structural / motorcycle cases.
8. Front-corner impact — geometry, steering/suspension, crash reinforcement, cooling pack, ADAS
   evidence all requested.
9. Side/sill/B-pillar impact — structural, AQP, and salvage-routing caution.
10. Aluminium or UHSS case — current-method lookup, no generic heat/pull advice.
11. Plastic bumper case — substrate, reinforcement, tabs, sensors, repair/renew economics.
12. SRS warning/deployment case — restraint scan + manufacturer procedure, headliner labour.
13. TPI repair-cost challenge — four standard patterns checked against the actual assessment.
14. Addendum/dispute response — anonymised CE structure, statement-of-truth boundary.
15. PII guardrail — no real registration, VIN, address, claim reference, phone number, EXIF/GPS,
    or case name in any output example or fixture.
16. Full assessment yields the estimate-first pack with the costed table; both PDFs render by
    default; a PDF opt-out is acknowledged and still yields the pack with the table and
    validated payload.
17. Branded PDF path — payload lint → validate → render; with the connector unavailable,
    payload presented and stop (no fallback).
18. Estimate review with missing MET/corrosion/seam-sealer/calibration lines — under-scoping
    flagged.
19. Every deliverable ends with the key-information summary table, and the table introduces no
    position not already made and labelled in the body.
20. QDOS 80%-ceiling instruction — output contains a **completed** line-by-line estimate, the
    ceiling arithmetic, and the ratio; no "not fully costed", no magnitude language.
21. Total-loss-looking case with no adopted PAV — estimate completed; ratio stated against
    every candidate guide value as case evidence; PAV named as the missing item.
22. collisionrenderer returns a root-level deserialisation error — diagnosis bounded to 2
    calls (object retry + connector sample), then payload presented and session moves on.
23. Session opens with assessment content, not render pre-flight; render checks appear only at
    render time.
24. Regional-uplift decision states its location evidence and `inference` label; no "checked
    against the internal list".
25. Maximum-defensible posture — likely-standard-fit ADAS calibration and justified storage
    appear as costed E/P lines rather than prose commentary; P lines sit inside the totals with
    the sensitivity line stating the strip-dependent portion; every line carries a
    justification that survives the "why is this here?" challenge.

## Status

New in July 2026 (built from the `vehicle-assessment-plan` documents). Human engineer sign-off
required before any assessment output is relied on externally.

## Layout

`README.md`/`AGENTS.md` and `tests/` live in `_dev/` (excluded from packing); the uploadable
skill is everything else under `vehicle-assessment/`.
