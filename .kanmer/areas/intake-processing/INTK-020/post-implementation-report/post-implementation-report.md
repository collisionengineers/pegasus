# Post-implementation report — INTK-020

Branch task/intk-020-upload-one-unit (from origin/dev 8caa39a5 line). Delivered exactly the ticket's three limbs:

1. **One decision per submission.** While any member is undecided (`UploadOutcomeView.IsOpenDecision`: NeedsReview / PossibleMatch / ReadyToCreate), `/Upload/Group/{id}` renders a single "This submission" card — Create a vehicle-image case (staff-typed registration via the new Core `NormalizeRegistrationInput` owner, registered through `IRegisterImageIntake` with `SubmissionGroupId` and the automation's own group replay key, so one registration can ever exist), Add to an existing case (existing combobox; new `AttachGroupAsync` links every open member with per-member lease + the single-path replay keys), Cancel. Per-file rows render compact (chip + message, no actions) via an inherited ViewData flag on `_UploadOutcome`.
2. **Thumbnails.** `UploadOutcomeView.ThumbnailReceiptId` (set when the receipt is image/*) renders a lazy `<img>` tile linking to the existing staff-only `/Received/{id}/Image` route on the group page, UploadStatus, Unidentified details, and Intake details.
3. **Group actions are single operations** resolving through the existing Core paths (link merges registered image cases; Unidentified resolution rides the existing reconciliation).

Tests: 3 new integration tests (one-card-instead-of-per-file-offers incl. thumbnail markup; staff group registration + replay; attach-all + replay) — UploadConfirmationWebTests 9/9, ImageIntakeWebTests 2/2, Core image/grouped suites 32/32, full Release build 0 warnings / 0 errors. Fixed during testing: per-member lease acquisition (a lease is consumed per mutation) and operation-key length (converged on the single-path keys).

Deviation: operator barred subagents this round — self-review recorded here and in scratch; no independent reviewer.

## Verification hand-off
Post-deploy: a real mixed multi-file upload shows one decision card with thumbnails; each action lands every member on one destination.
