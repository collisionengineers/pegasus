# Field Extraction Model and Schema Mapper

Generated: 2026-05-22

**Type:** AI extraction + schema validation tool
**Priority:** High

## Objective

Extract canonical fields from unstructured documents when provider-specific deterministic mappings are weak or absent.

## Why it matters for Collision Engineers

The existing final JSON fields are useful but narrow. The future workflow needs canonical work-item fields, field-level confidence, evidence, validation, and downstream EVA mapping.

## Proposed shape

An extraction tool combines deterministic mapper output, document-intelligence OCR/tables, and LLM extraction into a canonical schema with field-level evidence.

## Candidate tools / MCP methods / skill actions

- `extract_canonical_fields(document_id)`
- `merge_mapper_and_ai_fields(document_id)`
- `validate_extracted_fields(work_item_id)`
- `generate_field_evidence(field_id)`
- `apply_reviewer_correction(field_id, corrected_value)`

## Inputs

- Document text
- OCR output
- document type
- provider
- mapper JSON
- canonical schema

## Outputs

- Field objects with value/confidence/source/normalised value/validation status
- missing fields
- contradictions
- review flags

## Guardrails

- Return null for missing values.
- Every extracted value needs source evidence.
- Low confidence requires review.
- Contradictions are not auto-resolved.
- Canonical schema version required.

## MVP implementation path

1. Define canonical schema `0.1.0-alpha.1`.
2. Map current 13 fields to canonical fields.
3. Add extraction prompts and JSON schema validation.
4. Add review corrections to dataset.
5. Use mapper output as a high-confidence signal where available.

## Test / acceptance criteria

- Field-level accuracy/recall by document type.
- Schema validation pass rate.
- Correction rate.
- Regression tests for known provider documents.

## Risks and open questions

- Hallucination if prompts are not strict.
- OCR errors.
- Schema drift.
- Insufficient labelled ground truth initially.

## Project source basis

- Final Format Example 02.json
- context_pack_2/09_DATA_MODEL_AND_JSON_CONTRACTS.md
- phase_ai_tools.md
- Azure Document Intelligence docs

## External reference basis

- Azure Document Intelligence: https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/overview
- Amazon Textract: https://docs.aws.amazon.com/textract/latest/dg/how-it-works-analyzing.html
