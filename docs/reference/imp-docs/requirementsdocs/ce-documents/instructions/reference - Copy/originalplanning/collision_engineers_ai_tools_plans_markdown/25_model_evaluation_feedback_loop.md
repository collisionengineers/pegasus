# Model Evaluation and Feedback Loop

Generated: 2026-05-22

**Type:** Evaluation service + dataset pipeline
**Priority:** Immediate

## Objective

Turn reviewer corrections and test corpus results into measurable extraction/classification/image/valuation performance metrics.

## Why it matters for Collision Engineers

The Phase 3 plan correctly calls for feedback loops. Without evaluation, automation cannot safely move from shadow mode to assisted mode or auto-submit for narrow cases.

## Proposed shape

A service stores gold-standard labels, compares outputs by version, and reports accuracy, recall, correction rate, false auto-approval risk, cost, and failure categories.

## Candidate tools / MCP methods / skill actions

- `record_gold_standard(sample_id, expected_json)`
- `evaluate_extraction_run(run_id)`
- `compare_model_versions(version_a, version_b)`
- `record_reviewer_correction(field_id, value)`
- `generate_quality_report(period)`

## Inputs

- Sample corpus
- expected outputs
- extraction results
- review corrections
- model/prompt/parser version

## Outputs

- Metrics report
- failure examples
- regression alerts
- dataset export

## Guardrails

- Anonymise or minimise training data where possible.
- Do not train on sensitive data without policy approval.
- Track model/prompt versions.
- Prefer field-level metrics.

## MVP implementation path

1. Create test corpus with expected canonical JSON.
2. Log versioned extraction runs.
3. Capture corrections from review UI.
4. Produce weekly quality report.

## Test / acceptance criteria

- Metrics align with manual spot check.
- Regression detected when parser worsens.
- Correction rate falls over time.
- False auto-approval rate stays near zero.

## Risks and open questions

- Insufficient labels.
- Privacy constraints.
- Different document types need separate metrics.

## Project source basis

- phase_ai_tools.md
- context_pack_2/13_TESTING_QA_ACCEPTANCE_CRITERIA.md

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
