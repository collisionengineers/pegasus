# Supplement and Hidden-Damage Risk

## Executive conclusion

Historical amendments and later repairer findings can train a model to estimate the risk that an initial remote assessment will change. The model should not claim to identify hidden damage. It should predict uncertainty and recommend additional evidence, reserve or review.

This may become one of the most valuable specialist models because it directly reflects the limitations of remote imagery.

## Target questions

- Is the supplied evidence likely to understate repair scope?
- Which visible impact patterns commonly lead to later additions?
- Which missing views are associated with supplements?
- Is the initial reserve unusually narrow?
- Should the engineer request repairer confirmation before finalising?
- Which operations have high uncertainty from exterior images?

## Labels

Possible outcomes:

- no later change;
- wording-only amendment;
- valuation change;
- labour increase/decrease;
- parts increase/decrease;
- repair-duration change;
- new component discovered;
- outcome changed from repairable to total loss or vice versa;
- roadworthiness/category change;
- reason unknown.

Also record:

- absolute and percentage cost change;
- time between versions;
- evidence added;
- source of later finding;
- whether the original report documented uncertainty.

## Training record

```yaml
supplement_example:
  case_id:
  original_evidence_cutoff:
  original_images:
  original_findings:
  original_estimate:
  evidence_quality:
  later_event:
  added_evidence:
  amended_findings:
  estimate_delta:
  amendment_reason:
```

Only information available at the original cutoff may be used as predictor input.

## Candidate features

### Image/evidence features

- number and diversity of views;
- affected-region coverage;
- close-up/context balance;
- glare/blur/occlusion;
- panel gaps or displaced components;
- visible crush depth proxy;
- wheel/suspension-adjacent impact;
- evidence source;
- no underbody or internal view;
- inconsistent or duplicated images.

### Vehicle features

- age and model;
- powertrain;
- ADAS equipment;
- material/body construction;
- prior total-loss history;
- mileage;
- part availability proxy.

### Assessment features

- impact region and magnitude;
- repair-versus-replace pattern;
- estimate-to-value ratio;
- number of precautionary checks;
- reserve margin;
- engineer uncertainty;
- use of image-based versus later repairer evidence.

## Model types

Start with:

- calibrated gradient-boosted classifier;
- survival/time-to-amendment model;
- quantile regression for cost delta;
- image-embedding features added to tabular inputs.

Only consider an end-to-end multimodal model after a transparent tabular baseline.

## Product output

```yaml
supplement_risk:
  risk_band: low | medium | high
  calibrated_probability:
  likely_change_families:
  evidence_gaps:
  comparable_case_ids:
  recommended_action:
  limitations:
```

Recommended actions may include:

- proceed;
- request a particular view;
- obtain repairer strip/inspection findings;
- widen reserve;
- senior review;
- defer a specific operation.

The final choice remains with the engineer.

## Avoiding false claims

Use language such as:

- “higher likelihood of later scope change”;
- “evidence does not exclude additional damage”;
- “consider requesting…”;
- “risk estimate based on comparable historical cases”.

Do not say:

- “hidden damage exists”;
- “this part is damaged” without evidence;
- “the final repair cost will be…”.

## Dataset challenges

- Amended reports may be under-recorded.
- Some supplements occur for commercial or parts reasons, not missed damage.
- Cases without amendments may simply lack later data.
- Different clients may have different reporting practices.
- Severe cases may be total losses and never dismantled.
- Historical estimates and repair methods drift.

Outcome collection must therefore distinguish “no known amendment” from confirmed no change.

## Evaluation

Measure:

- calibration;
- precision/recall at operational risk bands;
- cost-delta range coverage;
- performance by impact area and source;
- additional-evidence requests generated;
- supplements prevented or better anticipated;
- unnecessary follow-ups caused;
- engineer usefulness rating.

Use decision-curve analysis: a model is valuable only if its intervention cost is lower than the avoided uncertainty/error cost.

## Recommended pilot

1. Identify all versioned cases over several years.
2. Reconstruct evidence timelines.
3. Create a transparent tabular baseline.
4. Add image-quality and embedding features.
5. Validate on the newest year.
6. Deploy as a non-blocking risk badge in shadow mode.

## Conclusion

The data cannot make hidden damage visible, but it can make uncertainty measurable. A calibrated supplement-risk system would help engineers decide when remote evidence is sufficient and when further material is proportionate.

