# CE Document Mapper Rebuild Planning Pack

Prepared: 2026-05-22

## Purpose

This pack reviews the current improvement plan and restructures it around a ground-up rebuild of `cedocumentmapper`. The key change is sequencing: the first delivery stream is a stronger base mapper with no mailbox automation and no EVA API integration. Automation and EVA are planned separately as later enhancements that consume a stable mapper core.

## Pack structure

| Zip/file | Contents |
|---|---|
| `01_base_rebuild_pack.zip` | Base functionality rebuild plan, architecture, data model, QA/migration and risks. |
| `02_automation_enhancement_pack.zip` | Outlook/Box/spreadsheet/dashboard automation plan, deliberately separated from the base mapper. |
| `03_eva_api_enhancement_pack.zip` | EVA/Sentry API integration plan, deliberately separated from both base mapper and automation. |
| `cedocumentmapper_rebuild_markdown_pack.zip` | All markdown files in one archive. |
| `cedocumentmapper_rebuild_plan_pack_all_zips.zip` | Zip-of-zips containing the three phase packs plus the overall markdown pack. |

## Executive decision

Use the existing app and the connected test projects as source material, not as final architecture. The existing tool has proven the right concepts: provider presets, deterministic mappings, batch mode, engineer-report overlay, PDF/DOC/DOCX/EML/MSG ingestion, image export, OCR fallback, and flat JSON export. The rewrite should preserve those behaviours but implement them through a modular, testable core.

Automation and EVA should be adapter layers, not core mapper features. The mapper should produce reviewed, evidence-backed, canonical case data. Automation can later feed files into it, and EVA can later consume approved payloads from it.

## Recommended reading order

1. `01_PLAN_REVIEW.md`
2. `03_CURRENT_TOOL_AUDIT.md`
3. `04_GROUND_UP_REBUILD_BASE_FUNCTIONALITY.md`
4. `05_BASE_ARCHITECTURE_SPEC.md`
5. `06_DATA_MODEL_PROVIDER_RULES_AND_JSON.md`
6. `07_TESTING_QA_AND_MIGRATION.md`
7. `08_PHASE_2_AUTOMATION_ENHANCEMENT_PLAN.md`
8. `09_PHASE_3_EVA_API_ENHANCEMENT_PLAN.md`
9. `10_DELIVERY_ROADMAP_AND_BACKLOG.md`
10. `11_RISKS_DECISIONS_OPEN_QUESTIONS.md`
11. `02_SOURCE_REGISTER.md`
