---
name: vehicle-history-check
description: Use this skill whenever the user wants a UK vehicle history, provenance, mileage, or roadworthiness check from a registration (or VIN) — MOT history and status, mileage validation / clocking / "is the mileage genuine", a current mileage estimate, outstanding recalls, defect history, tax/SORN status, pre-incident condition before an accident date, identity/clone/export-marker risk, or a combined assessment context. Triggers on phrases like "check this reg", "MOT history", "is the mileage right", "mileage anomalies", "clocked", "estimate current mileage", "outstanding recall", "is it roadworthy / legal to drive", "clone check", "export marker", "Cat S/N history", "pre-incident MOT", "DVLA/DVSA check", "vehicle history", "provenance" — even if the skill is not named. Also use as a fact-gathering pre-step before vehicle-valuation or roadworthy-report.
---

> **Source-workspace boundary:** Preserve connector-facing fact-gathering workflow, but relabel it as source-pack behavior and remove any implication of an AI Centre live caller or policy authority. This is evidence, an example, a package-local format, or an experiment only; `Pegasus.Core`, current operator authority, and an authorised human own every accepted fact, cost, category, outcome, legal position, send, report issue, and approval.
# Vehicle History Check

Pull authoritative UK government vehicle data via the **`dvsa-mot`** MCP connector
(DVSA MOT History API + DVLA Vehicle Enquiry Service) and turn it into a clear, defensible
history/provenance summary. This skill is the standalone front-end for the connector — the
same data also feeds `vehicle-valuation` (subject-vehicle facts) and `roadworthy-report`
(baseline status). It does **no** scraping or valuation; it reports government records.

## Canonical for: subject-vehicle facts & mileage estimate

This skill is the **single source of truth** for gathering subject-vehicle identity (reg/VIN →
make/model/year/fuel) and for the **mileage-estimate-from-MOT** definition. Other skills should
route their subject-fact intake here rather than re-implementing it.

- **Intended consumers (route subject-fact intake here):** `vehicle-valuation`,
  `roadworthy-report`, `total-loss-assessment`, `diminution-rebuttal`, `diminution-report`,
  `salvage-categorisation`.
- **The mileage-estimate definition (canonical):** use the latest MOT reading as the
  current-mileage estimate, projecting forward at the annualised rate from recent clean MOT
  intervals (`current_mileage_estimate`). Always disclose it as an **estimate for assessment
  purposes, not a substitute for a physical odometer reading**, with the low/high band and
  confidence. Callers must reuse this definition and **never ask the user to state mileage** when
  an MOT reading exists. See "Mileage estimate — handling" below.

## When to use
- A user gives a registration (or VIN) and wants any of: MOT history/status, mileage
  validation, a **current mileage estimate**, recalls, defects, tax/SORN, roadworthy
  go/no-go, clone/export risk, or pre-incident condition.
- As a **pre-step** for a valuation or roadworthy report — gather the government facts first,
  then hand off (see Hand-offs).

## Core workflow
1. **Normalise the input.** Accept a registration (spaces tolerated) or 17-char VIN. Never
   ask the user for data the connector returns (make/model/year/colour/fuel/MOT/mileage) —
   fetch it.
2. **For a one-shot overview**, call `summarise_assessment_context` (registration, optional
   `incident_date`, `include_dvla=true`) — it bundles identity, MOT status, recall, mileage
   anomalies, roadworthy compliance, cross-source checks, and (if given) pre-incident
   condition in one call.
3. **For specific questions**, call the focused tools:
   - Identity card: `get_vehicle_valuation_facts` / `get_vehicle_summary` / `get_vehicle_dvla_summary`.
   - Mileage: `get_mileage_history` + `detect_mileage_anomalies`; for "what should it read now",
     `current_mileage_estimate` (returns an estimate, low/high band, confidence, and caveats —
     present it as an estimate, never as fact; always surface its caveats). To check whether a
     stated or observed mileage is plausible, such as an advert figure or a physical reading, call
     `assess_mileage_plausibility(registration, observed_mileage)`; this is distinct from the
     estimator. Surface its verdict and caveats verbatim, and treat `BELOW_LAST_MOT` as a clocking
     red flag unless reconciled by evidence.
   - Legality: `get_roadworthy_compliance` (tax + MOT + recall → legally_drivable + blockers),
     `get_mot_status`, `get_tax_status`, `get_recall_status`.
   - Provenance/risk: `check_export_clone_risk`, `verify_vehicle_identity_full`,
     `get_export_marker`, `get_v5c_status`.
   - Condition: `get_defect_history` (optionally filter severity), `get_pre_incident_condition`
     (with the incident date).
4. **Report plainly.** Lead with the headline (e.g. "Legally drivable: yes/no", "Mileage:
   consistent / anomaly found"). Quote the connector's own explanations. For ULEZ/emissions,
   reproduce the connector's own `ulez_note` caveat verbatim (it directs the reader to verify
   against Transport for London before quoting) — never paraphrase or restate it as definitive.

## Mileage estimate — handling
`current_mileage_estimate` annualises a rate from the most recent clean MOT intervals and
projects to today. Always state it is an **estimate for assessment purposes, not a
substitute for a physical odometer reading**, give the low/high band and the confidence
(HIGH/MEDIUM/LOW/VERY_LOW), and note any anomalies it considered. If confidence is
VERY_LOW (sparse history, clocking, or a reading >5 years old), say so and lean on the last
known reading.

For a stated or observed mileage supplied by the user, call `assess_mileage_plausibility` rather
than trying to compare the figure manually. It judges the input against the last MOT reading and
the estimated range at the observation date. Quote the connector's `verdict`, `reasons`, and
`caveats` directly.

## Hand-offs
- For a **market valuation** of the vehicle, hand the gathered facts to `vehicle-valuation`
  (it consumes the same subject-vehicle facts and then sources live comparable adverts).
- For a **roadworthy / re-insurance report**, hand the MOT/roadworthy facts to
  `roadworthy-report`.
- For **written output** (a client note summarising the check), apply `ce-house-style`. If the
  user wants a formal client-facing history/compliance document rather than a chat summary,
  render it via the `collisionrenderer` connector (`templateId: expert-report`) with
  `collision-engineers-design` layout — this skill only gathers and explains the facts.

## Boundaries
- Government records only — no advert scraping, no valuation, no PDF rendering here.
- Never ask the user to confirm facts the connector already returns.
- Mileage estimate and ULEZ are advisory; never present them as definitive.
- If the connector is unavailable, say so and stop — do not guess vehicle facts.
