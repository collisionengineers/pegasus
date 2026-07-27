# Collision Engineers System Plans - Enhanced Index

## Review finding

The two original plan files are materially overlapping. They are not wrong, but they describe the same target platform at different levels of maturity:

- `phase_new_system.md` is best treated as the **near-term production MVP**: case intake, holding pen, upload/parser flow, evidence matching, review, Box storage, and EVA-ready payloads.
- `phase_bespoke_system.md` is best treated as the **mature end-to-end platform**: the same foundation plus workflow orchestration, role-based access, deeper EVA/Sentry operations, mobile/engineer workflow, monitoring, hardening, and legacy decommissioning.

The improved structure below avoids duplicated architecture. Phase 2 builds the operating core; Phase 6 expands and hardens that core rather than rebuilding it.

## Delivered files in this pack

| File | Purpose |
|---|---|
| `00_README_PLAN_INDEX.md` | This index, overlap analysis, and recommended reading order. |
| `01_SOURCE_REFERENCE_MATRIX.md` | All relevant source files reviewed and how each should be used. |
| `02_PHASE_2_CASE_INTAKE_MVP_ENHANCED.md` | Enhanced replacement for `phase_new_system.md`. Detailed MVP plan. |
| `03_PHASE_6_BESPOKE_PLATFORM_ENHANCED.md` | Enhanced replacement for `phase_bespoke_system.md`. Detailed long-term platform plan. |
| `04_WORK_PACKAGE_DISCOVERY_AND_BASELINE.md` | Step-by-step discovery, sample corpus, workflow baselining, and decision capture. |
| `05_WORK_PACKAGE_DATA_MODEL_AND_WORKFLOW.md` | Canonical model, workflow state machine, events, audit, and configuration. |
| `06_WORK_PACKAGE_DOCUMENT_MAPPER_AND_EXTRACTION.md` | Parser strategy, CE Document Mapper migration/bridge, OCR/AI extraction. |
| `07_WORK_PACKAGE_OUTLOOK_AND_COMMUNICATIONS.md` | Outlook/Graph intake, email metadata, chasers, communication tone and templates. |
| `08_WORK_PACKAGE_BOX_STORAGE_AND_FILES.md` | Box folder structure, metadata, checksums, retention, and file handling. |
| `09_WORK_PACKAGE_DASHBOARD_REVIEW_AND_MATCHING.md` | Holding pen, case detail view, evidence matching, review queue, exceptions. |
| `10_WORK_PACKAGE_EVA_SENTRY_INTEGRATION.md` | EVA/Sentry API adapter, payloads, tokens, error handling, duplicate prevention. |
| `11_WORK_PACKAGE_ENGINEER_PACK_AND_REPORTING.md` | Engineer pack generation, image ordering, report upload, invoice/report artefacts. |
| `12_WORK_PACKAGE_PROVIDER_SETTINGS_AND_MIGRATION.md` | Provider/principal/garage configuration, job-sheet migration, admin UI. |
| `13_WORK_PACKAGE_SECURITY_TESTING_ROLLOUT.md` | Security, privacy, QA, monitoring, rollout, decommissioning, acceptance gates. |

## Recommended reading order

1. `01_SOURCE_REFERENCE_MATRIX.md`
2. `02_PHASE_2_CASE_INTAKE_MVP_ENHANCED.md`
3. `03_PHASE_6_BESPOKE_PLATFORM_ENHANCED.md`
4. Work packages `04` through `13` as implementation briefs.

## Programme-level sequencing

```text
0. Discovery and baseline
1. Canonical model + workflow state machine
2. Document Mapper bridge + extraction baseline
3. Outlook intake + Box storage
4. Holding pen/review UI + evidence matching
5. EVA/Sentry adapter + controlled submission
6. Engineer pack/reporting + provider settings
7. Security, monitoring, rollout, and decommissioning
8. Phase 6 expansion: mobile/PWA, auto-approval, analytics, further integrations
```

## Implementation principle

Build in thin, testable slices. Do not attempt the final bespoke platform as a single greenfield replacement. The immediate objective should be to reduce manual intake/storage/re-keying while retaining human review and preserving the existing CE Document Mapper and job sheet until the replacement has proven operational reliability.

