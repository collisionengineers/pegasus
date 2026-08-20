# CASE-006 file map

## Core
- `src/Pegasus.Core/Intake/DownloadIntakeSource.cs` + `IntakeContracts.cs` — `IntakeSourceDownload.ContentType` becomes the stored asset media type (single line each; presentation still decided per endpoint).
- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` — `ImageIntakeImage(ReceiptId, FileName, MediaType)` record + `IImageIntakeQueries.ListImagesAsync` (default `[]`).

## Infrastructure
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` — implement `ListImagesAsync` reusing `ResolveGroupMemberReceiptsAsync` (INTK-014).

## Web
- `src/Pegasus.Web/Pages/Intake/Image.cshtml(.cs)` — NEW inline image endpoint `/Received/{id:guid}/Image` (staff-only, image/* only, inline, nosniff, no-store).
- `src/Pegasus.Web/Pages/Shared/_ImageGallery.cshtml` — NEW shared gallery partial.
- `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml(.cs)` — Images section via the partial.
- `src/Pegasus.Web/Pages/Cases/Details.cshtml(.cs)` — evidence tab: per-intake gallery blocks (replaces the 3-column table, keeping reference link + registered time).
- `src/Pegasus.Web/wwwroot/css/site.css` — `.image-gallery` styles (no inline styles; CSP).

## Tests
- `tests/Pegasus.IntegrationTests/ImageViewingWebTests.cs` — NEW: image/* inline content type + nosniff; non-image receipt 404; anonymous → sign-in redirect; roleless → 403; galleries render `<img>` on both pages.
- Existing suites to keep green: `MultiFormatIntakeWebTests` (octet-stream pin), `ImageIntakeWebTests`, `CaseDetailsWebTests`, Browser suites incl. `AccessibilityTests`.
