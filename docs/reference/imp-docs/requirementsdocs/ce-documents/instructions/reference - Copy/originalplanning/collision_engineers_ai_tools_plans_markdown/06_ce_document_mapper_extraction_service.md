# CE Document Mapper Extraction Service

Generated: 2026-05-22

**Type:** Internal tool/API wrapper
**Priority:** Immediate

## Objective

Expose the existing CE Document Mapper as an extraction service rather than only a desktop UI, so its provider presets, OCR rules, image extraction, and JSON output can feed the wider automation centre.

## Why it matters for Collision Engineers

The mapper already handles PDF/DOCX/DOC/MSG/EML ingestion, provider presets, mapping methods, OCR fallback, image extraction, batch mode, and engineer-report/audit overwrites. It is too valuable to discard, but its output should become a service-level tool.

## Proposed shape

Wrap the mapper logic behind a local API, CLI, or MCP-style tool. The desktop app remains available for staff, while the automation pipeline can call the same extraction engine in batch/shadow mode.

## Candidate tools / MCP methods / skill actions

- `extract_document_fields(file_id, provider_hint?)`
- `extract_images(file_id)`
- `detect_provider(file_id)`
- `run_engineer_report_merge(instruction_doc, engineer_doc)`
- `export_mapper_json(file_id)`
- `get_provider_mapping(provider_name)`
- `test_provider_mapping(provider_name, sample_file)`

## Inputs

- PDF/DOC/DOCX/MSG/EML file
- optional provider hint
- providers.json
- source text
- engineer-report pairing

## Outputs

- Final Format JSON fields
- source text
- image list
- provider detected
- field evidence
- confidence if added later
- override/highlight metadata

## Guardrails

- Do not break desktop UX.
- Preserve provider preset migration.
- Retain source text for audit.
- Do not overwrite instruction fields with blank engineer report fields.
- Separate extraction from approval.

## MVP implementation path

1. Extract engine into importable Python module if not already separated.
2. Add CLI/API entrypoint.
3. Add structured evidence per field.
4. Store outputs in canonical work item.
5. Run existing V42-V64 regression corpus.

## Test / acceptance criteria

- Every supported file type returns a stable JSON object.
- Known provider mappings produce same results as desktop.
- Engineer report merge highlights changed fields.
- OCR page-limit behaviour preserved.

## Risks and open questions

- Desktop code may be monolithic.
- Provider presets are local per user.
- Mappings lack confidence/evidence today.
- Multiple users may have divergent providers.json.

## Project source basis

- handover.docx
- claudechat.md
- Final Format Example 02.json
- phase_ai_tools.md

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
