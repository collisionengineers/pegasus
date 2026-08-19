# Research — INTK-006: grouped vehicle images must associate or create one Image-initiated Case

## Question

Why did the reported vehicle image produce no case, did both recognition layers run, and what exact grouped outcome must replace the current terminal path?

## Production evidence (read-only, 2026-08-19)

- Active Web revision: `pegasus-prod-web-252ow37gij--d8de29cb94f3`, release 10, healthy, one replica, 100% traffic.
- The Function App exposed all expected intake functions, including intake processing, pending dispatch, and staged-artifact reconciliation.
- Azure SQL identified the only matching recent manual upload: JPEG, 295,933 bytes, received 09:09:15 UTC; work completed on attempt 1 at 09:10:12 UTC with no work or receipt failure code.
- The retained receipt decision was `needs_sorting`; there was no origin Case, `ImageIntakes` row, or manual association.
- `ImageVrmSuggestions` contained one persisted row: engine `fast-alpr-onnx` v1, outcome `suggested`, text `61644`, confidence `0.160449`, no failure code, pending disposition. This is below the accepted 0.80 automatic-action bar.
- The in-process engine is sequential: `PlateDetector.Detect` must produce a crop, then `PlateRecognizer.Recognize` produces normalized text. A persisted suggestion cannot exist unless both layers ran. Therefore both ran for the correlated image and neither returned a technical failure.
- The deployed engine stores pinned hashes for detector, recognizer, and configuration but emits no separate per-layer telemetry.
- A separate 09:35 JPEG produced `no_readable_result`. Current persistence cannot distinguish detector-empty from detector-success/recognizer-empty.
- The user-supplied clipboard PNG has different bytes/hash from stored JPEGs after clipboard transcoding; visual similarity cannot prove byte identity.
- The original source filename is retained in `IntakeStagedReceipt.SourceFileName` and processed receipt persistence. The earlier production query deliberately did not read PII/filename; code inspection confirms the field exists. An authorized diagnostic may query that one field by correlated receipt if still required.

## Current code path and defect

- `ProcessQueuedIntake` invokes `IImageIntakeAutomation.ApplyAsync` after durable receipt processing/allocation work.
- `ImageIntakeAutomation.ApplyAsync` only considers image assets on one receipt. It collects suggestions at/above 0.80 and continues only when there is exactly one distinct normalized registration.
- Below-bar, no-readable, or conflicting results return the unchanged `NeedsSorting` receipt. No Image Intake is registered, no existing Case is associated, and no fallback Case is created.
- With one accepted registration, the automation calls the existing eligible-case candidate query. Exactly one eligible candidate permits registration/association; zero or several do not create the required Image-initiated Case.
- `ImageIntakeCasePairing` already owns exact-registration, unique eligible-case pairing in the opposite direction and must be reused or made the shared policy owner rather than copied.
- `EfImageIntakeStore` provides idempotent Image Intake registration, its own immutable Image Intake reference sequence, origin receipt uniqueness, suggestions, and association queries. An Image Intake is currently explicitly pre-Case and is not an Image-initiated Case.
- Case creation is owned by the existing Core case acceptance/allocation path and persisted by the existing case store. Direct EF insertion or a second allocator would violate the one-Core-owner rule.

## Binding group outcome

For every durable upload group produced by [[INTK-005]]:

1. Load all retained image members in stable ordinal order.
2. Run/consume recognition for every eligible image; a member with no visible plate remains part of the evidence group.
3. Normalize only suggestions meeting the accepted automatic bar.
4. If exactly one distinct accepted VRM exists across the group and exactly one eligible pre-report Case matches it with no contradictory identity evidence, associate every image member with that Case.
5. In every other completed recognition state—no accepted VRM, conflicting accepted VRMs, zero eligible Case matches, or multiple eligible matches—create exactly one Image-initiated Case for the group and attach every image member to it.
6. Never split one group across Cases. Never attach based on filename, time proximity, low-confidence text, or first-completing member.
7. Replay, out-of-order worker completion, concurrent processing, and retry must converge on the same single group outcome.

A close-up without a registration therefore follows a registration-bearing sibling. Conflicting confident registrations do not authorize an existing-case attachment; the intact group receives one Image-initiated Case for staff resolution.

## Recognition state required for correct orchestration

- Group outcome cannot finalize merely when the first member completes. The group query must know every member and whether each receipt's image recognition has reached a terminal state.
- Recognition needs one canonical result per member/asset: accepted suggestion(s), no plate detected, plate detected but unreadable, or technical failure. The first three are completed evidence states; technical failure follows bounded retry/failure policy and must not be silently treated as “no VRM.”
- Detector/reader diagnostic detail belongs in the recognition result/telemetry, not in a duplicate intake decision taxonomy.
- No image pixels, crops, or registration candidates belong in logs. Safe telemetry is group/receipt correlation, model versions, stage outcome, candidate count, and confidence category.

## Governing-document conflict

- Current operator notes and FRD-02 state that image-only material is pre-Case and does not create a definitive Case merely because images arrived.
- Product invariants require principal identity before normal Case/PO allocation.
- The clarified user requirement explicitly requires an Image-initiated Case fallback.
- Therefore implementation must not begin until kanmer-docs reconciles operator truth, PRD/FRD behavior, Case/reference/principal semantics, and the exact meaning of “Image-initiated Case.” This is a behavior change, not a UI fix.
- No ADR is automatically required: reuse of current stores/runtime is expected. An ADR is needed only if the governing-doc work proves a new architectural boundary is unavoidable.

## Verified premises

Production Azure inventory/health, Function discovery, Application Insights, targeted SQL readback, `OnnxVrmRecognitionEngine`, `ImageIntakeAutomation`, pairing/lifecycle contracts, EF mappings, focused Core/integration tests, FRD-02, FRD-06, operator notes, and EPIC-007 context.

## Open questions

None for ticket planning. The governing-document step must explicitly settle the Image-initiated Case's reference/principal/lifecycle semantics before code; the implementer is instructed to stop if that prerequisite is absent rather than invent it.


## Parallel-branch execution note — 2026-08-19

This ticket is intentionally implemented from the INTK-005 PR branch before PR merge. Record the exact base SHA in execution scratch and ticket notes. When INTK-005 is reviewed, rebase this branch onto the reviewed INTK-005 result and resolve any conflicts before its PR is finalized. INTK-005 review/merge coordination is not an execution blocker.
