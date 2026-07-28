# OCR and Cloud Document Intelligence Fallback

Generated: 2026-05-22

**Type:** Document-processing tool
**Priority:** High

## Objective

Retain the local Tesseract fallback for the desktop mapper but add a controlled cloud/document-intelligence fallback for difficult tables, poor scans, and provider templates that defeat local extraction.

## Why it matters for Collision Engineers

The mapper already bundles/uses Tesseract for strict image-only PDF cases. Some PDFs and tables will still need better extraction. Azure Document Intelligence and Amazon Textract both support structured document extraction concepts such as forms/tables/key-values/custom models.

## Proposed shape

A tiered extraction service chooses local text extraction, local OCR, then cloud OCR/document intelligence only when needed and allowed.

## Candidate tools / MCP methods / skill actions

- `extract_text_local(file_id)`
- `ocr_local_if_allowed(file_id)`
- `run_cloud_document_intelligence(file_id, mode)`
- `extract_tables(file_id)`
- `compare_extraction_methods(file_id)`
- `choose_best_extraction(file_id)`

## Inputs

- PDF/image file
- provider/document type
- data-governance flag
- OCR policy
- cost budget

## Outputs

- Text blocks
- tables
- key-value pairs
- confidence
- method used
- cost estimate
- review flag

## Guardrails

- Do not send files to third-party OCR until governance approval.
- Keep local extraction first.
- Log method and cost.
- Avoid OCRing photo dumps.
- Preserve source file unchanged.

## MVP implementation path

1. Implement extraction method registry.
2. Add cloud mode behind feature flag.
3. Test on EVA tables, scanned letters, and photo PDFs.
4. Record method output for side-by-side evaluation.

## Test / acceptance criteria

- Local success path unchanged.
- Cloud fallback improves target templates.
- Photo dumps skipped.
- Cost per document captured.
- Privacy controls tested.

## Risks and open questions

- Third-party processing restrictions.
- Cost creep.
- Different OCR methods may produce different line order.
- Custom model training data required.

## Project source basis

- claudechat.md
- phase_ai_tools.md
- Azure Document Intelligence docs
- Amazon Textract docs

## External reference basis

- Azure Document Intelligence: https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/overview
- Amazon Textract: https://docs.aws.amazon.com/textract/latest/dg/how-it-works-analyzing.html
