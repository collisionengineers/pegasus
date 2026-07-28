# Box Storage and Metadata MCP

Generated: 2026-05-22

**Type:** MCP server + storage adapter
**Priority:** High

## Objective

Create consistent Box folders, upload source files, apply metadata, and return durable links/IDs for every work item and document.

## Why it matters for Collision Engineers

The current process requires Box storage for engineer access, and the EVA guide ends by reminding staff to upload email, instruction, and images to Box. Box metadata can become the searchable bridge between source files, work items, and EVA payloads.

## Proposed shape

A Box MCP exposes constrained file/folder operations. The workflow layer decides when to create folders and upload files; agents can search or fetch metadata but cannot arbitrarily delete or move originals.

## Candidate tools / MCP methods / skill actions

- `create_case_folder(work_item_id, folder_name)`
- `upload_original_file(work_item_id, file)`
- `apply_ce_metadata(file_id, metadata)`
- `list_case_files(work_item_id)`
- `get_box_link(file_id)`
- `find_files_by_vrm_or_reference(vrm?, claim_ref?)`
- `lock_or_mark_reviewed(folder_id)`

## Inputs

- Work item ID
- folder naming convention
- source file bytes/IDs
- VRM
- claim ref
- principal
- document type
- review status

## Outputs

- Box folder ID
- Box file IDs
- shared/internal links
- metadata records
- upload/audit events

## Guardrails

- Never overwrite originals.
- Do not expose delete operations to agents.
- Use metadata templates rather than filename-only tracking.
- Respect access controls by user/team role.
- Keep original files immutable where feasible.

## MVP implementation path

1. Define a CE work-item metadata template.
2. Implement folder creation and upload.
3. Populate metadata from canonical work item.
4. Add Box links to engineer packs and EVA notes/payloads if accepted.
5. Add a repairer/provider folder naming standard.

## Test / acceptance criteria

- Folder created in correct location.
- Files upload with checksums.
- Metadata search finds files by VRM/ref.
- Duplicate upload is detected by checksum.
- Permission failure routes to runbook.

## Risks and open questions

- Box licensing/features.
- Current folder structure may differ by principal.
- Metadata templates require admin setup.
- Shared links may be inappropriate for sensitive files.

## Project source basis

- EVA User Guide.pdf
- handover.docx
- context_pack_2/06_BOX_STORAGE_AND_METADATA.md
- Box API docs

## External reference basis

- Model Context Protocol documentation: https://modelcontextprotocol.io/docs/getting-started/intro
- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
- Box API reference: https://developer.box.com/reference
- Box metadata templates: https://developer.box.com/reference/resources/metadata-template
