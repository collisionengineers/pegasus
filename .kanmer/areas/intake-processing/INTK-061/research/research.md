# Research — INTK-061

## Question

Restore one durable destination after intake failures without adding a second pipeline.

## Findings

Baseline 3da60bd0c270111d5168dc17246dc831882108ea. The isolated audit is
pegasus_pack/current/intake-audit.md; source facts below were checked at that SHA.

- EfDocumentRequestStore.TryClaimHandOverAsync probes PublicUploadOccurrences before IntakeAssets, but the Worker role is deliberately denied that table (AzureSqlRuntimeRoleMigrationTests:969). FindAsync/RecordAsync already parse intake operation keys correctly.
- DurableIntake commits Completed before allocation, Triage, images and Unidentified. Failure returns are discarded by UnifiedWorkFunction; required Unidentified writes are swallowed. Persist the evaluation before routing while retaining the processing lease/retry owner until routing finishes.
- UniqueMatch association exceptions fall through to allocation, whose CurrentCaseId-only guard can create a duplicate. Both orchestration and allocation must refuse this fallthrough.
- ImageIntakeAutomation drops the canonical group terminal reason and ordinary synchronization registers per receipt. Existing UnidentifiedOrigin.SubmissionGroup supports the correct single record.
- ReconcileGroupedImageIntake pages all NeedsSorting receipts newest-first before filtering; old eligible groups can starve. Query pending eligible groups oldest-first, excluding groups already resolved or registered Unidentified.
- OCR CompleteAsync marks external work complete before analysis; ExecuteAsync no-ops Completed. Retain result output while keeping followup work retryable, replay analysis by its stable operation key, then complete external work.

## Implications

Use existing lease/state/evaluation fields, custody operation identities, U origin,
OCR result JSON and external-work retry conventions. No new runtime, dependency,
queue, grants or provider policy. Existing tests must exercise actual commands
and real restricted SQL role, not merely duplicate declared grant statements.

## Open questions

None requiring operator input. Current session already authorizes this remediation.
No declared project research sources were returned by get_sources.
