# Automated QA, Consistency and Anomaly Detection

## Executive conclusion

Automated quality assurance is one of the highest-value and lowest-risk uses of the dataset. Many serious defects are not failures of expert judgement; they are mismatches across instructions, images, estimates, valuation evidence, report text and final document versions.

A layered QA system can detect deterministic inconsistencies immediately and use machine learning to surface unusual cases for review. It should describe anomalies neutrally and show the conflicting evidence. It should not silently “correct” an expert report or accuse a contributor of error.

## QA layers

### File and case integrity

Check:

- required artifacts are present and readable;
- attachments have been extracted;
- file hashes and duplicates are recorded;
- every artifact is assigned to the intended case;
- report versions form a valid sequence;
- no image or document appears to belong to another vehicle;
- the report evidence cutoff is recorded.

### Identity consistency

Compare registration, VIN, make/model, dates, mileage and reference numbers across:

- instruction;
- email;
- image OCR;
- lookup results;
- estimate;
- valuation;
- report;
- fee note.

The output should list all observed values and sources. A rule must not overwrite one with another.

### Evidence-to-finding consistency

For each finding:

- a supporting image or document exists;
- cited image IDs resolve;
- component and side agree;
- “visible”, “reported” and “inferred” wording matches source status;
- later evidence is not represented as available earlier;
- low-quality or missing views are acknowledged.

### Estimate and valuation checks

Use deterministic rules for:

- subtotal, VAT and grand-total reconciliation;
- duplicated lines;
- incompatible component/side labels;
- part, rate and source effective dates;
- repair/PAV/salvage scenario arithmetic;
- value and mileage consistency;
- comparison with approved thresholds;
- unusually large undocumented adjustments.

### Report-document checks

Confirm:

- mandatory sections and declarations;
- correct template and version;
- consistent outcome throughout;
- correct party and case references;
- limitations appropriate to remote assessment;
- unresolved warnings are visible;
- attachments and schedules match the narrative;
- final filename, version and document hash.

## Deterministic rules versus learned anomalies

Rules are best when the relationship is known:

- totals must add;
- cited artifacts must exist;
- a registration should not change silently;
- a report cannot cite an evidence item received after its cutoff.

Machine learning is useful when “unusual” depends on many factors:

- an atypical operation for a component/vehicle combination;
- a valuation far outside similar current cases;
- a report section unusually inconsistent with the structured findings;
- a case likely to be missing a normally supplied view;
- an amendment pattern unlike comparable cases.

The anomaly model should produce a ranked review suggestion with the contributing features, never an automatic verdict.

## Example finding schema

```yaml
qa_finding:
  finding_id:
  severity: information | warning | blocking
  type:
  description:
  evidence:
    - artifact_id:
      field:
      value:
  rule_or_model_version:
  confidence:
  proposed_action:
  reviewer_status:
  resolution_reason:
```

Blocking findings should be reserved for objective conditions such as failed calculations, missing required approval or unresolved cross-case identity conflict.

## Training data

Useful labels come from:

- audit corrections;
- original-to-amended report differences;
- rejected estimate lines;
- corrected identifiers;
- later evidence revealing an omission;
- quality-review notes;
- user dispositions of QA warnings.

Do not treat every amendment as proof the original work was defective. New evidence, changed instructions and updated market information need separate reason codes.

## Severity framework

Suggested categories:

- **Blocking:** risk of wrong case/vehicle, failed calculation, missing approval, corrupt final artifact.
- **High:** possible unsupported safety-related finding, incompatible outcome, material valuation/estimate conflict.
- **Medium:** stale source, missing evidence citation, unusual operation or unacknowledged limitation.
- **Low/information:** formatting, preferred wording or a non-material anomaly.

Severity should reflect consequence and confidence, not model novelty.

## Evaluation

- precision and recall by defect type;
- material-defect escape rate;
- alerts per case;
- reviewer acceptance, rejection and resolution time;
- false-positive burden;
- defects prevented before issue;
- audit correction and amendment rates;
- performance by report template, client and vehicle type;
- stability after rules or model changes.

Evaluate against a manually adjudicated set, including clean cases. If the test set contains only known errors, alert precision and operational burden will be overstated.

## Workflow and ownership

Each finding needs:

- an accountable reviewer role;
- a due point in the workflow;
- a documented resolution;
- escalation for unresolved high-risk issues;
- preserved evidence;
- a way to report a false or poorly worded rule.

Engineers should not be expected to review dozens of low-value alerts. Consolidate related findings and tune thresholds to consequence.

## Recommended pilot

Begin with deterministic checks on identity, evidence cutoff, versioning, arithmetic, citations and required sections. Replay historical original reports and test whether the checks find known audit/amendment issues without overwhelming reviewers.

Only then add learned anomaly ranking. Run it silently, have engineers label the most important alerts, and measure incremental defects found over the rules baseline.

## Conclusion

Automated QA can deliver value before any ambitious autonomous assessment model exists. It turns the dataset's cross-document relationships and version history into preventative controls, while preserving the engineer's authority to resolve judgement-based questions.
