# Evaluation and Benchmark Design

## Executive conclusion

Evaluation must reproduce the information constraints and consequences of remote assessment. Random image splits and generic model metrics will overstate performance.

The benchmark should be case-level, time-aware, source-diverse and designed to measure unsupported confidence as aggressively as correct predictions.

## Benchmark layers

### Layer 1 — Artifact processing

Measure:

- successful file decoding;
- page and attachment recall;
- field extraction precision/recall;
- case-link accuracy;
- duplicate detection;
- redaction recall;
- version identification;
- monetary calculation consistency.

### Layer 2 — Image quality and view

Measure:

- macro F1 by view class;
- per-quality-flag precision/recall;
- sufficient-evidence classification;
- false “ready” rate;
- latency on target device;
- calibration.

The false-ready rate matters more than overall accuracy because accepting an inadequate image set harms every downstream task.

### Layer 3 — Damage/component vision

Measure:

- component detection mAP/mAR;
- damage detection precision/recall;
- per-class and per-severity performance;
- false positive rate on undamaged or unrelated regions;
- performance by image quality;
- performance by vehicle family and evidence source.

### Layer 4 — Multi-image assessment

Measure finding-level:

- precision;
- recall;
- evidence-citation correctness;
- unsupported-finding rate;
- missing-evidence recall;
- contradiction detection;
- confidence calibration;
- engineer acceptance and edit distance.

Every generated finding should be scored against both the final case conclusion and the evidence available at the cutoff.

### Layer 5 — Estimate and valuation support

Measure:

- operation precision/recall;
- omitted safety-related operation rate;
- estimate component error;
- range coverage;
- supplement-risk calibration;
- valuation residuals by time;
- outlier-detection precision;
- arithmetic error prevention.

A model should not be rewarded for matching a total while proposing the wrong repair plan.

### Layer 6 — Language and correspondence

Measure:

- factual consistency with structured inputs;
- unsupported assertion rate;
- source citation correctness;
- template/policy compliance;
- tone and clarity;
- PII leakage;
- inappropriate adoption of third-party language;
- engineer edit time;
- send-without-change rate, used cautiously.

## Split design

### Group by case

All images, messages, reports and versions from a claim stay in one split.

### Group related vehicles where possible

Repeat claims, plate changes or near-duplicate images can create hidden leakage.

### Temporal holdout

Reserve the most recent period for testing. This exposes:

- valuation drift;
- rate changes;
- new vehicle types;
- updated report templates;
- evolving guidance.

### Source holdout

Hold out one or more:

- instructing firms;
- repairers/bodyshops;
- evidence channels;
- report templates.

This tests whether the model learned engineering concepts or source formatting.

### Rare-condition challenge set

Create a manually curated set containing:

- previous total loss;
- unrelated damage;
- poor evidence;
- subtle damage;
- hybrid/EV procedures;
- uncommon body structures;
- contradictory instructions;
- later amendments;
- misleading client pressure;
- wrong-vehicle images.

## Evidence-time evaluation

For versioned cases:

1. build the input exactly as it existed at the original assessment cutoff;
2. score the original prediction;
3. introduce later evidence;
4. measure whether the system updates appropriately;
5. verify that it does not claim later findings were previously visible.

This is essential for supplement-risk and multi-image assessment.

## Calibration and abstention

Evaluate whether predicted confidence corresponds to correctness.

Define thresholds for:

- automatic extraction;
- show suggestion;
- require prominent warning;
- abstain and request more evidence.

Useful measures include:

- expected calibration error;
- Brier score;
- risk-coverage curve;
- error rate at the selected operating threshold.

The business target is not maximum coverage. It is high-value coverage with controlled risk.

## Human baseline

Establish current performance:

- time to assemble a case;
- time to identify missing evidence;
- report preparation time;
- query-response time;
- amendment rate;
- audit correction rate;
- inter-engineer agreement;
- common QA failures.

AI value cannot be proven without a comparable baseline.

## Engineer evaluation protocol

Use blinded review where practical:

- engineer sees case evidence;
- model suggestion is hidden for an independent first judgement on a subset;
- compare findings;
- then expose model output and collect edits/reasons;
- prevent reviewers from knowing which model/version produced the result.

Record correction categories rather than only accept/reject:

- unsupported;
- missed;
- wrong component;
- wrong relatedness;
- wrong action;
- stale method;
- calculation error;
- phrasing only.

## Production acceptance gates

Example gates should be agreed per task, but should include:

- no unresolved critical privacy/security finding;
- reproducible dataset and model;
- performance above baseline on untouched cases;
- subgroup performance within defined tolerances;
- unsupported-finding rate below threshold;
- acceptable calibration;
- meaningful engineer-time improvement;
- documented rollback;
- domain-owner and governance approval.

## Monitoring after release

Monitor:

- input drift;
- label and outcome drift;
- confidence distribution;
- abstention rate;
- engineer override rate;
- override reason;
- source-specific errors;
- amendment/supplement rate;
- latency and failure rate;
- privacy or security incidents.

A fall in engineer edits is not automatically positive; it may indicate automation bias. Periodic independent review is required.

## Benchmark release format

Each benchmark version should include:

- dataset manifest;
- case-level split IDs;
- label schema;
- evaluation code;
- metric definitions;
- subgroup definitions;
- known limitations;
- baseline results;
- human-performance reference;
- approval and expiry/review date.

## Conclusion

The evaluation programme should reward evidence-grounded assistance and safe abstention. A model that produces fewer answers but reliably identifies what is visible and what is missing may be more valuable than one that imitates complete reports while silently inventing certainty.

