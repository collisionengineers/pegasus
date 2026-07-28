# Document Classifier and Router

Generated: 2026-05-22

**Type:** AI/classifier tool
**Priority:** High

## Objective

Classify every incoming file into instruction, engineer report, Audatex/repair estimate, invoice, image, valuation evidence, email note, or unknown, then route to the right parser and workflow state.

## Why it matters for Collision Engineers

The current mapper relies on detected phrases and provider presets. A classifier reduces mapping burden, supports asynchronous evidence, and prevents photo dumps/invoices/estimates being treated as instructions.

## Proposed shape

Hybrid rules plus AI. Use MIME type, filename, text features, image count, known provider phrases, and LLM classification for ambiguous files.

## Candidate tools / MCP methods / skill actions

- `classify_file(file_id)`
- `route_file_to_pipeline(file_id, classification)`
- `explain_classification(file_id)`
- `add_training_label(file_id, label)`
- `list_unknown_templates()`

## Inputs

- Filename
- MIME type
- text extraction
- first page preview
- image count
- email metadata
- provider phrases

## Outputs

- Document type
- confidence
- reasons
- recommended parser
- review requirement

## Guardrails

- Unknown is acceptable.
- No auto-routing to EVA for low confidence.
- Classification reasons must be visible.
- Keep labelled corrections for evaluation.
- Do not infer facts absent from source.

## MVP implementation path

1. Start with deterministic rules for obvious documents.
2. Add LLM JSON classifier for ambiguous docs.
3. Add feedback labels.
4. Train lightweight model later if labelled corpus grows.

## Test / acceptance criteria

- Corpus classification accuracy.
- Confusion matrix by document type.
- Unknown rate monitored.
- Photo PDF not OCRed as instruction.

## Risks and open questions

- Needs real labelled documents.
- Provider templates change.
- Invoices and estimates may share vocabulary.
- Scanned docs may need OCR first.

## Project source basis

- phase_ai_tools.md
- context_pack_1/10_ai_modules_and_prompts.md
- handover.docx

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
