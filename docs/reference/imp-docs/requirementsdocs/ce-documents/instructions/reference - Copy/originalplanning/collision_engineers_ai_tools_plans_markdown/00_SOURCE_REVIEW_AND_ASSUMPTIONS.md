# Source Review and Planning Assumptions

Generated: 2026-05-22

## Files reviewed

I reviewed the supplied Phase 3 plan and the wider project source context available in `/mnt/data`:

- `phase_ai_tools.md`
- `claudechat.md`
- `Final Format Example 02.json`
- `Backup of Conditional Formatting 260202.txt`
- `EVA User Guide.pdf`
- `Sentry_API_Complete_Guide.md`
- `evaapidocs.pdf`
- `Standard Audatex Invoice.docx`
- `Website Invoice Template.docx`
- `Mapped Principals.xlsx`
- `handover.docx`
- `CE Communication Style & Tone Profile.docx`
- `Backup of CE Job Sheet 260309.xlsm`
- `Backup of CE Job Sheet 260429.xlsm`
- `Collision Engineers Whiteboard.jam`
- `collision-engineers-context-pack.zip` and all extracted markdown files inside it
- `collision_project_context_pack.zip` and all extracted markdown files inside it

A standalone `app.py` source file was not present in the uploaded directory. The CE Document Mapper implementation details were therefore taken from the handover document and development transcript, which describe the importer, provider presets, mapping methods, OCR behaviour, batch mode, audit/engineer-report flow, JSON export, and OneDrive path handling.

## Context absorbed

Collision Engineers' practical workflow is not just “read a document and extract fields.” The broader problem is intake orchestration: emails arrive with PDFs, images may arrive separately, files need to be stored in Box, case data needs to be put into EVA, the spreadsheet still acts as an operational control layer, and humans must approve technical/legal outputs. The current CE Document Mapper is a useful bridge technology, but the future system should not hard-code everything directly to EVA or the job sheet. It should use a canonical case/work-item model, with adapters for EVA, Box, Graph, spreadsheets, chat, and AI skills.

## Planning assumptions

- Human review remains mandatory for technical conclusions, report issue, valuation uplift approval, and any uncertain evidence matching.
- MCPs should expose narrow, auditable business tools. They should not provide broad unrestricted access to mailboxes, Box, EVA, or files.
- Skills should be layered on top of deterministic tools and schemas, not used as free-form replacements for workflow state.
- The CE communication style should be treated as a reusable writing constraint for any outbound-drafting skill.
- The Sentry/EVA API guide now gives enough detail to plan a concrete EVA adapter/MCP.
- The job sheet and Box process can be retained as a transitional control layer, but they should not be the long-term single source of truth.
- Any production system should keep original source files unchanged and preserve extracted text, field evidence, review decisions, payloads, and API responses.

## Recommended architecture pattern

1. Inbound events from Outlook/Graph, manual upload, web forms, or WhatsApp.
2. Work item created in an internal case data store.
3. Original files stored in Box or equivalent storage with metadata.
4. Document/text/image extraction runs and writes field-level evidence to the canonical model.
5. Validation, missing-info checks, duplicate checks, and evidence matching decide whether the item is review-ready or blocked.
6. Staff review low-confidence or high-impact changes.
7. EVA payloads, engineer packs, chasers, valuation reports, and management summaries are generated from approved canonical data.
8. Every tool invocation and decision is logged.

## Source basis

Project files reviewed for this plan included the supplied `phase_ai_tools.md`, the CE Document Mapper handover/development transcript, the EVA user guide, the Sentry/EVA API guides, the CE communication style profile, the job sheet backups, the mapped-principals workbook, the invoice templates, the Collision Engineers whiteboard export, and both uploaded context packs.

Relevant external references checked:

- Model Context Protocol documentation: https://modelcontextprotocol.io/docs/getting-started/intro
- OpenAI Agents/tools documentation: https://developers.openai.com/api/docs/guides/tools and https://openai.github.io/openai-agents-python/tools/
- Microsoft Graph mail/attachments/change-notifications documentation: https://learn.microsoft.com/en-us/graph/api/resources/mail-api-overview, https://learn.microsoft.com/en-us/graph/api/attachment-get, https://learn.microsoft.com/en-us/graph/change-notifications-overview
- Microsoft Graph OneDrive/SharePoint file documentation: https://learn.microsoft.com/en-us/graph/api/resources/onedrive and https://learn.microsoft.com/en-us/graph/api/resources/driveitem
- Box API/metadata documentation: https://developer.box.com/reference and https://developer.box.com/reference/resources/metadata-template
- DVLA Vehicle Enquiry Service API documentation: https://developer-portal.driver-vehicle-licensing.api.gov.uk/apis/vehicle-enquiry-service/vehicle-enquiry-service-description.html
- DVSA MOT History API documentation: https://documentation.history.mot.api.gov.uk/
- Azure AI Document Intelligence documentation: https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/overview
- Amazon Textract documentation: https://docs.aws.amazon.com/textract/latest/dg/how-it-works-analyzing.html
- OpenAI vision/image-input documentation: https://developers.openai.com/api/docs/guides/images-vision
