# Collision Engineers — Expanded AI Tools, MCPs, and Skills Plan

Generated: 2026-05-22

This pack reviews the supplied Phase 3 AI tools plan and breaks the expanded brainstorm into separate Markdown plan files. Each plan is intended to be independently readable and implementation-ticket friendly.

## How to read this pack

- Start with `00_SOURCE_REVIEW_AND_ASSUMPTIONS.md` for the source audit.
- Read `00_PHASE_3_REVIEW_AND_ARCHITECTURE.md` for the overall critique and recommended implementation order.
- Then use the numbered plan files as separate candidate tools/MCPs/skills.

## Recommended first build set

1. `01_eva_sentry_api_mcp.md`
2. `30_canonical_case_store.md`
3. `04_dvla_dvsa_vehicle_intelligence_mcp.md`
4. `05_autotrader_valuation_agent_skill.md`
5. `06_ce_document_mapper_extraction_service.md`
6. `15_missing_info_checker_and_chaser_drafter.md`
7. `16_case_summary_status_skill.md`
8. `21_review_queue_and_human_approval_tool.md`

## Full file list

- `00_SOURCE_REVIEW_AND_ASSUMPTIONS.md`
- `00_PHASE_3_REVIEW_AND_ARCHITECTURE.md`
- `01_eva_sentry_api_mcp.md` — EVA / Sentry API MCP (MCP server + deterministic adapter, Immediate)
- `02_outlook_graph_intake_mcp.md` — Outlook / Microsoft Graph Intake MCP (MCP server + event intake service, High)
- `03_box_storage_metadata_mcp.md` — Box Storage and Metadata MCP (MCP server + storage adapter, High)
- `04_dvla_dvsa_vehicle_intelligence_mcp.md` — DVLA/DVSA Vehicle Intelligence MCP (MCP server, Immediate)
- `05_autotrader_valuation_agent_skill.md` — Autotrader Valuation Agent Skill (Skill + tool adapter, Immediate)
- `06_ce_document_mapper_extraction_service.md` — CE Document Mapper Extraction Service (Internal tool/API wrapper, Immediate)
- `07_document_classifier_and_router.md` — Document Classifier and Router (AI/classifier tool, High)
- `08_field_extraction_model_and_schema_mapper.md` — Field Extraction Model and Schema Mapper (AI extraction + schema validation tool, High)
- `09_ocr_and_cloud_document_intelligence_fallback.md` — OCR and Cloud Document Intelligence Fallback (Document-processing tool, High)
- `10_image_evidence_quality_and_schedule_checker.md` — Image Evidence Quality and Schedule Checker (Vision tool + skill, High)
- `11_visible_vrm_and_image_case_matcher.md` — Visible VRM and Image Case Matcher (Vision + matching tool, Medium)
- `12_audatex_estimate_parser_and_qa_assistant.md` — Audatex Estimate Parser and QA Assistant (Parser + review assistant, Medium)
- `13_abp_charge_review_assistant.md` — ABP Retail / Non-Contract Charge Review Assistant (Knowledge skill + parser, Medium-Low)
- `14_evidence_matcher_and_duplicate_detector.md` — Evidence Matcher and Duplicate Detector (Workflow tool + model, High)
- `15_missing_info_checker_and_chaser_drafter.md` — Missing-Information Checker and Chaser Drafter (Skill + deterministic checklist, Immediate)
- `16_case_summary_status_skill.md` — Case Summary and Status Skill (Natural-language skill, Immediate)
- `17_engineer_pack_generator.md` — Engineer Pack Generator (Skill + document generator, High)
- `18_valuation_explanation_and_dispute_response_skill.md` — Valuation Explanation and Dispute Response Skill (Writing skill + evidence retriever, High)
- `19_ce_style_communications_skill.md` — CE Communication Style Skill (Reusable writing skill, Immediate)
- `20_job_sheet_spreadsheet_bridge_mcp.md` — Job Sheet / SharePoint Spreadsheet Bridge MCP (MCP server + transitional adapter, Medium)
- `21_review_queue_and_human_approval_tool.md` — Review Queue and Human Approval Tool (Workflow UI/tool, Immediate)
- `22_knowledge_base_report_clause_rag_skill.md` — Knowledge Base / Report Clause RAG Skill (File-search/RAG skill, Medium)
- `23_whatsapp_teams_portal_front_door.md` — WhatsApp / Teams / Portal Front Door (Channel interface + skills gateway, Medium)
- `24_ai_literacy_training_and_policy_skill.md` — AI Literacy and Internal Training Skill (Training skill/content module, Medium-Low)
- `25_model_evaluation_feedback_loop.md` — Model Evaluation and Feedback Loop (Evaluation service + dataset pipeline, Immediate)
- `26_observability_runbook_dashboard_mcp.md` — Observability, Runbook, and Admin Dashboard MCP (Monitoring tool + MCP, Medium)
- `27_pii_redaction_audit_governance_tool.md` — PII Redaction, Audit, and Data Governance Tool (Governance tool, Immediate)
- `28_provider_mapping_assistant.md` — Provider Mapping Assistant for CE Document Mapper (Staff skill + tool, Medium)
- `29_tool_registry_and_mcp_security_gateway.md` — Tool Registry and MCP Security Gateway (Platform component, Immediate)
- `30_canonical_case_store.md` — Canonical Case Store and Schema Tool (Database/API tool, Immediate)
- `31_invoice_fee_note_generation_tool.md` — Invoice / Fee Note Generation Tool (Document/template tool, Medium-Low)
- `32_andie_damage_review_workbench.md` — A.N.D.I.E Damage Review Workbench Extension (Vision/LLM workbench, Later)

## Core architectural recommendation

Use deterministic workflow controls and a canonical case model first. MCPs/tools should expose narrow, permissioned actions. Skills should draft, summarise, explain, and assist staff on top of approved data. Human approval remains the boundary for technical conclusions, valuation uplifts, outbound messages, and EVA submission during rollout.
