# 8.3 CE Document Mapper to Central Extraction Service

## Purpose

Reuse the proven CE Document Mapper extraction/mapping logic as a central service for the wider automation platform, while keeping the desktop mapper available as a fallback and development/testing tool.

## Why this matters

CE Document Mapper already contains hard-won logic for PDF/DOC/DOCX/MSG/EML ingestion, provider presets, engineer-report merging, mileage/VAT/mileage-unit special handling and JSON export. The next system should not discard that work.

## Step-by-step plan

### Step 1 — Separate engine from UI

1. Identify pure extraction and mapping functions.
2. Move them into a reusable module or service.
3. Keep Tkinter UI code separate.
4. Preserve the existing desktop app behaviour for manual use.
5. Add automated tests around the extraction module before refactoring heavily.

### Step 2 — Formalise input and output contracts

1. Input: file path, file bytes, source metadata and provider config version.
2. Output: canonical field values, confidence indicators, source text, source spans, extraction warnings and extracted image metadata.
3. Include the source document hash in every extraction response.
4. Include the mapper version and provider config version.
5. Return structured errors rather than UI-only alerts.

### Step 3 — Preserve existing special rules

1. VRM normalisation remains mapping/export-stage behaviour.
2. Inspection address normalisation remains six-line output logic.
3. Mileage strips commas and captures digits until first non-numeric content.
4. Mileage Unit accepts only Miles, Km or blank.
5. VAT Status accepts only Yes, No or blank.
6. Engineer Report values overwrite only mapped non-blank fields, with UI/audit indication where values changed.

### Step 4 — Build service wrapper

1. Expose an internal API such as `extract_case(file_id, provider_id)`.
2. Allow batch extraction for multi-file instructions.
3. Return extracted files/images as linked file records, not loose local paths.
4. Route low-confidence or incomplete outputs to review.
5. Maintain a CLI/test runner for quick diagnostics.

### Step 5 — Connect to the work item state store

1. New source file arrives.
2. Work item is created.
3. Extraction job is queued.
4. Extracted fields and evidence are attached to the work item.
5. The work item state moves to extracted, validation_failed or needs_review.

### Step 6 — Maintain the desktop app as fallback

1. Continue releasing a portable app build for manual import/export.
2. Include requirements.txt in every development drop.
3. Allow provider configs to be exported/imported between desktop and central system.
4. Use the desktop app for rapid mapping rule tests before pushing into production.

## Deliverables

- Refactored extraction module.
- Service/API wrapper.
- Extraction result schema.
- Regression test corpus.
- Desktop app compatibility path.

## Acceptance criteria

- The central extraction output matches the desktop mapper output on the test corpus.
- Provider presets can be versioned and tested.
- Extraction failures create structured work item errors.
- Manual fallback remains available.

## Risks and controls

| Risk | Control |
|---|---|
| Refactor breaks working desktop behaviour | Add regression tests before changing architecture. |
| Service loses transparency | Return source text and field evidence with every value. |
| Provider settings diverge | Use one shared config format and version it. |

## Suggested priority

P0/P1. This is the bridge between the current tool and the future automation platform.
