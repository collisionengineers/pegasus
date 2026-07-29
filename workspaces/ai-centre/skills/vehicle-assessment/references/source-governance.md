# Source Governance

> **Source-workspace boundary:** This file is an experimental annotation scheme below root documentation, `Pegasus.Core`, and operator authority; it is not a Pegasus caller, policy, current instruction, or acceptance authority.


How conclusions are labelled, which source wins when they disagree, and when a source is too
stale to rely on.

## The four conclusion labels

Every material conclusion in the pack carries exactly one label:

| Label | Meaning | Examples |
|---|---|---|
| `official/live source` | Verified against a current authoritative source in this assessment | DVLA/DVSA facts via `vehicle-history-check`; the current ABP guide checked today; the current ABI Code |
| `maintained reference` | From this skill's curated data with effective/verified metadata | `abp-reference-data.2026.json`; the escalation matrix; repair-principles logic |
| `case evidence` | From the material supplied in this case | Photos, the repairer's estimate, the owner's account, incident description |
| `inference` | Engineering reasoning from the above, not directly evidenced | Likely transferred damage along the impact path; provisional repair/renew view |

Owner-reported and unverified mechanical symptoms are `case evidence` and take "we understand"
wording. Anything the skill could not verify stays `inference` or `case evidence` — never promote
a label to make a conclusion sound firmer. The labels cut both ways: never drop a justifiable
estimate line because its basis is `inference` — a labelled inference with a stated
justification is a legitimate costing basis (status `E`/`P`); defensibility comes from the
label and the justification, not from omission.

Label at the tightest level that avoids repetition: a section whose heading declares its label
is labelled once at the heading; mixed content — including every estimate-table row — is
labelled per item. Never double-label inside an already-segregated section.

## Source hierarchy

When sources disagree, the higher one governs; note the disagreement rather than blending:

1. Current OEM/manufacturer method data and Thatcham (for repair method, permitted repair,
   sectioning, joining, calibration) — pointer-only via `manufacturer-methods-evidence`.
2. Official/live records: DVLA/DVSA (identity, MOT, mileage, markers), the current ABI Code of
   Practice (salvage), the current ABP guide (charges).
3. This skill's maintained references (dated, versioned, reviewed).
4. Case evidence supplied for this assessment.
5. General engineering reasoning (book-derived assessment logic) — informs judgement and prompts,
   never a method or categorisation authority.

## Stale-source rules

- **ABP**: `abp-reference-data.2026.json` is the maintained default from 2026-01-01
  (`verified_date` 2026-07-07, `review_by` 2027-01-01). Verify against
  https://www.abpclub.co.uk/publications whenever current rates are legally or commercially
  material. Past the review date, say the data is due for re-verification.
- **Date-scoped rates**: select the ABP year by the assessment/repair/estimate date. A 2025-dated
  estimate is challenged on 2025 figures, stated as date-scoped historical evidence — 2026
  figures are not applied backwards, and 2025 figures are never a current default.
- **ABI Code**: salvage reasoning stays provisional; cite or check the current ABI publication
  where exact wording matters
  (https://www.abi.org.uk/globalassets/files/publications/public/motor/2025/codepracticecategorisationmotorisedvehiclesalvagemay2025.pdf
  was current at last review — confirm before external reliance).
- **AQP syllabus-derived boundaries**: scope guidance only; check whether a newer IAEA syllabus
  exists before treating details as current.
- **OEM/Thatcham pointers**: require current-source verification before reliance; methods change
  by model year and revision.

## No-invention rules

Never invent, estimate-as-fact, or fill in:

- Part numbers or catalogue prices (estimated prices and WUs are stated, marked `E`/`P`/`*`,
  and justified in the estimate table and payload — never withheld; see
  `estimate-construction.md`).
- Exact repair methods, cut lines, weld counts, tolerances, torque values, or heat limits.
- VIN-decoded facts, mileage, MOT history, or marker status without a
  `vehicle-history-check` lookup.
- PAV or salvage values — these come from the engineer or `vehicle-valuation`.
- A final salvage category, structural condemnation, or roadworthiness verdict from photos alone.

## Desktop caveat

Where the assessment rests on desktop/photo evidence, say so once, plainly, and name what is
outstanding: inspection, diagnostic scan, geometry measurement, strip, source lookup, or AQP
review. One clear caveat beats stacked hedging on every line. State the caveat once in the
Confidence section with a single provisional-pending list; the estimate table's status flags
carry the per-line uncertainty — do not also write "provisional" into line prose.
