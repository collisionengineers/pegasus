# Knowledge Currency, Drift and Change Control

## Executive conclusion

Collision assessment combines stable concepts with rapidly changing facts. Vehicle design, repair methods, calibration requirements, prices, labour rates, market values, templates and professional guidance all change. A system that was accurate when trained can become confidently wrong without any software failure.

Changing knowledge should live primarily in versioned retrieval sources and deterministic configuration. Models still require monitoring because vehicle mix, image sources, language and work patterns drift.

## Knowledge classes

### Stable or slowly changing

- basic component taxonomy;
- common damage categories;
- evidence-provenance rules;
- distinction between visible, reported and inferred;
- core case structure.

These may be learned in model weights, although taxonomy versioning remains necessary.

### Frequently changing

- part prices;
- labour rates;
- valuation evidence;
- salvage markets;
- manufacturer methods;
- ADAS and diagnostic requirements;
- client templates and contractual rules;
- legal and professional guidance;
- internal approved wording.

These should be retrieved or configured with effective dates.

## Knowledge record

Every approved reference should include:

```yaml
knowledge_item:
  source_id:
  title:
  publisher:
  authority_class:
  version:
  effective_from:
  effective_to:
  retrieved_at:
  rights_status:
  applicable_vehicle_or_context:
  supersedes:
  superseded_by:
  review_due:
  approved_by:
  content_hash:
```

The application should prefer the item effective for the assessment date and warn when only stale or undated material exists.

## Drift types

### Input drift

Changes in:

- vehicle age/fuel/technology mix;
- image resolution, compression and capture source;
- new client instruction formats;
- mailbox language and attachment types;
- report template;
- geographic or market distribution.

### Label and concept drift

Changes in:

- damage or operation taxonomy;
- what evidence is considered sufficient;
- repair versus replacement practice;
- valuation adjustment approach;
- definitions used in workflow status.

### Outcome drift

Changes in:

- repair cost;
- total-loss rate;
- PAV distribution;
- supplement rate;
- engineer correction patterns;
- query volume.

### Retrieval drift

Changes in source availability, indexing, permissions, document versions or relevance quality.

## Monitoring

Monitor by calendar period and relevant segment:

- input feature distributions;
- unknown/OCR failure rates;
- view and damage class frequencies;
- confidence and abstention;
- engineer edit/rejection;
- unsupported-finding and QA rates;
- valuation residuals;
- operation acceptance;
- retrieval no-result and stale-result rates;
- latency and cost;
- differences between engineers and sources.

Set thresholds from baseline variability, not arbitrary percentages. Some rare but high-risk failures require event-based alerts even when volume is low.

## Change-control process

1. Record the proposed change and reason.
2. Identify affected tasks, sources, clients and report types.
3. Update references, configuration, taxonomy or model in an isolated version.
4. Re-run the stable benchmark and relevant challenge sets.
5. Compare against the current production version.
6. Obtain domain, privacy/security and release approval as applicable.
7. Deploy in shadow or canary mode.
8. Monitor for a defined period.
9. promote, hold or roll back.
10. Retain the complete decision and artifact history.

An urgent source correction may be deployed through a controlled expedited path, but it should still be documented and retrospectively reviewed.

## When to retrieve, reconfigure or retrain

### Update retrieval

Use when a new repair method, guidance document or approved response supersedes an older item.

### Reconfigure rules

Use when a rate, threshold, required section or deterministic calculation changes.

### Recalibrate

Use when confidence no longer corresponds to observed accuracy but core ranking remains useful.

### Retrain

Use when performance declines due to new vehicles, image conditions, terminology or corrected labels.

### Redesign

Use when the task definition or professional workflow changes materially. Retraining an obsolete formulation is not enough.

## Preventing stale-model leakage

Even if current information is retrieved, a generative model may repeat facts memorised during fine-tuning. Controls should:

- instruct the model to use only supplied current values;
- validate generated amounts and dates against structured inputs;
- block unsupported citations;
- test adversarial stale examples;
- omit unstable facts from style-tuning targets where possible;
- use templates for controlled values.

## Benchmark maintenance

Keep:

- a stable regression set for comparability;
- a rolling recent-period holdout;
- rare-condition challenge sets;
- source and vehicle-family slices;
- security/privacy tests;
- cases containing known prior failures.

Do not repeatedly tune against the secret test set. Create a new sealed holdout periodically and record any exposure.

## Ownership

- Domain lead approves taxonomy and knowledge applicability.
- Data steward owns source lineage and rights status.
- ML owner monitors model drift.
- Product/operations owner monitors workflow outcomes.
- Engineer/quality lead determines whether a performance shift is professionally material.
- Release approver records deployment decisions.

## Conclusion

Currency is an operational capability, not a one-off data-cleaning exercise. Keeping volatile facts outside model weights, versioning every reference and monitoring real engineer corrections allows the system to improve without quietly drifting away from current practice.
