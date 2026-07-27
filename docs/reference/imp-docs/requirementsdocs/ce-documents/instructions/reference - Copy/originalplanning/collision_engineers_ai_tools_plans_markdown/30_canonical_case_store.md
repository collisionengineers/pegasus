# Canonical Case Store and Schema Tool

Generated: 2026-05-22

**Type:** Database/API tool
**Priority:** Immediate

## Objective

Create the internal source of truth for work items, documents, extracted fields, reviews, EVA submissions, events, and links to Box/job-sheet/EVA.

## Why it matters for Collision Engineers

The context packs repeatedly recommend canonical JSON before EVA mapping. This prevents brittle direct coupling between parser outputs, spreadsheets, and EVA fields.

## Proposed shape

A small API/database stores versioned canonical work items and exposes tools to create, search, update, validate, and export them.

## Candidate tools / MCP methods / skill actions

- `create_work_item(source_event)`
- `get_work_item(identifier)`
- `update_field(work_item_id, field_path, value, source)`
- `validate_work_item(work_item_id)`
- `export_canonical_json(work_item_id)`
- `link_external_record(work_item_id, system, id_or_url)`
- `append_event(work_item_id, event)`

## Inputs

- Email/source event
- document IDs
- extracted fields
- review decisions
- external system links

## Outputs

- Canonical JSON
- field-level evidence
- workflow state
- audit event
- external links

## Guardrails

- Schema version required.
- Do not let EVA schema dictate internal schema.
- Every mutation creates an event.
- Preserve source evidence.
- Review status controls downstream actions.

## MVP implementation path

1. Define schema `0.1.0-alpha.1` using current mapper fields plus email/document/EVA/Box/review sections.
2. Implement SQLite/Postgres API.
3. Add schema validation.
4. Integrate mapper and review queue.

## Test / acceptance criteria

- Work item lifecycle from email to EVA submission.
- Schema validation catches invalid fields.
- Export matches expected JSON.
- Event log complete.

## Risks and open questions

- Over-modeling early.
- Migration from spreadsheet.
- Versioning complexity.

## Project source basis

- context_pack_2/09_DATA_MODEL_AND_JSON_CONTRACTS.md
- Final Format Example 02.json

## External reference basis

- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
