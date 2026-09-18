# Vehicle images (Image-initiated Case)

- **Mockup route:** `pegasus_image_intake_v28.html` in [`../../current/`](../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Cases/Index.cshtml` (tab `awaiting`, the list), `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml` (the record, route `/VehicleImages/{id:guid}`)

- [**How it works**](how-it-works.md)

## Screenshots

- [s29-image-intake-list-1580.png](../../current/v28-shots/s29-image-intake-list-1580.png) · [1440](../../current/v28-shots/s29-image-intake-list-1440.png) · [760](../../current/v28-shots/s29-image-intake-list-760.png)
- [s30-image-intake-detail-1580.png](../../current/v28-shots/s30-image-intake-detail-1580.png) · [1440](../../current/v28-shots/s30-image-intake-detail-1440.png) · [760](../../current/v28-shots/s30-image-intake-detail-760.png)

## Notes

- Covered here as one page, not split into subfolders: every live route
  that actually renders UI for this material is exactly two views (the
  Awaiting-instruction list tab and the ImageIntake record), the same shape
  as the `triage_unidentified` lane. The other files the task asked to
  check turned out not to be a multi-step wizard and not this lane's scope
  at all — see the determinations below.
- **`Pages/PreCaseImages/Index.cshtml`** is POST-only
  (`OnGet() => NotFound()`); its comment says it directly: "Post-only owner
  of pre-Case crop and tag; the forms live in the evidence viewer." It owns
  `OnPostCropAsync`/`OnPostTagAsync`, which the shared evidence viewer
  posts to from the image record, Triage and Unidentified pages alike. It
  has no page of its own to transcribe.
- **`Pages/Intake/Source.cshtml`, `Asset.cshtml`, `Image.cshtml`** are three
  unrelated raw GET file-serving routes (`/Received/{id}/Source`,
  `/Received/{id}/Asset/{assetId}`, `/Received/{id}/Image`), each returning
  `File(...)` bytes with no rendered markup at all — not a wizard, not
  sequential steps. They are the download/preview endpoints the gallery's
  tile links and `<img>` sources point at (see `Url.Page("/Intake/Asset",
  ...)` in `ImageIntake/Details.cshtml`). Nothing to capture as UI.
- **`Pages/Cases/Custody.cshtml`** is also `OnGet() => NotFound()` — a
  Case-workspace-only POST action handler (custody retry, logical document
  removal, mark-as-original-report, and a *Case* image's tag/untag) that
  always redirects to `/Cases/Details?section=files`. It is not reached
  from any pre-Case flow; it belongs to the `case_record` lane, not this
  one, and is not duplicated here.
- **`Pages/Cases/Shared/_CaseImageTagPicker.cshtml`** posts only to
  `/Cases/Custody` and is not referenced from `ImageIntake/Details.cshtml`
  or `PreCaseImages/Index.cshtml` (checked by grep) — it is the tag picker
  for an already-associated *Case's* images, not a pre-Case one. Also
  `case_record`'s scope.
- The record ribbon's lifecycle chip is captured with the live app's actual
  (slightly inconsistent) behaviour: `OperatorLabels.ImageIntakeLifecycleState`
  produces "Awaiting definitive instruction" / "Merged into
  Instruction-initiated Case" / "Staff-closed", none of which match any
  keyword in `_StatusChip.cshtml`'s tone table, so all three render as a
  **neutral grey** chip on the record page — unlike the list tab's own
  row, which sets an explicit amber "Awaiting instruction" override with
  different wording. Both are captured faithfully, not reconciled.
- No bulk or merge action exists on the live Awaiting-instruction list or
  the image record; none is shown here.
