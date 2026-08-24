# Files

| Path | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Triage/Details.cshtml.cs` | Load the origin receipt in `LoadAsync`, mirroring `Unidentified/Details.cshtml.cs:161-174` (inject the intake read use case; degrade silently on `UnauthorizedAccessException`). Project `InstructionEvidenceImages.Select(receipt.AssetRecords)` to `GalleryImage` using the existing asset route. |
| `src/Pegasus.Web/Pages/Triage/Details.cshtml` | One section: heading plus `<partial name="Shared/_ImageGallery">`. Absent entirely when there are no images — not an empty-state panel. |

That is the whole change.

## Consumed, not modified

| Owner | Why it is reused rather than re-derived |
| --- | --- |
| `InstructionEvidenceImages.Select` | The single owner of "which retained assets are this record's photographs". The gallery, custody, and now [[CASE-021]]'s readiness gate all ask it. |
| `/Received/{id}/Asset/{assetId}` (`Intake/Asset.cshtml.cs`) | Already `PerformCasework`-gated, SHA-256 re-verified, `image/*`-only, inline, `nosniff`. |
| `Pages/Shared/_ImageGallery.cshtml` + `Presentation/GalleryImage.cs` | **Consume only.** [[DOCS-011]] owns edits to this partial; consuming it means this ticket inherits that viewer for free and the two never touch the same lines. |
| `Cases/Details.cshtml:186-201` | The projection shape to copy. |

## Not touched, deliberately

No domain change, no migration, no new table, no custody write, no Box folder.
The bytes stay in one place under the receipt with one authorised route. See the
plan for why the alternative was rejected.

## Documents

None. `docs/design/README.md` already binds the shape (no explanatory sentence
under a heading; a section with nothing to show is absent). FRD-03 would gain a
sentence only if the operator wants this recorded as *required* behaviour rather
than an obvious view — ask, do not assume.
