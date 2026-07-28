# Feature parity across surfaces

Collision Renderer exposes the same document-rendering engine through four
surfaces: the Core library, the command-line client, the WinUI 3 desktop
application, and the cloud API. This document records which capabilities are
reachable from each surface and explains why parity holds by construction rather
than by separate effort on each front end.

The single source of truth is `src/CollisionRenderer.Core`. The CLI, GUI and API
are thin clients; none of them carries its own rendering, templating, density or
validation logic.

## Parity matrix

| Capability | Core API | CLI | WinUI GUI | Cloud API |
| --- | --- | --- | --- | --- |
| List templates | `CollisionRendererFactory.Catalog.List()` | `list` (alias `templates`) | Document-type rail bound to the catalog | `GET /v1/templates` |
| List blank authoring templates | `CollisionRendererFactory.AuthoringCatalog.List()` | `forms list` | Blank-template rail bound to the authoring catalog | `GET /v1/authoring-templates` |
| Get blank draft/form schema | `AuthoringCatalog.GetBlankJson/GetForm` | `forms blank`, `forms schema` | **New blank** and generated form | `GET /v1/authoring-templates/{id}/blank`, `/form` |
| Generate starter draft | `AuthoringCatalog.GetStarterJson(id)` | `forms starter`, optional `--out` | **New document** populates generated prompts | Not exposed |
| Validate payload | `PayloadValidator.Validate(id, model)` | `validate` | Runs as part of render; failures surface in the validation bar | `POST /v1/validate` |
| Render to PDF | `IDocumentRenderer.RenderAsync(request)` | `render` | **Render** button | `POST /v1/render`, `POST /v1/render.pdf`, `POST /v1/render.multipart` |
| Batch render | `IDocumentRenderer.RenderAsync(request)` per item | `batch --manifest` | **Batch...** command | `POST /v1/render/batch` |
| Density: auto-fit | `RenderOptions { Fit = Auto }` | `--density auto` (default) | **Auto** preset (default) | `density: "auto"` (default) |
| Density: normal | `RenderOptions { Fit = Fixed, Density = Normal }` | `--density normal` | **Normal** preset | `density: "normal"` |
| Density: compact | `RenderOptions { Fit = Fixed, Density = Compact }` | `--density compact` | **Compact** preset | `density: "compact"` |
| Density: ultra-compact | `RenderOptions { Fit = Fixed, Density = UltraCompact }` | `--density ultra` | **Ultra** preset | `density: "ultra"` |
| Multi-page flow | Handled inside the engine (A4 paged media, repeating header/footer, non-splitting rows/blocks) | Inherited from Core | Inherited from Core | Inherited from Core |
| Save output | Caller writes `RenderResult.Pdf` | `--out <file.pdf>` (defaults to `SuggestedFileName`) | **Save** picker | Response body is the PDF (`render.pdf`) or base64 (`render`) |
| Open output | Caller opens the bytes | `--open` | **Open** launches the system viewer | Caller handles the returned bytes |
| Preview | Caller renders bytes as it sees fit | Not applicable (console) | WebView2 PDF preview tab | Not applicable (machine-to-machine) |

Notes on the matrix:

- **Validation.** The CLI and the cloud API expose a standalone validate step
  (`validate` / `POST /v1/validate`). The GUI does not present a separate button;
  validation runs inside `RenderAsync`, and a failure raises
  `RenderValidationException`, which the view-model maps to
  `RenderOutcomeKind.ValidationFailed` and displays in the validation bar. In all
  cases the check is the same `PayloadValidator.Validate` call.
- **Preview** is a property of an interactive surface. Only the GUI has a viewport
  (WebView2), so preview is meaningful there. The CLI and API return bytes; the
  caller decides what to do with them.
- **Density** maps identically everywhere. The GUI presets are defined in
  `DensityOption.All` as Auto, Normal, Compact, Ultra — the same four modes the
  CLI accepts via `--density` and the API accepts via the `density` field.

## Parity by construction

Parity is not maintained by re-implementing features per surface. It follows from
a single composition root.

### One composition root

Every host builds its renderer through `CollisionRendererFactory`:

```csharp
public static class CollisionRendererFactory
{
    public static ITemplateCatalog Catalog => TemplateCatalog.Default;

    public static IDocumentRenderer CreateRenderer(IPdfEngine? engine = null);
}
```

- The CLI calls `CollisionRendererFactory.CreateRenderer()` in its `render`
  command and `CollisionRendererFactory.Catalog` for `list`, `sample` and
  `validate`.
- The API registers `CollisionRendererFactory.CreateRenderer()` as a singleton
  `IDocumentRenderer` and reads from `CollisionRendererFactory.Catalog` in its
  template and validate endpoints.
- The GUI view-model holds `CollisionRendererFactory.Catalog` and calls
  `CollisionRendererFactory.CreateRenderer()` inside `RenderAsync`.

Because the same factory wires together the same catalog, HTML composer,
validator and PDF engine, there is no second pipeline for any surface to drift
away from. A capability added to Core appears in every host that already calls the
factory; there is nothing surface-specific to keep in step.

### One renderer, one set of options

All four surfaces produce a `RenderRequest` and hand it to the same
`IDocumentRenderer.RenderAsync`:

```csharp
public sealed record RenderRequest
{
    public required string TemplateId { get; init; }
    public required string Json { get; init; }
    public RenderOptions Options { get; init; } = new();
}
```

Density, fit, base64 inclusion and the page-target behaviour all live in
`RenderOptions` and the template's `DensityProfile`. The CLI's `--density`, the
API's `density` field and the GUI's preset list are three spellings of the same
`RenderOptions` values; none of them changes how the engine behaves.

### Engine behaviour cannot fork per surface

Capabilities that could plausibly be re-implemented inconsistently — multi-page
layout and density auto-fit — are resolved entirely inside `DocumentRenderer`:

- **Multi-page flow** is a property of the composed HTML and the A4 paged-media
  settings the engine applies. Hosts never touch it.
- **Density auto-fit** is decided in `DocumentRenderer.ResolveDensities`: for a
  `FitToPages` template under `DensityFit.Auto`, the renderer tries
  Normal → Compact → Ultra-compact and stops once the page count meets the
  template's target. A fixed density renders once at the requested setting. The
  CLI, GUI and API only choose `Auto` or a fixed density; the fitting loop itself
  is shared.

### Swappable engine, unchanged surfaces

`CreateRenderer` accepts an optional `IPdfEngine`. The default is the headless
Chromium engine; the tests pass a fake. Swapping the engine changes neither the
public contract nor any surface, which is what lets the integration tests exercise
the real pipeline while unit tests stay fast.

## Where the surfaces legitimately differ

Parity is about capability reach, not identical presentation. The surfaces differ
only where their medium demands it:

- The **CLI** is non-interactive: it prints results, writes files and can open the
  output with `--open`, but has no preview.
- The **GUI** is interactive: it adds generated forms, upload pickers, draft open/save, batch
  rendering, a WebView2 preview, file pickers for PDF save/open, and a one-time Chromium
  installation prompt when the engine is missing.
- The **cloud API** is machine-to-machine: it returns either a PDF stream
  (`/v1/render.pdf`) or a JSON artifact with a base64 PDF (`/v1/render`), handles multipart
  uploads and batch requests, adds `GET /healthz`, and supports optional bearer authentication via
  raw or SHA-256 token environment variables.

None of these differences touches what a document can contain or how it is
rendered. Those decisions belong to Core, and Core is the same everywhere.
