# Source Reference Matrix

This matrix replaces the vague/invalid source references in the original phase plans with concrete project files available in the current source set.

## Primary plan files reviewed

| Source file | Use in enhanced plans | Notes |
|---|---|---|
| `phase_new_system.md` | Base scope for Phase 2 MVP: dashboard/holding pen, upload/intake, evidence matching, case detail, chasers, engineer pack, provider settings, data model, backend/frontend/storage/integrations. | Strong feature list but too high-level. Needs work-package breakdown. |
| `phase_bespoke_system.md` | Base scope for Phase 6 mature platform: communication layer, parser/image layer, case management core, workflow engine, roles, EVA/Box/valuation/DVLA services, web/mobile presentation. | Overlaps heavily with Phase 2. Reframed as expansion/hardening, not a separate rebuild. |

## Context-pack files - `collision_project_context_pack.zip`

| Source file | Use in enhanced plans |
|---|---|
| `00_INDEX.md` | Context map and reading order. |
| `01_PROJECT_BRIEF.md` | Business outcomes, users, non-goals, implementation bias. |
| `02_CURRENT_STATE_AND_MANUAL_PROCESS.md` | Current workflow: Outlook -> PDF/images -> spreadsheet -> Box -> EVA. Pain points and sample corpus requirements. |
| `03_TARGET_OPERATING_MODEL.md` | Target pipeline, work item statuses, human review role, control-table minimum columns. |
| `04_SYSTEMS_AND_INTEGRATION_CONTEXT.md` | Confirmed systems: Outlook/Microsoft Graph, Box, EVA, spreadsheet/control layer. |
| `05_OUTLOOK_INTAKE.md` | Microsoft Graph/polling intake strategy, email metadata, attachment handling, separate image-email correlation, duplicate detection. |
| `06_BOX_STORAGE_AND_METADATA.md` | Controlled Box storage principles, folder structure, metadata template, checksums, versioning, permissions. |
| `07_EVA_INTEGRATION.md` | EVA adapter boundary, direct API vs JSON import vs robotic fallback, idempotency, payload versioning, error categories. |
| `08_DATA_EXTRACTION_AND_AI_STRATEGY.md` | Direct text extraction, OCR, template parsing, AI extraction, confidence scoring, field status values, evidence map. |
| `09_DATA_MODEL_AND_JSON_CONTRACTS.md` | Canonical work item model, field-level extraction object, spreadsheet mapping, EVA payload boundary, database tables, event log object. |
| `10_WORKFLOW_STATES_AND_ORCHESTRATION.md` | State machine, queues, idempotency rules, retries, reconciliation, audit events, configuration management. |
| `11_EXCEPTION_HANDLING_AND_HUMAN_REVIEW.md` | Review queue categories, actions, SLAs, review UI options, feedback loop. |
| `12_SECURITY_PRIVACY_AND_GOVERNANCE.md` | UK GDPR/data handling, secrets, access control, audit trail, minimisation, retention, AI controls, incident runbooks. |
| `13_TESTING_QA_ACCEPTANCE_CRITERIA.md` | Unit/integration/E2E/regression tests, gold-standard outputs, acceptance gates, negative tests. |
| `14_IMPLEMENTATION_ROADMAP.md` | Incremental roadmap: discovery, email/Box, extraction/validation, review, EVA, hardening, expansion. |
| `15_AUTOMATION_CENTRE_OPERATING_MODEL.md` | Reusable platform capability model and repository/operating-cadence direction. |
| `16_MONITORING_OBSERVABILITY_AND_RUNBOOKS.md` | Dashboard sections, alerting, log structure, correlation IDs, runbooks. |
| `17_OPEN_QUESTIONS_AND_DECISION_LOG.md` | High-priority unknowns and decision-log structure. |
| `18_GLOSSARY.md` | Terminology. |
| `19_AGENT_HANDOFF_CONTEXT.md` | Compact context for future AI/developer handoff. |
| `20_SELF_REVIEW_AND_COVERAGE_CHECK.md` | Coverage check and known gaps. |

## Context-pack files - `collision-engineers-context-pack.zip`

| Source file | Use in enhanced plans |
|---|---|
| `01_project_brief.md` | Business definition and Phase-one positioning. |
| `02_company_business_context.md` | Company context and independence/technical evidence framing. |
| `03_domain_glossary.md` | Domain language and terms. |
| `04_current_workflow_and_pain_points.md` | Holding-pen and asynchronous-evidence pain points. |
| `05_target_workflow.md` | Target state, confidence handling, engineer-pack requirements, activity log requirements. |
| `06_mvp_demo_case_intake_engineer_pack.md` | MVP demo scope and engineer-pack skeleton. |
| `07_ui_dashboard_spec.md` | Dashboard, upload view, processing results, case detail, evidence matching, chaser view, engineer-pack view, settings view. |
| `08_technical_architecture.md` | Prototype/production architecture and processing pipeline. |
| `09_data_model_and_schemas.md` | Case/document/image/estimate/missing item/review flag/audit entities. |
| `10_ai_modules_and_prompts.md` | Classifier, extractor, estimate parser, matcher, missing-info checker, chaser drafter, pack generator modules. |
| `11_audatex_abp_industry_context.md` | Audatex estimate context and ABP charge-review assistance. |
| `12_compliance_governance_and_risk.md` | Human sign-off, data protection, audit trail, source attribution and AI risk. |
| `13_implementation_roadmap_and_roi.md` | Phased roadmap and ROI/KPI direction. |
| `14_discovery_questions_and_call_prep.md` | Discovery questions to resolve open implementation decisions. |
| `15_demo_dataset_and_test_plan.md` | Demo/test cases: complete case, missing images, VRM mismatch, duplicate, supplementary estimate, unrelated attachment. |
| `16_client_pitch_and_positioning.md` | Useful only for stakeholder framing, not core build. |
| `17_source_map_and_assumptions.md` | Assumption register and source map. |
| `18_work_check_coverage_audit.md` | Coverage/self-check. |

