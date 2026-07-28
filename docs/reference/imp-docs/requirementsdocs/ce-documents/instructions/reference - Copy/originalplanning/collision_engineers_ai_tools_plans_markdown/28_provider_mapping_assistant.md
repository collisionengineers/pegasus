# Provider Mapping Assistant for CE Document Mapper

Generated: 2026-05-22

**Type:** Staff skill + tool
**Priority:** Medium

## Objective

Help staff create, test, and maintain provider mapping presets for the CE Document Mapper using sample documents and extracted source text.

## Why it matters for Collision Engineers

Provider presets are powerful but require manual setup. The mapper has multiple methods and special cases. A mapping assistant can suggest labels, test extraction, and explain failures without changing the app’s clean UI.

## Proposed shape

A skill analyses source text and expected output, proposes mapping rules, runs the mapper test tool, and produces a providers.json-compatible change for approval.

## Candidate tools / MCP methods / skill actions

- `suggest_mapping_rules(sample_file, expected_json)`
- `test_mapping_rule(sample_file, field, method, config)`
- `compare_expected_vs_actual(expected_json, actual_json)`
- `generate_provider_preset(provider_name)`
- `explain_mapping_failure(field)`

## Inputs

- Sample document
- source text
- expected field values
- existing providers.json
- mapper methods

## Outputs

- Suggested provider preset
- field rule table
- test results
- diff against expected output

## Guardrails

- Do not auto-edit user providers without backup/approval.
- Keep mapping method vocabulary consistent.
- Prefer deterministic rules before LLM extraction.
- Preserve provider migration logic.

## MVP implementation path

1. Wrap mapper test functions.
2. Build prompt for rule suggestion.
3. Generate preset draft.
4. Add backup/restore providers.json.
5. Add test corpus by provider.

## Test / acceptance criteria

- Suggested rules pass on sample docs.
- Bad rule explains failure.
- Provider backup created.
- No schema-invalid providers.json.

## Risks and open questions

- Overfitting to one sample.
- Complex OCR layouts.
- Non-technical users may accept weak rules without tests.

## Project source basis

- handover.docx
- claudechat.md
- Final Format Example 02.json

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
