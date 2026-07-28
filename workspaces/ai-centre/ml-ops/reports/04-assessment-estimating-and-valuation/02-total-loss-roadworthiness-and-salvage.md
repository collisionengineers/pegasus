# Total Loss, Roadworthiness and Salvage

## Executive conclusion

Historical reports can support total-loss triage, salvage-range checks and structured roadworthiness warnings. These are high-consequence decisions, so the system should remain advisory and explicitly constrained by remote evidence.

Collision Engineers does not physically inspect vehicles. A photograph-based system cannot confirm the absence of hidden structural, mechanical, electrical or safety-system damage. It can identify visible indicators, calculate scenarios and show what is unknown; the engineer must make and sign the professional conclusion.

## Separate the three decisions

These related outcomes should not be collapsed into one model label.

### Economic repairability

This compares a justified repair-cost scenario with the applicable pre-accident value, salvage assumptions and client rules. It is primarily a structured calculation informed by uncertain inputs.

### Roadworthiness or safety warning

This concerns whether visible or reported conditions indicate that use may be unsafe or that further checks are required. It is not the same as economic total loss.

### Salvage treatment

This uses vehicle facts, damage extent, category, market evidence and current commercial data to estimate a range or validate an external return.

Keeping the outputs separate makes errors visible and prevents a single opaque score from implying more certainty than the evidence supports.

## Useful training data

Case records can provide:

- final repairable/total-loss outcomes;
- repair-cost and valuation components;
- vehicle age, mileage and specification;
- visible damage pattern;
- structural or safety-system concerns;
- salvage quotation or return;
- total-loss category where present;
- engineer caveats and evidence requests;
- later changes to outcome;
- disagreement or query themes.

The completed sample already contains both repairable and total-loss cases. Thousands more cases would support useful models if the distribution is broad and the labels are reviewed.

## Proposed decision-support output

```yaml
outcome_support:
  evidence_cutoff:
  economic_scenarios:
    - repair_cost_range:
      pav_range:
      salvage_range:
      threshold_source:
      resulting_indicator:
  visible_safety_indicators:
    - finding:
      evidence_ids:
      confidence:
      required_action:
  unknowns:
    - possible concealed structural involvement
    - warning-light status not evidenced
  salvage_checks:
  categorisation_prompts:
  recommendation_status: engineer_review_required
```

The output should show the calculation inputs and sensitivity. For example, it should make clear when the economic result changes if the PAV or anticipated supplement moves within a plausible range.

## Model opportunities

### Total-loss triage

A calibrated classifier can prioritise likely total-loss cases early, helping allocate work and avoid unnecessary estimate effort. It should use a deliberately limited label such as “high probability of economic total loss”, not issue the final conclusion.

### Outcome scenario calculator

Rules should calculate ratios and thresholds from approved inputs. Machine learning may estimate ranges or uncertainty, but the displayed arithmetic should be deterministic and reproducible.

### Visible safety-indicator detection

Vision models can flag possible:

- displaced lamps or sharp exposed edges;
- wheel/tyre damage;
- glazing obstruction;
- fluid leakage evidence;
- deployed restraints;
- visibly distorted structural areas;
- insecure panels.

The correct response is a prominent engineer-review prompt or evidence request. Lack of a flag must never be represented as proof of roadworthiness.

### Salvage range and anomaly model

Historical salvage outcomes can support a time-aware range model and identify an unusually high or low quotation. Live market or supplier evidence remains authoritative.

### Categorisation support

A constrained checklist can organise evidence relevant to salvage categorisation. The model can retrieve current approved guidance, identify potentially relevant observations and expose uncertainty. It should not autonomously assign a category from photographs.

## Remote-evidence limitations

Reports and interfaces should consistently distinguish:

- visible condition;
- condition reported by another party;
- engineer inference;
- unverified hidden condition;
- checks that require a suitable on-site repair or diagnostic process.

Images may miss chassis alignment, restraint faults, diagnostic codes, high-voltage isolation, steering damage or mounting-point deformation. The system should ask for better evidence where possible and abstain where it cannot reduce uncertainty.

## Evaluation

For economic triage:

- recall for final total-loss cases;
- false-total-loss rate;
- calibration and risk-coverage curves;
- performance by vehicle age, value and source;
- sensitivity to current valuation and rate inputs.

For roadworthiness prompts:

- recall of engineer-confirmed visible safety concerns;
- false reassurance rate;
- evidence-citation accuracy;
- appropriate abstention;
- missing-view request quality.

For salvage:

- range coverage;
- median absolute percentage error;
- drift by calendar period;
- outlier-flag precision;
- performance by vehicle segment and damage category.

The most important safety measure is not overall accuracy. It is how often the system fails to escalate a genuine visible concern or presents an absence of evidence as an affirmative clearance.

## Governance controls

- Require an engineer to accept the final outcome and wording.
- Store the rule, threshold, source and effective date used.
- Do not allow clients to tune the model toward more total losses or more repairs.
- Monitor outcomes by instructing party and evidence source for inappropriate bias.
- Use current valuation, salvage and repair inputs.
- Record later supplements and reversals as learning events.
- Present roadworthiness language as evidence-bounded, not as a physical-inspection certificate.
- Set conservative abstention thresholds for rare vehicles, EV/high-voltage concerns and poor evidence.

## Recommended pilot

Retrospectively replay a time-held-out set using only the evidence available at each original cutoff. Test three independent outputs:

1. likely economic outcome and sensitivity;
2. visible safety-indicator prompts;
3. salvage-range anomaly checks.

Engineers should review false negatives first. Deployment should begin as triage and QA only, with no automatic categorisation, roadworthiness statement or case disposition.

## Conclusion

The archive can materially improve early triage and consistency, particularly when repair, valuation and salvage facts are linked at case level. The defensible product is a transparent scenario and warning system. It does not replace an engineer's remote professional judgement, and it cannot create the assurance that a physical inspection would provide.