## Operational source files

| Source file | Use in enhanced plans | Key points carried forward |
|---|---|---|
| `handover.docx` | Current operational truth for job sheet, CE Document Mapper, AI subscriptions, Figma/whiteboards, EVA installation/use. | Job sheet requires Excel Desktop/VBA; folder creator relies on shared Z drive; CE Document Mapper stores `app_settings.json` and `providers.json` in `Documents\CE Document Mapper`; import supports PDF/DOC/DOCX/MSG, image extraction, batch mode and Audit Mode/Engineer Report. |
| `claudechat.md` | Development transcript for CE Document Mapper and current mapping/import/export behaviour. | Use to preserve Document Mapper assumptions, field order, provider presets, batch mode, engineer-report overwrite, PDF/DOC improvements, date/mileage rules, OneDrive path migration, UI constraints. Actual `app.py` is not present in the source set. |
| `Backup of CE Job Sheet 260429.xlsm` | Latest job-sheet reference. | Sheets: `Jobs`, `Own figures`, `Principals`, `Garages`. Jobs sheet has holding-pen sections with Date, VRM, Principal, Client, Vehicle, Missing, Due, Notes, folder buttons/paths, `T1_START` and `T2_START`. Principals/Garages sheets are the source for provider and garage configuration. |
| `Backup of CE Job Sheet 260309.xlsm` | Historical job-sheet comparison. | Use only to check prior columns/structure; prefer `260429` as current. |
| `Backup of Conditional Formatting 260202.txt` | Legacy rules for dashboard/status cues. | Date-age rules, current/past due dates, duplicate VRM rule ignoring spaces. |
| `Mapped Principals.xlsx` | Provider/principal onboarding list and exceptions. | Contains principal names/codes; includes lost causes such as OCR quality too low and inconsistent formats. |
| `Final Format Example 02.json` | Canonical CE Document Mapper export shape. | Work Provider, VRM, Vehicle Model, Claimant Name, Reference, Incident Date, Instruction Date, Inspection Date, Inspection Address, Accident Circumstances, VAT Status, Mileage, Mileage Unit. Keep order stable. |
| `EVA User Guide.pdf` | Manual EVA setup process. | Requires offline email copy, instruction, images; populate Reg No, Principal, Case ID/PO, Insured, Claim No, Incident Date, Inspected On, Inspect At, Speedo; upload photos in required first-two-then-all order; ensure Box folder contains email, instruction and images. |
| `Sentry_API_Complete_Guide.md` | Clean Sentry/EVA API integration guide. | JWT token endpoint; `/Instruction/Inspection`; `/Claim/LocationUpdate`; `/Claim/AuthorityStatusUpdate`; `/Note/SubmitNote`; `/Claim/Update`; `/Report/SubmitReport`; report retrieval; response model and errors. |
| `evaapidocs.pdf` | Full Sentry API documentation. | Use as primary vendor/reference documentation when API details in the guide need validation. |
| `CE Communication Style & Tone Profile.docx` | Chaser/email/report delivery style. | Calm, professional, technically authoritative, concise, no slang/emojis, evidence-based, clear requests. |
| `Standard Audatex Invoice.docx` | Standard invoice template. | Audatex assessment invoice layout, VAT, paid stamp, due on receipt. |
| `Website Invoice Template.docx` | Website invoice template. | Similar invoice layout with customer email/phone/VRM fields. |
| `Collision Engineers Whiteboard.jam` | Visual workflow/process reference. | `.jam` is a Figma/FigJam archive with workflow map images. Treat as a visual source for Outlook, WhatsApp, job sheet, Box and website/CMS flows; not fully machine-readable from `canvas.fig`. |

## Notable source gaps

1. The actual latest CE Document Mapper `app.py`, `README.md`, `requirements.txt`, and `providers.json` are not present as standalone files. Behaviour is inferred from `handover.docx` and `claudechat.md`.
2. EVA credentials, sandbox access, and exact production configuration are not present.
3. Box API credentials/root folder IDs and metadata template IDs are not present.
4. Microsoft Graph app registration details are not present.
5. The `Collision Engineers Whiteboard.jam` contains useful visuals, but the binary FigJam canvas is not reliably text-extractable. Use it during manual design review.

