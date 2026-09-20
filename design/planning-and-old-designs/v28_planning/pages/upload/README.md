# Upload

- **Live source:** `src/Pegasus.Web/Pages/Upload.cshtml`, `src/Pegasus.Web/Pages/UploadStatus.cshtml`, `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml`, `src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml`
- [**How it works**](how-it-works.md)

## Captured states

Each state is the running application's own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.

| State | Live route | Open | Screenshots |
| --- | --- | --- | --- |
| Upload | `/Upload` | [frame](../../current/pegasus_mail_upload_v28.html#upload) · [page](../../current/states/upload.html) | [1580](../../current/v28-shots/s40-upload-1580.png) · [1440](../../current/v28-shots/s40-upload-1440.png) · [760](../../current/v28-shots/s40-upload-760.png) |
| Upload group status, images awaiting a decision | `/Upload/Group/96b2fc42-af32-42b2-8fa9-9476d4deb851` | [frame](../../current/pegasus_mail_upload_v28.html#upload-group-status) · [page](../../current/states/upload-group-status.html) | [1580](../../current/v28-shots/s41-upload-group-status-1580.png) · [1440](../../current/v28-shots/s41-upload-group-status-1440.png) · [760](../../current/v28-shots/s41-upload-group-status-760.png) |

## Not captured

- Single-file status (`/Upload/Status/{id}`) in its processing, complete and failed outcomes, and upload validation errors. They are responses to a post of real files.
