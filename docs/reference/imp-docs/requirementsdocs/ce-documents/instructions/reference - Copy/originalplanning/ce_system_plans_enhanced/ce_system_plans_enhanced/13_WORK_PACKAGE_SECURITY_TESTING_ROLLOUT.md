# Work Package 13 - Security, Testing, Rollout, and Decommissioning

## Purpose

Ensure the platform is safe, reliable, testable, and adoptable before replacing the existing spreadsheet/Document Mapper workflow.

## Source files

- `collision_project_context_pack.zip` / `12_SECURITY_PRIVACY_AND_GOVERNANCE.md`, `13_TESTING_QA_ACCEPTANCE_CRITERIA.md`, `16_MONITORING_OBSERVABILITY_AND_RUNBOOKS.md`
- `collision-engineers-context-pack.zip` / `12_compliance_governance_and_risk.md`, `13_implementation_roadmap_and_roi.md`, `15_demo_dataset_and_test_plan.md`
- `CE Communication Style & Tone Profile.docx`
- `handover.docx`
- `Backup of CE Job Sheet 260429.xlsm`

## Security and privacy plan

### Data classification

Treat case data as sensitive personal and commercial information. It can include names, addresses, phone numbers, emails, vehicle details, claim references, accident details, photos, and reports.

### Security controls

1. Least-privilege access by role.
2. Strong authentication; SSO preferred in Phase 6.
3. Separate development/test/production data.
4. No production personal data in development unless approved and controlled.
5. Secrets stored in a secrets manager or encrypted environment store.
6. No secrets in logs, source code, or exported plan files.
7. Audit events for all user edits, external submissions, and file operations.
8. Retention/archive policy.
9. Incident response process.
10. AI usage controls: minimise personal data, log prompt versions, require evidence and review.

## Testing plan

### Test categories

1. Unit tests for parsers, validators, mappers, state transitions.
2. Integration tests for Outlook/Graph, Box, EVA/Sentry adapters.
3. End-to-end tests from email/upload to EVA-ready payload.
4. Regression tests using the sample corpus.
5. Negative tests for duplicates, mismatches, missing files, bad OCR, invalid dates, unknown providers.
6. Security tests for permissions and secrets handling.
7. User acceptance tests with real staff.

### Sample acceptance cases

At minimum:

1. Complete instruction with images.
2. Instruction missing images.
3. Image email arrives later and matches by VRM/reference.
4. Estimate arrives separately.
5. VRM mismatch between instruction and estimate.
6. Duplicate instruction email.
7. Engineer report overwrites fields.
8. Provider unknown.
9. OCR poor quality.
10. EVA submission failure and retry.
11. Box upload failure and retry.
12. Chaser sent and response linked.

### Quality gates

Before internal pilot:

- Parser gold-output tests pass for agreed corpus.
- No critical field silently missing.
- No unknown provider can be exported/submitted without review.
- Box storage operations are auditable.
- EVA payload preview works.
- User can manually override and audit correction.

Before production rollout:

- Parallel run completed.
- Staff training completed.
- Rollback path documented.
- Monitoring dashboard live.
- Error runbooks approved.
- Security review complete.

## Monitoring and runbooks

### Operational metrics

- Cases received per day.
- Cases awaiting information.
- Cases awaiting review.
- Cases ready for EVA.
- Average intake-to-ready time.
- Chaser count and response time.
- Parser success rate by provider.
- EVA success/failure rate.
- Box upload success/failure rate.
- Duplicate cases flagged.

### Alerts

- No emails ingested for abnormal period.
- Box upload failures above threshold.
- EVA failures above threshold.
- Parser failures for a provider spike.
- Cases stuck in `failed_retryable` or `needs_review` beyond SLA.
- Token/auth failures.

### Runbooks

Required runbooks:

1. Outlook ingestion stopped.
2. Box upload failure.
3. EVA API unavailable.
4. Parser producing bad values for a provider.
5. Duplicate cases created.
6. User cannot access system.
7. AI service unavailable or producing unsafe output.
8. Data incident or mistaken disclosure.

## Rollout plan

### Stage 1 - Internal prototype

- Use sample corpus.
- No live submissions.
- Validate extraction/review UI.
- Compare against CE Document Mapper output.

### Stage 2 - Shadow pilot

- Process real cases in parallel with current job sheet.
- Staff still use legacy process as source of truth.
- Compare field values, missing flags, Box files, EVA payloads.
- Log every discrepancy.

### Stage 3 - Controlled live pilot

- Select small provider subset.
- Use platform as primary intake/review for those providers.
- Keep job sheet updated or synced during transition.
- Daily review meeting.

### Stage 4 - Wider rollout

- Add providers in batches.
- Monitor extraction accuracy by provider.
- Expand chaser and matching functions.
- Add EVA submission only after payload quality is proven.

### Stage 5 - Legacy decommissioning

- Freeze legacy spreadsheet to read-only only after parity checklist passes.
- Retain archive copy.
- Retain CE Document Mapper as fallback for defined period.
- Remove fallback only after support period and incident-free operation.

## Training plan

Training modules:

1. Case dashboard and statuses.
2. Upload/intake.
3. Review and edit extracted fields.
4. Missing information and chasers.
5. Matching late evidence.
6. EVA payload preview/submission.
7. Engineer pack generation.
8. Provider/admin settings.
9. How to report a problem.

## Acceptance criteria

1. Users can complete core workflow with less manual re-keying than current process.
2. Critical errors are visible, not hidden.
3. All actions are audited.
4. System has monitoring and runbooks.
5. Parallel run demonstrates equal or better accuracy than legacy workflow.
6. Business can safely roll back during pilot.
7. Legacy systems are decommissioned only after documented parity.
