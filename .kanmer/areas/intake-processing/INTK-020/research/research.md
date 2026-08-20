# Research — INTK-020

Verified at origin/dev a1775841 (read-only):

- Per-file offers come from `UploadOutcomeQueries.BuildAsync` (`Presentation/UploadOutcome.cs`): each member independently resolves to NeedsReview (open Unidentified → "Review"), ReadyToCreate ("Create a case"), PossibleMatch, Attached, or ImageCaseRegistered. `UploadGroupStatusModel` collapses to one card only when ALL members share the same ImageCaseRegistered outcome — any mix keeps per-file offers, which is exactly the operator's screenshot.
- **Reusable staff registration exists**: `IRegisterImageIntake` (`RegisterImageIntake` use case) accepts a staff actor (`ValidateRegister` → `StaffAuthorization.Require(PerformCasework)`), takes `SubmissionGroupId` — `EfImageIntakeStore.RegisterAsync` with a group id flips every member receipt in the registration transaction (INTK-015 machinery). `Pages/Intake/Details.cshtml.cs:513` already does per-receipt staff registration with inline VRM normalization and `imageIntakeOriginResolver.ResolveOriginAsync`.
- **Reusable attach exists**: `UploadCaseDecision.AttachAsync` — resolve case (id or exact-reference), lease `upload-attach-lease:{receiptId:N}:{caseId:N}`, `ILinkIntake` (which merges a registered image case), replay-safe. Group version needs one lease + per-member link keys.
- Automation registration replay key is `image-intake-register:group:{groupId:N}` — a staff group registration should use the SAME key so exactly one registration can ever exist per group (replay probe returns it).
- Thumbnails: `/Received/{id:guid}/Image` (`Pages/Intake/Image.cshtml`) already serves staff-only inline image/* — reuse as `<img>` src. `QueuedIntakeStatus` has no media type; `UploadOutcomeQueries.BuildAsync` already loads the receipt, so the outcome view can carry a `ThumbnailReceiptId` when the receipt is image/*.
- VRM normalization currently inline at `Intake/Details.cshtml.cs:523-526` (single copy) — extract to one Core owner before adding a second caller.

Premises assumed (not re-verified): browser combobox JS from INTK-016 works unchanged when the form posts group handlers (same `data-case-search` contract).
