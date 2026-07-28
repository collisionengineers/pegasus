# 7.2 Fraud Detection and Risk Scoring


## Purpose

Create a risk-indicator and review-prioritisation module that flags unusual patterns for human investigation. This should not be framed as autonomous fraud detection.

## Recommended reframing

Rename this workstream:

> **Risk Indicators and Evidence Anomaly Review**

The system can surface patterns such as duplicates, repeated claimants, conflicting identifiers, valuation outliers or reused images. It should not decide fraud, liability or validity.

## Step-by-step plan

### Step 1 — Define approved risk language

1. Create a list of permitted labels: possible duplicate, evidence mismatch, valuation outlier, repeated party, image anomaly, requires review.
2. Ban labels such as fraud confirmed, fake image, invalid claim, dishonest claimant.
3. Add wording rules to prompts, UI and reports.

### Step 2 — Build deterministic indicators first

Start with explainable checks:

1. Same VRM and incident date already exists.
2. Same claimant/contact appears across unrelated references.
3. Same image checksum appears in multiple cases.
4. Claim reference conflicts between instruction and estimate.
5. Vehicle model conflicts with VRM lookup/EVA/Experian output.
6. Incident date is after instruction date or implausible.
7. Repair estimate value appears high relative to known valuation fields.
8. Odometer/mileage conflicts across documents or MOT/Percayso-derived notes.

### Step 3 — Add field-level evidence

For every flag, store:

- field/value involved;
- source document;
- page/snippet or image file;
- rule version;
- confidence;
- recommended reviewer action.

### Step 4 — Create a review dashboard

1. Add a risk/review column to the work item table.
2. Show flags with reasons.
3. Let reviewers mark flags as confirmed, dismissed, duplicate resolved or escalated.
4. Store reviewer decisions as training/improvement data.

### Step 5 — Add historical pattern detection

Once enough work items exist:

1. Search across historical work items by claimant, VRM, contact, postcode, repairer and reference.
2. Add duplicate-image similarity only after checksum and metadata checks are reliable.
3. Add outlier rules for valuation, repair total, case duration and provider patterns.

### Step 6 — Governance and DPIA

1. Complete a DPIA or equivalent risk assessment before live risk profiling.
2. Confirm who can see risk flags.
3. Ensure flags do not automatically affect external communications or claim outcomes.
4. Review false positives monthly.

## Deliverables

- Risk-language policy.
- Deterministic ruleset `0.1.0-alpha.1`.
- Risk indicator schema.
- Review dashboard section.
- Reviewer outcome logging.
- DPIA/security note.

## Acceptance criteria

- Every flag includes a source and reason.
- No output states that fraud has been detected.
- Reviewers can dismiss or escalate flags.
- False-positive rates are measured.
- No automated adverse action occurs from a score alone.

## Risks and controls

| Risk | Control |
|---|---|
| False accusations | Use review-flag wording only; human decision required. |
| Biased profiling | Start deterministic and evidence-based; review DPIA. |
| Poor data quality | Only score fields with evidence and confidence. |
| Over-alerting | Track reviewer dismissal rate and tune thresholds. |

## Suggested priority

P2 after work item history, duplicate controls and governance are mature.
