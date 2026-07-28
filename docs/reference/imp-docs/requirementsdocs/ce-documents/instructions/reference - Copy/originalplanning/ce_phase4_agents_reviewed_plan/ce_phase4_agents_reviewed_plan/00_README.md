# Reviewed Phase 4 Plan — AI Agents and Workflow Assistance

This folder is a revised version of `phase_ai_agents.md`. It keeps the useful parts of the original proposal but changes the implementation framing: **build deterministic automations first, then add AI/agent capability only where uncertainty, drafting, synthesis, or tool orchestration justifies it**.

## Core correction

The original file names most functions as “agents”. That is too broad. Several proposed features are better implemented as ordinary backend jobs, workflow rules, scheduled checks, dashboards, or reviewed draft generators. A true agent should only be introduced where the system needs to inspect context, choose among allowed tools, produce a grounded draft/recommendation, and stop for approval when risk is non-trivial.

## Main outputs in this pack

1. `01_context_review_and_plan_corrections.md` — direct review of the original plan and corrected assumptions.
2. `02_agent_vs_automation_decision_framework.md` — rules for deciding whether a feature is an automation, AI assist, or controlled agent.
3. `03_intake_and_case_association.md` — revised inbox triage plan.
4. `04_missing_information_and_communication.md` — revised missing-information and chaser plan.
5. `05_valuation_vehicle_data_and_uplift.md` — revised valuation/evidence plan.
6. `06_engineer_support_report_drafting_and_eva.md` — revised engineer support and EVA/Sentry plan.
7. `07_image_review_evidence_quality_and_matching.md` — image and evidence matching plan.
8. `08_operations_management_intelligence.md` — continuous improvement as analytics rather than autonomous learning.
9. `09_technical_architecture_integrations.md` — implementation architecture and relevant services.
10. `10_security_governance_audit.md` — controls for insurance/legal-facing work.
11. `11_delivery_roadmap_and_acceptance_tests.md` — phased build order and acceptance criteria.
12. `12_open_questions_decision_log.md` — unresolved decisions before build.
13. `13_source_review_log.md` — files and context reviewed.

## Recommended headline

**Phase 4 should be a supervised automation-and-agent layer, not a bundle of autonomous agents.**

## Recommended first Phase 4 build

Start with:

- case database + file/evidence registry;
- Outlook intake automation;
- evidence matching with confidence thresholds;
- missing-information state machine;
- draft-only chaser/email generation;
- engineer pack generator;
- valuation evidence lookup with human approval;
- audit log for every file, extraction, AI output, human edit, approval and external action.

Defer:

- autonomous external emailing;
- WhatsApp Desktop automation;
- autonomous report submission to EVA;
- autonomous final valuation, roadworthiness, causation, fraud, or liability conclusions;
- open-ended “continuous learning” from live case data without governance.

## Source basis

This revision used the uploaded Phase 4 plan, the project context packs, CE Document Mapper development transcript, handover document, Sentry/EVA API material, EVA user guide, job-sheet backups, principal mapping sheet, communication tone profile, invoice templates, and web spot-checks for Microsoft Graph, Box, DVLA/DVSA, Azure Document Intelligence, Teams/WhatsApp, Audatex and valuation data services.
