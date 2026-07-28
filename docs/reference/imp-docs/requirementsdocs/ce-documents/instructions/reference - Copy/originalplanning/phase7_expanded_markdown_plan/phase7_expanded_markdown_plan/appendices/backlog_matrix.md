# Backlog Matrix

## P0 — Foundations before wider automation

| Item | Reason |
|---|---|
| Work item state store | Central source of truth for all workflows. |
| Provider/principal configuration library | Reduces hardcoding and provider-specific failures. |
| Extraction regression corpus | Protects current mapper improvements. |
| Human review queue | Main safety gate before EVA/API/export. |
| Security/DPIA baseline | Required before high-risk AI, portal or partner API work. |

## P1 — First operational automation layer

| Item | Reason |
|---|---|
| CE Document Mapper extraction service | Reuses existing working logic centrally. |
| Box/file storage automation | Makes evidence traceable and searchable. |
| EVA/Sentry assisted import adapter | Removes manual re-keying while retaining review. |
| Duplicate/historical search | Prevents duplicate work and supports review. |
| Monitoring/runbooks/release process | Keeps automation supportable. |

## P2 — Workflow extensions

| Item | Reason |
|---|---|
| Customer self-service portal | Reduces email volume once intake model is stable. |
| Engineer pack generator | High staff/engineer value after reviewed data exists. |
| Image quality and coverage assistant | Reduces missing image rework. |
| Estimate/ABP review pack | Supports technical review but requires controls. |
| Communications/chaser drafting | Useful once missing-information state is reliable. |
| Knowledge base assistant | Useful after approved source material is structured. |

## P3 — Longer-term integrations and intelligence

| Item | Reason |
|---|---|
| Insurer/work-provider APIs | Depends on stable internal model and partner readiness. |
| Partner API | Needs mature security, tenancy and state model. |
| Predictive scheduling | Needs enough historical data and case state timestamps. |
| Advanced analytics | Needs consistent, reliable data first. |
| Data warehouse/archival | Follows operational data model and retention policy. |
| Risk indicators | High governance burden; should be review-only and phased carefully. |
| Invoice/payment automation | Useful but not central to intake/EVA automation. |
