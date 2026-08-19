# Research — INTK-006: vehicle image produced no case or visible outcome

## Question

What happened to the reported production vehicle-image upload, why was no case created, and where is the real defect?

## Azure/runtime evidence (read-only, 2026-08-19)

- The active Web revision is `pegasus-prod-web-252ow37gij--d8de29cb94f3`, created 2026-08-18 14:23 UTC, healthy, one replica, and receiving 100% traffic. This is release 10.
- The production Function App exposes all nine expected functions, including `IntakeWorkFunction`, `PendingWorkDispatchFunction`, and `StagedArtifactReconciliationFunction`.
- Azure SQL readback found one recent `manual_upload`: JPEG, 295,933 bytes, received 2026-08-19 09:09:15 UTC. Its work item completed on attempt 1 at 09:10:12 UTC with no work-item or receipt failure code.
- The processed receipt decision is `needs_sorting`; no Case has that receipt as origin. The file was therefore not lost and Worker processing did not fail.
- Application Insights confirms the Worker is emitting current telemetry. It has no correlated Web upload exporter because the Web host intentionally lacks one. The recurring Sent-evidence polling authorization exception seen in the same window is a separate incident and does not explain this receipt.
- Resource Health API did not support the Container Apps query used and rejected the Function App query for authorization/provider reasons; active revision health, function discovery, telemetry, and SQL state provide the useful service-specific evidence instead.
- Recent failed Azure control-plane activity concerns release/deployment container writes on 2026-08-18, not the 2026-08-19 image receipt.

## Code and policy findings

- `UploadModel.OnPostAsync` stages one file and redirects to `/Upload/Status/{stagedReceiptId}`. A successful POST does not create a case synchronously.
- `UploadStatusModel` auto-refreshes while received/processing. When processing completes without a case it links to the retained receipt, but its heading/message are only `Complete` / `Processing is complete.`; it does not disclose the receipt's `needs_sorting` decision.
- Direct JPEG/PNG inputs are intentionally accepted into `NeedsSorting` without an instruction-derived principal. Tests in `MultiFormatIntakeWebTests` and genuine-corpus tests assert this behaviour.
- TICK-011/INT-17 implements source-image-bound VRM recognition and registration after durable intake. Its governing FRD explicitly separates an image registration suggestion/registration from Case association and Case/PO allocation. TICK-011 records production caller execution as not established.
- Product invariants require Case/PO allocation to fail closed when principal identity or processing evidence is incomplete. A lone ordinary vehicle image does not identify an accepted work provider/principal, so creating a case from this upload would be a defect.
- The reported “nothing happened” is therefore an observability/expectation defect, not a lost-upload or failed-case-creation defect: the UI collapses a meaningful `Needs sorting` terminal result into generic `Complete`.

## Root cause and implication

The production pipeline behaved as designed: receipt retained, processed once, terminal `Needs sorting`, no case. The operator surface does not explain that outcome or direct the operator clearly to the work queue/receipt, so successful fail-closed processing appears inert. The fix should expose the existing canonical decision and next action on the status page; it must not weaken principal/allocation gates.

## Verified premises

Azure inventory, active revision health, Function discovery, Application Insights, production SQL aggregate/readback, current source, focused tests, TICK-011 documents, FRD-02 and FRD-06.

## Assumptions

The correlated manual JPEG is the user-reported upload because it is the only recent manual upload and matches the reported media type. No filename, content, actor identity, or other PII was read.

## Operator clarification and exact VRM trace — 2026-08-19

The operator confirmed the binding outcome is exhaustive: (1) associate to the one unambiguous case when a VRM match exists, or (2) create an Image-Only case. `Needs sorting` without either case outcome is not an accepted terminal path for an uploaded vehicle image.

A second read-only production query traced the correlated JPEG through the image tables:

- `ImageVrmSuggestions` contains exactly one row, proving Pegasus ran recognition.
- Engine: `fast-alpr-onnx`, version `1`.
- Outcome: `suggested`.
- Suggested value: `61644`.
- Confidence: `0.160449`, below the `0.80` automatic-action bar.
- Failure code: none; disposition remains `pending`.
- `ImageIntakes`: zero rows for the receipt.
- `IntakeManualAssociations`: zero rows for the receipt.

This corrects the earlier implication that policy-consistent `Needs sorting` was the intended final result. It is consistent with the current implementation, but not with the operator-confirmed product requirement. The root defect is in the outcome policy/caller: `ImageIntakeAutomation.ApplyAsync` returns the unchanged receipt whenever it does not obtain exactly one distinct suggestion at or above 0.80, so the fallback Image-Only case is never created. Status wording is a secondary manifestation.

The linked FRD-06 currently describes threshold-gated registration and association, while the clarified requirement makes Image-Only case creation mandatory below/without the bar. The governing behaviour must be reconciled before implementation; do not silently reinterpret `ImageIntake` registration as an allocated Image-Only case.
