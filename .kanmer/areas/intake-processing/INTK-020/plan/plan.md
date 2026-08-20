# Plan — INTK-020

Branch task/intk-020-upload-one-unit from origin/dev (after PLAT-016 merges). Every step names its reuse.

1. **Core normalizer** (reuse: `ImageIntakeLifecycleRules.ValidateNormalizedRegistration`): add `public static string NormalizeRegistrationInput(string? raw)` beside it; `Intake/Details.cshtml.cs` delegates.
2. **Outcome view**: `UploadOutcomeView` + `Guid? ThumbnailReceiptId` (set in `BuildAsync` when the loaded receipt's media type is image/*), + `bool IsOpenDecision` (NeedsReview / ReadyToCreate / PossibleMatch).
3. **Group attach** (reuse: `AttachAsync` body): `AttachGroupAsync(groupId, memberReceiptIds, caseId, reference, reason, actor)` — resolve case once, skip members already on it, one lease, per-member `linkIntake` with derived keys; aggregate result message.
4. **Group page**: compute `OpenGroupDecision` = any member outcome IsOpenDecision. When set: render ONE decision card — "Add this submission to a case" (existing combobox partial contract), "Create a vehicle-image case" (registration input via normalizer; reuse `imageIntakeOriginResolver` for the first open image member; `IRegisterImageIntake` with `SubmissionGroupId` and the automation's replay key `image-intake-register:group:{gid:N}`), Cancel link. Per-file rows render compact outcomes (ViewData flag consumed by `_UploadOutcome`) + thumbnails. Settled group (all attached to one case or registered) keeps the single settled card.
5. **Thumbnails**: `<a href="/Received/{id}/Image"><img loading="lazy" …></a>` tile (reuse `_ImageGallery` CSS classes) on group rows, UploadStatus, Unidentified/Details (its receipt when image), Intake/Details.
6. **Tests** (reuse `UploadConfirmationWebTests` fixtures): group card renders instead of per-file offers for a mixed undecided group; `OnPostAttachGroupAsync` links every open member with one reason and is replay-safe; `OnPostRegisterGroupAsync` registers via the group key with staff-typed VRM; thumbnails render for image members only.
7. Focused suites: UploadConfirmationWebTests, ImageIntakeWebTests, AutomaticImageIntakeTests (registration replay), release build 0/0.

Copy: all new strings pass the (new) no-explanatory-copy rule — labels + one consequence sentence max.

Deviation note: operator barred subagents — self-review recorded in scratch instead of independent review.

## Simplification pass — 2026-08-20 (own diff)

- Reuse: group actions converge on the single-path replay identities (`upload-attach-lease:{rid}:{caseId}` / `upload-attach:{rid}:{caseId}` / automation's `image-intake-register:group:{gid}`) — no new key vocabulary; combobox JS contract, gallery CSS language, `IRegisterImageIntake`, origin resolver, and the ONE new normalizer owner (which absorbed the existing inline copy) all reused.
- Simplification: GET + handlers share one `LoadAsync`; compact per-file rendering is an inherited ViewData flag, not a second partial.
- Found + fixed during testing: composite 3-GUID operation keys exceeded the Core 100-char key bound — converging on the single-path keys removed the problem and the duplication at once; per-member lease/fresh-version loop matches the lease-consumed-per-mutation contract (`CaseMutationGuard.Complete` clears the lease each link).
- Accepted local: `UploadGroupStatusModel` keeps a private `_caseDecision` field because the base deliberately doesn't expose its dependency; widening the base for one consumer would be the bigger change.
- Copy: every new string is label-or-consequence only; checked against the new no-explanatory-copy rule.
