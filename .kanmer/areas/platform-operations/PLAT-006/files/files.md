# Files — PLAT-006

Verified by read-only checks on 2026-08-19 (worktree `../pegasus-worktrees/plat-006-shell-upload`, cut from `origin/dev` `60fde326`).

| File | Change | Why |
| --- | --- | --- |
| `src/Pegasus.Web/wwwroot/css/site.css` | `.app-rail-main` gains `margin-inline: auto` (rail block, ~line 408); new dropzone states (`is-enhanced`, `is-dragover`, `has-file`, `__glyph`, `__browse`, `__file`, `:focus-within`), an `.upload-layout` two-column grid with its ≤1023px reflow, and an `.accepted-list` for the formats panel | The one stylesheet; the CSP discards inline styles so every new style is a named class here |
| `src/Pegasus.Web/wwwroot/js/site.js` | New `[data-dropzone]` enhancement: whole dashed area is a drop target, a `Choose file` button drives the real `<input type=file>`, chosen name/size read back to a live region | Progressive enhancement only — the native input stays the no-script control, matching the file's existing philosophy |
| `src/Pegasus.Web/Pages/Upload.cshtml` | Two-column layout (form + "What happens next" / accepted formats panel), enhanced dropzone markup, `Choose file` button, readout | The screen the operator called visually poor |
| `src/Pegasus.Web/Pages/Uploads/Request.cshtml` | Same dropzone markup (data attributes + browse button + readout) on the public request page | One dropzone behaviour, both callers |
| `docs/design/README.md` | Rail section: state that the content region is bounded (1280px) and centred in the space beside the rail | The design authority must say what the shell does beyond the cap |
| `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` | Unchanged — the `/Upload` and `/Uploads/{token}` routes are already in the axe/inline-style theory | Guard, not change |

Untouched: `Pegasus.Core`, `Pegasus.Infrastructure`, `Pegasus.Worker`, `Upload.cshtml.cs`, `Request.cshtml.cs` (form field names `Upload`, `ExternalReceiptToken`, `Token`, `OperationKey` are unchanged, so `IntakeWebTestSupport.PostUploadAsync` and the journey tests keep working). `docs/design/system/` is not authored (it copies `site.css` at build); a `/design-sync` refresh is a follow-up, not part of this fix.
