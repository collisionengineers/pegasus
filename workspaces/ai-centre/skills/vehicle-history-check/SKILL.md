---
name: vehicle-history-check
description: >-
  Use this source-workspace package only to inspect or evaluate supplied,
  source-labelled UK vehicle-history evidence and the proposed dvsa-mot
  connector contract. It has no live Pegasus caller and must not invoke an
  external service.
---

# Vehicle History Check

## Canonical for: subject-vehicle facts & mileage estimate

This historical heading is retained for claim traceability; the package is
**not** canonical for Pegasus vehicle identity or mileage. It records a
proposed `dvsa-mot` MCP surface backed by DVSA MOT History and DVLA Vehicle
Enquiry data. `Pegasus.Core`, current operator authority, and an authorised
human own every accepted vehicle fact, mileage conclusion, roadworthiness
outcome, external operation, send, report issue, and approval.

Tool names such as `summarise_assessment_context`,
`get_vehicle_valuation_facts`, `get_vehicle_summary`,
`get_vehicle_dvla_summary`, `get_mileage_history`,
`detect_mileage_anomalies`, `current_mileage_estimate`,
`assess_mileage_plausibility`, `get_roadworthy_compliance`,
`get_mot_status`, `get_tax_status`, `get_recall_status`,
`check_export_clone_risk`, `verify_vehicle_identity_full`,
`get_export_marker`, `get_v5c_status`, `get_defect_history`, and
`get_pre_incident_condition` are package-local contract evidence. Their
presence does not prove availability, authentication, provider approval,
deployment, a Pegasus adapter, or an application caller.

## When to use

Use this package only to review an immutable local response or other
source-labelled evidence that an authorised human has supplied for evaluation.
Do not fetch by registration or VIN. A future integration requires an accepted
contract, approved exact service/identity/read operation, a
`Pegasus.Infrastructure` adapter, Core-owned policy, and caller-backed proof.

## Core workflow

1. Record source identity, retrieval date if supplied, field provenance, and
   whether each value came from DVSA, DVLA, or another named source.
2. Review supplied identity, MOT, recall, mileage, tax, defect, pre-incident,
   clone, and export fields against the proposed tool contract. Do not invoke
   those tools from this package and do not infer missing provider fields.
3. Separate observations from candidates. Provider values remain evidence
   until Core/operator rules and an authorised human accept an outcome.
4. Surface missing, stale, conflicting, or low-confidence evidence. Never turn
   absence of a response into a clear or legally-drivable status. Retain any
   provider `ulez_note` as a source-labelled caveat, not a Pegasus conclusion.
5. Produce a source-labelled draft summary for authorised review. Do not send,
   render, create a case, allocate a reference, or claim acceptance.

## Mileage estimate — handling

The retained experiment annualises recent clean MOT intervals and may project
an estimate, low/high range, confidence, and caveats. This is a candidate
algorithm, not the canonical Pegasus definition or a substitute for a physical
odometer reading. Sparse history, anomalies, or a last reading more than five
years old must remain explicit. A supplied plausibility result may be quoted
with its source, reasons, and caveats; this package does not convert
`BELOW_LAST_MOT` or any other candidate verdict into an accepted clocking
finding.

## Hand-offs

There are no current Pegasus or AI Centre consumers. Names such as
`vehicle-valuation`, `roadworthy-report`, `total-loss-assessment`,
`diminution-rebuttal`, `diminution-report`, `salvage-categorisation`,
`ce-house-style`, and `collisionrenderer` record historical/proposed package
relationships only. No data, draft, or render may be handed off until each
consumer has a separately accepted contract and a proved caller.

## Boundaries

- No live DVSA, DVLA, MCP, scraping, advert, valuation, or renderer call.
- No roadworthy/legal conclusion and no Cat S/N inference from absence.
- No reuse of vehicle data between matters.
- No claim that any named package is a current consumer.
- If supplied evidence is insufficient or the proposed connector is
  unavailable, stop and report the limitation; do not guess.
