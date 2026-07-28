# Review of `phase_ai_tools.md` and Expanded Tooling Direction

Generated: 2026-05-22

## Short review

The Phase 3 plan is directionally sound. It correctly identifies valuation, DVLA/DVSA enrichment, classification/extraction, image analysis, staff-facing natural-language skills, model hosting, data collection, and feedback loops as high-value areas. The main improvement is to split the plan into three implementation layers:

1. **Deterministic workflow tools**: ingestion, storage, validation, matching, EVA payload generation, duplicate prevention, logging.
2. **MCP/tool interfaces**: narrow tools that expose Graph, Box, EVA, DVLA/DVSA, valuation, job-sheet, and case-store operations to agents and internal apps.
3. **Skills/agents**: case summaries, chaser drafting, valuation explanation, engineer pack drafting, mapping assistance, query responses, and training modules.

This separation matters because Collision Engineers' work involves technical evidence, vehicle valuation, insurance/legal context, and potentially expert-witness-style reasoning. AI should prepare, extract, explain, and draft. It should not silently decide or issue final expert judgments.

## Strongest existing ideas in the plan

- The valuation agent should be converted from a proof of concept into a proper evidence-backed valuation workflow.
- The DVLA/DVSA MCP is a natural backbone for vehicle verification, mileage estimation, and discrepancy detection.
- Document/image classification is a good way to reduce pressure on provider-specific mapping rules.
- The staff skills are valuable because they compress repeated admin tasks into guided, approved outputs.
- Feedback loops and evaluation are essential; they should be designed from the start rather than added later.

## Gaps added in this markdown pack

This expanded pack adds plans for:

- Outlook/Graph intake MCP.
- Box storage and metadata MCP.
- EVA/Sentry API MCP.
- Canonical case data store.
- CE Document Mapper extraction service wrapper.
- Review queue and human approval tooling.
- Duplicate/conflict detection.
- Audatex estimate QA and ABP charge review assistants.
- CE communication style writer skill.
- Job sheet/SharePoint bridge.
- Observability, runbooks, audit, security, and MCP gateway controls.
- Provider mapping assistant.
- Tool registry and permissions strategy.
- Invoice/fee-note support.
- A.N.D.I.E-style damage review workbench extension.

## Suggested implementation priority

### Immediate / Phase 3A

1. EVA/Sentry API MCP and payload validator.
2. Canonical case data store.
3. DVLA/DVSA vehicle intelligence MCP.
4. Autotrader valuation agent industrialisation.
5. CE Document Mapper extraction service wrapper.
6. Missing-info checker and chaser drafter.
7. Case summary/status skill.
8. Review queue and approval tool.

### Next / Phase 3B

1. Outlook/Graph intake MCP.
2. Box storage/metadata MCP.
3. Document classifier/router.
4. Field extraction model and schema mapper.
5. Image evidence quality checker.
6. Engineer pack generator.
7. Valuation explanation/dispute response skill.
8. Observability dashboard.

### Later / Phase 3C

1. ABP charge-review assistant.
2. Audatex estimate QA assistant.
3. Knowledge-base/report-clause RAG skill.
4. WhatsApp/Teams/portal front door.
5. Job sheet bridge.
6. AI literacy and internal training skill.
7. Tool registry/security gateway.

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
