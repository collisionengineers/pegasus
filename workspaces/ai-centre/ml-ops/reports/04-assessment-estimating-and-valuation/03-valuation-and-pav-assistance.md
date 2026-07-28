# Valuation and Pre-Accident Value Assistance

## Executive conclusion

Historical valuations are useful for learning how engineers select comparables, apply documented adjustments, identify outliers and explain a pre-accident value. They should not be used to teach a model a permanent mapping from vehicle photographs to price.

Vehicle values change with time, mileage, specification, condition, region and market conditions. The recommended system retrieves current licensed valuation evidence, normalises it, applies transparent adjustment logic and drafts a range with provenance for engineer approval.

## What the archive can contribute

At case level, the reports and supporting material may contain:

- registration, VIN and vehicle description;
- make, model, derivative, body style, engine and transmission;
- first-registration date;
- mileage;
- pre-incident condition information;
- guide values and dates;
- advertised comparables;
- optional equipment or specification;
- prior total-loss or provenance information;
- engineer adjustments;
- accepted PAV and range;
- queries, challenges and amended values.

The useful target is not only the final PAV. It is the chain of evidence and adjustment that produced it.

## Recommended valuation pipeline

### 1. Resolve vehicle identity

Combine OCR, instruction data and an authorised vehicle-data service. The system should surface conflicts rather than silently choosing a value.

### 2. Obtain current evidence

Retrieve licensed guide values, approved market comparables and relevant provenance data. Every result should retain:

- provider;
- query parameters;
- retrieval time;
- effective date;
- permitted use;
- raw response or stable reference.

The [DVLA Vehicle Enquiry API](https://developer-portal.driver-vehicle-licensing.api.gov.uk/apis/vehicle-enquiry-service/v1.2.0-vehicle-enquiry-service.html) can support vehicle-fact verification, but it is not a valuation source.

### 3. Normalise comparables

Map each result to a canonical schema and identify mismatches in derivative, age, mileage, body style, transmission, fuel, condition and seller context.

### 4. Apply transparent adjustments

Use rules or a constrained statistical model to propose adjustments. The engineer should see the reason and evidence for each change.

### 5. Produce a range and explanation

Show central estimate, plausible range, excluded outliers, uncertainty drivers and all source dates. The engineer selects or edits the reported value.

## Candidate model tasks

### Comparable relevance ranking

A learning-to-rank model can prioritise current comparables that most closely match the subject vehicle. Historical engineer selections provide useful relevance labels.

### Adjustment assistance

Quantile regression or another interpretable tabular model can estimate the effect of mileage, age and specification within a defined segment. It should provide uncertainty and should not overwrite an approved guide methodology.

### Outlier and inconsistency detection

Flag:

- a value inconsistent with the listed derivative;
- stale retrieval dates;
- duplicate advertisements;
- a mileage inconsistent across documents;
- unusually large undocumented adjustments;
- valuation totals that do not reconcile with component evidence.

### Explanation drafting

A language model can turn accepted structured evidence into clear valuation reasoning. It must cite the sources and avoid introducing unsupported condition claims.

### Query and dispute support

Past correspondence can help classify why a value was challenged and retrieve the applicable evidence or approved explanation. It should not learn that a challenge automatically justifies changing the value.

## What not to train

Avoid an image-to-PAV model. Accident images are a poor source of pre-accident condition and can introduce spurious associations between damage, client and reported value.

Avoid training on prices without effective dates. A random split lets a model memorise near-duplicate vehicles and creates misleading accuracy.

Avoid scraping or reproducing valuation and advertisement data without confirming contractual, database and copyright rights. Access to a portal or report does not necessarily grant model-training or redistribution rights.

## Time-aware dataset design

Each example should include:

```yaml
valuation_record:
  subject_vehicle:
  valuation_date:
  evidence_available_at:
  sources:
    - provider:
      retrieved_at:
      comparable_facts:
      displayed_price:
      rights_status:
  adjustments:
    - factor:
      amount_or_direction:
      rationale:
      engineer_status:
  final_pav:
  later_query_or_amendment:
```

Training, validation and test sets should be split chronologically. The test should simulate a genuinely later market period and use only information that would have been available on the valuation date.

## Evaluation

- vehicle-resolution accuracy;
- comparable relevance precision at `k`;
- source-date and derivative mismatch detection;
- median absolute error and percentage error;
- prediction-interval coverage;
- residuals by vehicle age, value band, fuel and body style;
- performance decay over time;
- unsupported-adjustment rate;
- citation accuracy;
- engineer edit size and explanation quality;
- rate of values changed after audit or query.

Evaluation should compare the complete evidence-backed range, not reward a model solely for landing near the final point estimate.

## Controls

- Current, authorised evidence takes precedence over model memory.
- Show source, retrieval time and vehicle match fields.
- Keep condition adjustments evidence-bounded.
- Require a reason for manually excluding a strong comparable or accepting a weak one.
- Set expiry periods for market evidence.
- Revalidate after a material delay or vehicle-identity change.
- Prevent downstream report generation if valuation inputs conflict.
- Log engineer overrides for review, not for automatic imitation.

## Recommended pilot

Begin with retrospective comparable ranking and anomaly detection on a time-held-out cohort. Do not ask the model to set PAV initially. Measure whether it:

- finds the engineer-used comparables;
- detects mismatched or stale evidence;
- creates a useful range from current approved sources;
- reduces research time;
- preserves or improves audit outcomes.

Only after this succeeds should adjustment suggestions and explanation drafting be introduced.

## Conclusion

The valuation archive has high value when treated as a record of reasoning under dated market conditions. Fine-tuning can improve selection, adjustment suggestions and drafting, while retrieval and deterministic controls provide current facts. The engineer should continue to own the final PAV and its evidential justification.
