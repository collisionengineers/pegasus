# Files — INTK-020

| File | Change |
| --- | --- |
| src/Pegasus.Core/ImageIntake/ImageIntakeLifecycle.cs | Add `NormalizeRegistrationInput` (one owner for staff-typed VRM normalization) |
| src/Pegasus.Web/Pages/Intake/Details.cshtml.cs | Delegate its inline normalization to the new owner |
| src/Pegasus.Web/Presentation/UploadOutcome.cs | `UploadOutcomeView` gains `ThumbnailReceiptId`; open-state kinds get a helper (`IsOpenDecision`) |
| src/Pegasus.Web/Presentation/UploadCaseDecision.cs | New `AttachGroupAsync` (one case resolve, one lease `upload-attach-lease:group:{gid:N}:{caseId:N}`, per-member `upload-attach:group:{gid:N}:{caseId:N}:{rid:N}`) |
| src/Pegasus.Web/Pages/UploadGroupStatus.cshtml(.cs) | Group decision card (register / attach-all / cancel) with handlers `OnPostRegisterGroupAsync` / `OnPostAttachGroupAsync`; per-file rows compact (no actions) whenever the group card shows; thumbnails |
| src/Pegasus.Web/Pages/UploadStatus.cshtml(.cs) | Thumbnail for an image upload |
| src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml | Honour a compact flag (chip + message only) |
| src/Pegasus.Web/Pages/Unidentified/Details.cshtml(.cs) | Thumbnail(s) for image material |
| src/Pegasus.Web/Pages/Intake/Details.cshtml(.cs) | Thumbnail for image receipts |
| src/Pegasus.Web/wwwroot/css/site.css | Thumbnail tile + group-card styles (reuse gallery classes) |
| tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs | Group card + group actions + thumbnail markup tests |
