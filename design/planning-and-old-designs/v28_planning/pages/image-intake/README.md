# Image intake

- **Live source:** `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml`, `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml.cs`, `src/Pegasus.Web/Pages/Cases/Index.cshtml`, `src/Pegasus.Web/Pages/Shared/_ImageGallery.cshtml`, `src/Pegasus.Web/Pages/Shared/_EvidenceViewer.cshtml`
- [**How it works**](how-it-works.md)

Images that arrived before their instruction sit in the Awaiting instruction tab of Cases and open at `/VehicleImages/{id}`. `/PreCaseImages` answers 404 to a GET.

## Captured states

Each state is the running application's own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.

| State | Live route | Open | Screenshots |
| --- | --- | --- | --- |
| Cases, Awaiting instruction tab | `/Cases?tab=awaiting` | [frame](../../current/pegasus_image_intake_v28.html#cases-awaiting) · [page](../../current/states/cases-awaiting.html) | [1580](../../current/v28-shots/s30-cases-awaiting-1580.png) · [1440](../../current/v28-shots/s30-cases-awaiting-1440.png) · [760](../../current/v28-shots/s30-cases-awaiting-760.png) |
| Cases, Awaiting instruction tab, row selected | `/Cases?tab=awaiting&selected=7fad92cc-cad2-4caf-8e69-9010a3769f3a` | [frame](../../current/pegasus_image_intake_v28.html#cases-awaiting-selected) · [page](../../current/states/cases-awaiting-selected.html) | [1580](../../current/v28-shots/s31-cases-awaiting-selected-1580.png) · [1440](../../current/v28-shots/s31-cases-awaiting-selected-1440.png) · [760](../../current/v28-shots/s31-cases-awaiting-selected-760.png) |
| Image intake record | `/VehicleImages/7fad92cc-cad2-4caf-8e69-9010a3769f3a` | [frame](../../current/pegasus_image_intake_v28.html#image-intake-record) · [page](../../current/states/image-intake-record.html) | [1580](../../current/v28-shots/s32-image-intake-record-1580.png) · [1440](../../current/v28-shots/s32-image-intake-record-1440.png) · [760](../../current/v28-shots/s32-image-intake-record-760.png) |

## Not captured

- The attach confirmation step ("Confirm and add to this case"), the edit session with tag pickers, and the "Original file unavailable" state.
