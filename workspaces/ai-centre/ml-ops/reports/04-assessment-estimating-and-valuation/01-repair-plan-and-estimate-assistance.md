# Repair-Plan and Estimate Assistance

## Executive conclusion

The case archive can support an engineer-facing repair-plan and estimate assistant, but it should not be trained to turn photographs directly into a single price. That formulation hides the professional decisions, current rates and external repair information that produce the total.

The safer and more useful system separates:

1. what can be seen in the remotely supplied evidence;
2. which repair or replacement operations may follow;
3. which methods, parts and rates apply;
4. which operations remain uncertain pending strip or further evidence; and
5. the deterministic calculation of the estimate.

Historical estimates are valuable training supervision for proposed operations, omissions, amendment risk and plausible ranges. Current prices and methods must come from authorised live or versioned sources.

## What the data can teach

At case level, completed reports and estimate schedules can provide labels for:

- damaged component and side;
- repair, replace, remove/refit, paint and specialist operations;
- labour category and allowed hours;
- parts and materials;
- VAT treatment;
- geometry, diagnostic, calibration and safety-related allowances;
- betterment, wear or unrelated-damage exclusions;
- total-loss versus repairable disposition;
- caveats arising from remote evidence;
- changes between original, audited and amended estimates.

The version history is especially valuable. It can show which operations were frequently added, removed or corrected and which visible patterns tend to result in later supplements.

## Recommended system boundary

The model should produce a structured proposal such as:

```yaml
repair_plan_draft:
  - component: front bumper cover
    observed_condition: split and distorted
    evidence_ids: [IMG-014, IMG-015]
    proposed_operation: replace
    operation_basis: visible_damage
    confidence: 0.91
    method_reference_ids: [METHOD-123]
    price_status: current_lookup_required
    uncertainties:
      - reinforcement condition not visible
```

A controlled calculation service should then apply:

- approved labour rates;
- time units;
- parts prices and effective dates;
- paint and material formulas;
- VAT rules;
- rounding;
- approved commercial rules.

The model must never invent a current part price, labour rate or manufacturer method when a lookup fails.

## Candidate model tasks

### Operation recommendation

Use a multilabel classifier or constrained multimodal model to propose applicable operations from accepted damage observations and vehicle facts. Train it on engineer-approved operations, not merely every line appearing in an incoming repairer estimate.

### Labour-time and cost range

A regression or quantile model can produce a planning range from vehicle, component and operation features. This is useful for anomaly detection and triage, but it is not a substitute for the approved estimating source.

### Estimate-line mapping

Natural-language line items can be normalised to a component and operation taxonomy. This enables comparison between a repairer estimate and Collision Engineers' assessment even when the wording differs.

### Omission and duplication checks

Rules plus a learned model can flag:

- an operation normally paired with the accepted repair;
- paint work without a related panel operation;
- duplicate remove/refit allowances;
- inconsistent left/right components;
- missing diagnostics or calibration prompts;
- calculations that do not reconcile.

These are review prompts, not automatic additions.

### Amendment and supplement risk

Versioned cases can train a model to predict the probability and likely category of later additions. Its output should be a risk flag and evidence request, not a claim that hidden damage exists.

## Data preparation

Each training example should preserve:

- the evidence available at the assessment cutoff;
- the source and author of every estimate line;
- the engineer-approved final line;
- the report version;
- the applicable price/rate date;
- reasons for changes where recoverable;
- the final outcome without leaking later information into the input.

Incoming repairer estimates, client instructions and engineer schedules need distinct source-role labels. Otherwise a model may learn to reproduce a claimant or repairer's request as if it were the independent engineering conclusion.

## Training strategy

Start with retrieval, normalisation and deterministic comparison. Fine-tune only after a reliable component-operation taxonomy and review dataset exist.

A practical sequence is:

1. normalise estimate lines and calculations;
2. build rules for arithmetic and known operation dependencies;
3. train an operation recommender on structured accepted findings;
4. add current method and price retrieval;
5. train range and amendment-risk models;
6. test a constrained multimodal proposal model.

Training from random weights is unnecessary. Pretrained text and vision encoders will learn the domain with far less data.

## Evaluation

Measure more than total-price error:

- component-operation precision and recall;
- safety-relevant omission rate;
- unsupported-operation rate;
- duplicate-line detection;
- current-price lookup success;
- labour-time error by operation;
- interval coverage for estimate ranges;
- calculation reconciliation;
- supplement-risk calibration;
- engineer acceptance, edit and rejection rates;
- time saved without increased audit correction.

A predicted total can be numerically close for the wrong reasons. Evaluation must therefore compare the repair-plan structure and evidence support.

## Controls

- Show the source and effective date of every rate, method and price.
- Require engineer approval before an operation reaches a report.
- Keep model suggestions visually separate from accepted lines.
- Record every addition, edit and rejection with a reason.
- Warn when image evidence is inadequate for the proposed operation.
- Preserve the original evidence and calculation inputs.
- Re-run calculations after any accepted change.
- Do not optimise the model to reach a preferred repair/total-loss outcome.

## Recommended pilot

Run the first pilot on repairable cases with an accepted structured damage list. Compare the engineer's existing estimate against:

- normalised incoming lines;
- rule-based omissions and duplicates;
- model-proposed operations;
- current-source retrieval;
- a deterministic recalculation.

Keep all proposals non-binding. Success is demonstrated by fewer missed inconsistencies and reduced preparation time, while unsupported operations and safety-related omissions remain below agreed thresholds.

## Conclusion

The archive is useful for repair-plan learning, estimate normalisation and risk prediction. It is not a timeless price book. The robust design learns the engineering structure from historical cases, obtains changing facts from controlled sources and leaves the independent engineer in control of the accepted estimate.
