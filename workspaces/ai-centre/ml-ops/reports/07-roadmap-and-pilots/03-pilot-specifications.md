# Pilot Specifications

## Executive conclusion

The initial programme should run bounded pilots with historical replay, shadow operation and explicit stop gates. Four pilots cover the strongest opportunities while sharing the same canonical case data and evaluation infrastructure.

Pilot sizes below are planning ranges, not statistical guarantees. Final cohort sizes should follow the observed class frequency, error tolerance and power analysis.

## Shared prerequisites

Before any pilot:

- approved purpose, rights and access;
- canonical case IDs and artifact hashes;
- source-role and version labels;
- case-level, time-aware train/validation/test split;
- representative clean and difficult cases;
- current-process baseline;
- named domain adjudicators;
- privacy/security review;
- success and stop criteria signed off;
- portable output artifacts and reproducible evaluation.

## Pilot A — Inbox-to-case assembly and automated QA

### Hypothesis

The system can reconstruct the case timeline, attach the correct messages/files and prevent common identity, version, arithmetic and citation defects with low professional risk.

### Scope

- one controlled mailbox period;
- associated case folders and reports;
- read-only ingestion;
- case/reference extraction;
- attachment hashing and deduplication;
- source-role/purpose classification;
- deterministic report/estimate checks.

### Suggested cohort

- discovery: 100–200 manually reviewed cases;
- evaluation: at least 300 held-out cases, with a deliberate set of cross-case, version and amendment edge cases;
- shadow use: several weeks across normal work volume.

### Success measures

- very high precision for automatic case links;
- zero known cross-case disclosures;
- high attachment and report-version recall;
- material QA issues found before issue;
- acceptable alerts per case;
- reduced case-assembly and checking time;
- complete audit trail.

### Stop conditions

- incorrect automatic case merges;
- inaccessible or untracked attachments;
- privacy scope broader than approved;
- alert burden prevents meaningful review;
- system overwrites source artifacts.

### Output

Canonical case graph, parser benchmark, QA rules, labelled message/case examples and a data-readiness decision for later pilots.

## Pilot B — Remote evidence quality and guided capture

### Hypothesis

A pretrained vision model can determine whether remotely supplied images cover the required views and provide one clear corrective instruction, reducing evidence-request cycles and false-ready cases.

### Scope

- image orientation/view class;
- blur, darkness, glare, obstruction and distance;
- duplicate/near-duplicate frames;
- vehicle/plate consistency prompts;
- evidence-set completeness;
- no damage severity, repair or roadworthiness conclusion.

### Suggested cohort

- 5,000–20,000 images from thousands of grouped cases if available;
- engineer-reviewed case-level completeness on a smaller adjudicated subset;
- test holdouts by case, time and evidence source.

### Success measures

- low false-ready rate;
- view-class macro F1;
- quality-flag precision/recall;
- fewer request cycles;
- faster time to evidence-complete;
- good performance on low-end devices/channels if capture is in scope;
- accepted, intelligible corrective prompts.

### Stop conditions

- the system frequently declares incomplete sets ready;
- guidance causes users to omit relevant context;
- source/device performance disparities are not controlled;
- wrong-vehicle checks create false assurance.

### Output

Portable view/quality models, label taxonomy, capture-readiness state machine, benchmark and human-factors findings.

## Pilot C — Governed RAG and controlled drafting

### Hypothesis

Permission-aware retrieval and constrained generation can reduce research and drafting time while preserving factuality, current knowledge and Collision Engineers' style.

### Scope

- approved domain-reference subset;
- selected stable report sections;
- evidence-request and query-response drafts;
- structured accepted case facts only;
- source citations and effective dates;
- no auto-send, auto-sign or autonomous opinion changes.

### Suggested cohort

- 300–1,000 approved report/message examples for evaluation and optional style tuning;
- 100–200 detailed blinded review cases;
- rare cases involving conflicts, stale sources and unsupported prompts.

### Baselines

- keyword search;
- generic model with no retrieval;
- template-only drafting;
- retrieval plus prompt, before any fine-tune.

### Success measures

- high citation correctness;
- very low unsupported-assertion rate;
- correct report and evidence version;
- lower engineer drafting time;
- no increase in material audit correction;
- preservation of remote-evidence limitations;
- low cross-case/PII leakage.

### Stop conditions

- invented facts, methods, values or attachments;
- third-party positions expressed as CE conclusions;
- stale references used without warning;
- recipients or messages crossed between cases;
- engineers approve without meaningful evidence review.

### Output

Governed knowledge index, retrieval benchmark, prompt/template package, optional style adapter, provenance validator and review-workflow evidence.

## Pilot D — Damage localisation and multi-image assessment research

### Hypothesis

Fine-tuned pretrained vision models can identify components and visible damage regions, and a case-level aggregator can create an evidence-grounded draft without unsupported professional conclusions.

### Scope

- selected common vehicle views/components;
- selected visible damage classes;
- boxes first, masks where justified;
- multi-image grouping;
- finding-to-image citations;
- missing-evidence and uncertainty output;
- no autonomous final estimate or signed assessment.

### Suggested cohort

- initial detector: approximately 5,000–15,000 carefully annotated images, depending on class balance;
- case-level aggregation: thousands of complete cases with structured approved findings;
- challenge sets for poor evidence, undamaged regions, unrelated damage, multiple impacts and rare/safety-relevant conditions.

These are discovery ranges. A smaller proof can test label learnability; production coverage will require broader data.

### Success measures

- component/damage precision and recall by class;
- low false-positive rate on undamaged areas;
- evidence-citation correctness;
- useful abstention;
- finding acceptance/edit/rejection;
- improved review time;
- stable performance by source, vehicle and quality;
- no hidden use of later evidence.

### Stop conditions

- unsupported findings are presented confidently;
- safety-relevant classes have unacceptable false negatives;
- the model learns report overlays, client/source artefacts or duplicate leakage;
- performance collapses on source/time holdouts;
- engineers cannot see why a finding was proposed.

### Output

COCO-style dataset, detector/segmenter artifacts, case-level benchmark, pipeline-versus-VLM comparison, limitations and go/no-go recommendation.

## Optional Pilot E — Estimate/valuation decision support

Run only after Pilots A and C establish reliable case facts and current-source retrieval.

### Scope

- estimate-line normalisation;
- operation omission/duplicate prompts;
- comparable ranking;
- source freshness and mismatch checks;
- deterministic calculations;
- no model-generated current price and no autonomous PAV/outcome.

### Success measures

- operation and comparable ranking quality;
- safety-relevant omission rate;
- calculation defects prevented;
- valuation range coverage;
- source/date accuracy;
- engineer time and audit outcome.

## Experiment design

Where practical:

- use historical replay before shadow operation;
- compare with simple baselines;
- blind reviewers to model identity;
- stratify by case type, vehicle, source and evidence quality;
- analyse accepted as well as rejected output;
- record later amendments;
- pre-register primary metrics;
- preserve a sealed final holdout.

## Pilot decision template

Each pilot closes with:

```yaml
pilot_decision:
  outcome: stop | revise | extend | controlled_production
  primary_metrics:
  risk_findings:
  segment_failures:
  human_factors:
  economic_result:
  data_rights_status:
  required_remediation:
  approved_scope:
  prohibited_scope:
  approvers:
```

## Conclusion

These pilots turn the broad opportunity into falsifiable tests. They prioritise low-risk infrastructure and evidence quality while creating a credible path to specialist vision and multimodal systems if the real corpus supports them.
