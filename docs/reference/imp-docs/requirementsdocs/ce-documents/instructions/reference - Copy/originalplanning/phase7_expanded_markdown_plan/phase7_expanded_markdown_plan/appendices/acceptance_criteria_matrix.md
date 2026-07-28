# Acceptance Criteria Matrix

This matrix summarises what should be true before each workstream is considered ready for pilot or live use.

| Workstream | Minimum pilot criteria | Live-readiness criteria |
|---|---|---|
| Work item state store | New cases receive unique IDs and states. | Old job sheet can be reproduced from the state store; failures are visible. |
| Provider configuration | Provider settings can be edited and tested. | Active provider config is versioned, auditable and rollback-capable. |
| Extraction service | Output matches CE Document Mapper on core samples. | Full regression corpus passes; errors route to review. |
| Review queue | Staff can correct and approve extracted fields. | Required fields cannot bypass review/validation. |
| EVA/Sentry adapter | Assisted payload generation works in test/sandbox. | Approved cases can submit safely with idempotency and audit logging. |
| Box/file storage | Source files stored with stable IDs/hashes. | Files are searchable, linked and retention-managed. |
| Customer portal | Authenticated uploads create or attach to work items. | Portal has access controls, audit logs and clear data boundaries. |
| Partner API | Test client can submit one case payload. | API has authentication, rate limits, object-level access checks and monitoring. |
| Analytics | Basic operational dashboards work. | Metrics are consistent, explainable and used in monthly review. |
| Risk indicators | Neutral review flags only; no final decisions. | DPIA and governance controls are complete; human review is mandatory. |
| Engineer pack | Pack generated from reviewed case data. | Pack clearly separates evidence, automation output and engineer judgement. |
| Communications | Draft chasers use verified fields. | Send/approval logs and provider-specific templates are in place. |
| Security/DPIA | Data map and draft DPIA exist. | DPIA, vendor review, secret handling and API security checks are complete. |
| Test harness | Core sample files have expected JSON. | Tests run automatically before code/config release. |
