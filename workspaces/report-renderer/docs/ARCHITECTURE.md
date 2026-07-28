# Architecture

Collision Renderer produces Collision Engineers Ltd's branded PDF documents — vehicle
valuation reports, advert evidence packs, fee notes and expert reports — in one consistent
house style. This document explains how the system is put together: the single rendering
engine, the pipeline a payload travels through, the page-furniture and density auto-fit
mechanisms, the swappable PDF engine seam, the container topology and the points where the
system is meant to be extended.

The guiding principle is **one shared engine, thin clients**. `CollisionRenderer.Core` is the
only code that knows how to turn a document model into a PDF. The CLI, the desktop app and the
cloud API are thin hosts over it. Because they all build the renderer through the same
composition root, feature parity holds by construction rather than by discipline.

## Solution layout

`CollisionRenderer.sln` is a single .NET 8 solution with four shipped projects and one test
project.

| Project | Target | Role |
| --- | --- | --- |
| `src/CollisionRenderer.Core` | `net8.0` (class library) | The single rendering engine and source of truth. Typed C# document models to HTML via Scriban + the brand CSS, then to PDF via headless Chromium. The top-level design sources are embedded at build time, so Core is self-contained at runtime. No Windows-only dependencies. |
| `src/CollisionRenderer.Cli` | `net8.0` console (assembly `collisionrenderer`) | Thin command-line client over Core. |
| `src/CollisionRenderer.Api` | `net8.0` ASP.NET Core minimal API | Cloud service wrapping Core; packaged by the workspace `Dockerfile`. |
| `src/CollisionRenderer.Gui` | `net8.0-windows`, WinUI 3 / Windows App SDK | Desktop thin client; in-process Core; WebView2 preview. |
| `tests/CollisionRenderer.Core.Tests` | xUnit | 57 tests, including real-Chromium integration renders. |

Core depends on `Scriban` (5.12.1), `Microsoft.Playwright` (1.49.0) and `PDFsharp` (6.2.4).
PDFsharp is used only to append uploaded advert evidence PDFs after Chromium has rendered the
branded evidence pack; it is not used for document layout. Core has zero Windows dependencies,
which is what lets the same code run on a Windows desktop and in a Linux container.

## Component diagram

```
            ┌──────────────┐   ┌──────────────┐   ┌──────────────┐
   thin     │     CLI      │   │     GUI      │   │     API      │
  clients   │ collision-   │   │  WinUI 3 +   │   │ ASP.NET Core │
            │  renderer    │   │  WebView2    │   │ minimal API  │
            └──────┬───────┘   └──────┬───────┘   └──────┬───────┘
                   │                  │                  │
                   └──────────────────┼──────────────────┘
                                      │
                       CollisionRendererFactory
                   .CreateRenderer(IPdfEngine?)  .Catalog
                                      │
        ┌─────────────────────────────────────────────────────────┐
        │              CollisionRenderer.Core                      │
        │                                                          │
        │   ITemplateCatalog ──── TemplateDescriptor (11 built-in) │
        │   IAuthoringTemplateCatalog ── blank form definitions    │
        │          │                                               │
        │          ▼                                               │
        │   IDocumentRenderer (DocumentRenderer)                   │
        │          │                                               │
        │   ┌──────┴───────┬───────────────┬──────────────────┐   │
        │   ▼              ▼               ▼                  ▼   │
        │  Models     IPayloadValidator  IHtmlComposer    IPdfEngine
        │ (typed C#)  (PayloadValidator) (HtmlComposer)        │   │
        │                                   │                  │   │
        │                       Scriban body + C# letterhead   │   │
        │                       shell + report.css (embedded)  │   │
        └───────────────────────────────────────────────────┼─────┘
                                                             │
                                              ┌──────────────┴───────────┐
                                              │  ChromiumPdfEngine        │
                                              │  (Microsoft.Playwright)   │
                                              │  headless Chromium        │
                                              └──────────────┬───────────┘
                                                             ▼
                                                       PDF bytes (A4)
```

The arrows describe ownership and call direction. The three hosts call into Core only through
`CollisionRendererFactory`; they never touch the composer, validator or engine directly. Within
Core, `DocumentRenderer` is the orchestrator that pulls the catalog, the validator, the HTML
composer and the PDF engine together.

## The Core engine

### Composition root

`CollisionRendererFactory` is the one place a renderer is built. Every host calls it, so they all
get the same wiring:

```csharp
public static IDocumentRenderer CreateRenderer(IPdfEngine? engine = null)
{
    var brand     = BrandAssets.Default;
    var catalog   = TemplateCatalog.Default;
    var composer  = new HtmlComposer(brand, catalog);
    var validator = new PayloadValidator();

    if (engine is null)
        return new DocumentRenderer(catalog, composer, validator, new ChromiumPdfEngine(), ownsEngine: true);

    return new DocumentRenderer(catalog, composer, validator, engine, ownsEngine: false);
}
```

`CollisionRendererFactory.Catalog` exposes the shared, stateless `ITemplateCatalog`. When the
caller supplies its own `IPdfEngine`, the renderer does not own it (the caller is responsible for
disposal); when Core builds the default `ChromiumPdfEngine`, the renderer owns and disposes it.

### Public surface

The contract a host programs against is small.

| Type | Purpose |
| --- | --- |
| `ITemplateCatalog` | `List()` → `TemplateDescriptor`; `Get(id)`; `TryGet(id, out)`. |
| `IDocumentRenderer` | `RenderAsync(RenderRequest)` → `RenderResult`. `IAsyncDisposable`. |
| `RenderRequest` | `{ TemplateId, Json, Options }`. |
| `RenderOptions` | `{ Fit: DensityFit, Density: Density, IncludeBase64, Base64Limit }`. |
| `RenderResult` | `{ Pdf, PageCount, Sha256, Density, EngineVersion, SuggestedFileName, Warnings, Base64? }`. |
| `IPayloadValidator` | `Validate(id, model)` → `ValidationResult { Ok, Errors, Warnings }`. |
| `RenderValidationException` | Thrown when validation fails; carries `Errors`. |
| `IPdfEngine` | The swappable PDF backend. |

`Density` is `Normal | Compact | UltraCompact`; `DensityFit` is `Auto | Fixed`. Per-template
overflow policy is `DensityFitProfile` (`None | FitToPages`).

### Templates and the catalog

`TemplateCatalog.Default` registers 12 render templates, each described by a `TemplateDescriptor`
holding the id, display name, description, model `Type`, the embedded Scriban body resource, the
density profile and the file-name suffix.

| Id | Model | Density profile |
| --- | --- | --- |
| `market-valuation-evidence` | `MarketValuationEvidenceDocument` | `FitToPages`, target 1 page |
| `advert-evidence-pack` | `AdvertEvidencePackDocument` | `None` |
| `fee-note` | `FeeNoteDocument` | `None` |
| `expert-report` | `ExpertReportDocument` | `None` |
| `repairable-contract-repair-report` | `ExpertReportDocument` | `None` |
| `total-loss-report` | `ExpertReportDocument` | `None` |
| `addendum-report` | `ExpertReportDocument` | `None` |
| `diminution-rebuttal` | `ExpertReportDocument` | `None` |
| `roadworthy-criminal-report` | `ExpertReportDocument` | `None` |
| `part-35-response` | `ExpertReportDocument` | `None` |
| `response-letter` | `ExpertReportDocument` | `None` |

The valuation template is the only one that auto-fits to a page target. The expert report is the
flexible one: it is built from content blocks — `paragraph`, `bullets`, `datatable`, `keyvalue`,
`evidencetable`, `valuebox`, `mediarow` — which `HtmlComposer.BlockCtx` maps to template context,
and which `PayloadValidator` checks against its `KnownBlockTypes` set.

## The rendering pipeline

`DocumentRenderer.RenderAsync` runs the same steps for every document.

1. **Resolve the template.** `_catalog.Get(request.TemplateId)` returns the `TemplateDescriptor`,
   which carries the model type, the Scriban body resource and the density profile.
2. **Deserialise.** The JSON payload is deserialised into the descriptor's `ModelType` using the
   shared `CrJson.Options` (camelCase, case-insensitive, trailing commas allowed, enums as
   strings). A `JsonException` is rewrapped as a `RenderValidationException` so callers see one
   error contract.
3. **Validate.** `_validator.Validate(descriptor.Id, model)` runs the per-template schema and
   policy checks. If `Ok` is false the renderer throws `RenderValidationException` carrying the
   errors; warnings are collected and carried forward into the result.
4. **Resolve the density list.** See [Density auto-fit](#density-auto-fit). A `Fixed` request
   yields a single density; an `Auto` request on a `FitToPages` template yields the ladder
   `Normal → Compact → UltraCompact`; anything else yields `Normal` only.
5. **Compose and render, per density.** For each density in turn: `_composer.Compose` builds the
   print-ready `ComposedDocument` (HTML + `PdfPageSettings`); `_engine.RenderHtmlToPdfAsync`
   produces PDF bytes; `_engine.CountPages` returns the page total. If the template is not
   `FitToPages`, or the page count is within the target, the loop stops.
6. **Record overflow.** If a `FitToPages` template still exceeds its target at the tightest
   density, a warning is added noting that the document rendered cleanly across the actual page
   count rather than being garbled.
7. **Finalise.** Compute the SHA-256 hex of the PDF, build the suggested file name from the model
   (registration / fee-note number / our-ref slugged to `{KEY}_{suffix}.pdf`), and optionally
   attach a base64 copy if `IncludeBase64` is set and the artefact fits inside `Base64Limit`.
8. **Return `RenderResult`** with the PDF, page count, hash, density actually used, engine version
   string, suggested file name and warnings.

### HTML composition

`HtmlComposer.Compose` switches on the template id to build a `DocChrome` — title, body HTML,
the Our/Your-Ref/Date values, footer strapline and a CSS body class for the chosen density. The
body comes from rendering the embedded Scriban template through a `ScriptObject` context that the
composer has populated with already-encoded, already-formatted values. The composer then wraps
that body in the **letterhead shell** built in C# (`Shell` + `Letterhead`): a single HTML document
with the embedded `report.css` inlined in a `<style>` block, the gear-"C" logo as a data URI, and
the reference table.

Two correctness properties are enforced here:

- **All payload text is HTML-encoded** through `Format.Enc` before it reaches a template, so no
  field can inject markup. Templates are first-party embedded artefacts and are never compiled
  from end-user input.
- **Currency, mileage, year and vehicle history are formatted** by `Format` helpers
  (for example `£12,500.00`) so figures look consistent regardless of how the payload was typed.

Parsed Scriban templates are cached in a `ConcurrentDictionary` keyed by resource name, so each
template is parsed once per process.

## Page furniture

"Page furniture" is the repeating per-page chrome that makes every page look like a Collision
Engineers document. It is split between the document HTML and the Chromium running templates.

`PdfPageSettings` carries the A4 paged-media settings handed to the engine:

| Setting | Value |
| --- | --- |
| `Format` | `A4` |
| Margins | top `1mm`, right `12mm`, bottom `22mm`, left `12mm` |
| `HeaderHtml` | running header (Chromium template) |
| `FooterHtml` | running footer (Chromium template) — the strapline + page marker |
| `PrintBackground` | `true` |

The composer supplies a blank running header (`<div></div>`) and a running **footer** built by
`FooterTemplate`: a thin red rule (`#c80a32`), the strapline, and a right-aligned page marker that
uses Chromium's `<span class="pageNumber">`/`<span class="totalPages">` placeholders to print
`— n of N —` on every page. The bottom margin of `22mm` reserves the band the footer occupies.

The strapline itself is document-type-dependent. Valuation, evidence pack and expert reports use
`Collision Engineers Ltd | www.CollisionEngineers.co.uk | engineers@collisionengineers.co.uk`. A
**fee note swaps the email for the VAT registration number** (`... | VAT Reg No. {VatNumber}`),
falling back to a bare strapline when no VAT number is supplied.

The first-page furniture — the logo letterhead, the centred UPPERCASE title (red on newer
outputs), the UPPERCASE section headings with a red rule under them — lives in the HTML produced
by `Letterhead` and the Scriban body, styled by `report.css`. The stylesheet provides a data
register at 8.8pt for valuation/evidence/fee documents and a letter register at 10pt for expert
reports.

### Multi-page robustness

The requirement is that long documents must not garble the look. This is achieved with standard
CSS paged-media features that Chromium honours:

- `@page A4` plus the Chromium running header/footer templates, which repeat on every page with
  live page numbers.
- `thead { display: table-header-group }`, so evidence and line-item table headers repeat at the
  top of each page a table spans.
- `tr`, value-box and media-row `{ break-inside: avoid }`, so a single row or block never splits
  across a page boundary.

This is validated: a 36-row valuation flows to three pages with a repeating header and footer and
no garbling.

## Density auto-fit

Auto-fit lets fit-to-page documents tighten their layout until they hit a page target, instead of
spilling onto an extra page or being manually re-edited. It applies only to templates whose
`DensityProfile` is `FitToPages` (currently the valuation, target one page).

`DocumentRenderer.ResolveDensities` decides what to try:

```csharp
if (options.Fit == DensityFit.Fixed)
    return new List<Density> { options.Density };

return d.DensityProfile == DensityFitProfile.FitToPages
    ? new List<Density> { Density.Normal, Density.Compact, Density.UltraCompact }
    : new List<Density> { Density.Normal };
```

When auto-fit is in play, the render loop renders at `Normal`, counts pages, and if the count
still exceeds `FitTargetPages` re-renders at `Compact`, then `UltraCompact`. The first density
that lands within the target wins, and that density is reported back on `RenderResult.Density`.
Each density maps to a body CSS class (`report-compact`, `report-ultra-compact`) that tightens
spacing and type. If even `UltraCompact` overflows, the loop stops at that density and a warning
records the actual page count — the document still renders cleanly, just across more pages.

Page counting is done by `PdfPageCounter`, which reads the PDF bytes as Latin-1 text and takes
the largest `/Count` value in the page tree, falling back to counting `/Type /Page` objects. It
exists only to drive auto-fit, so a rare off-by-one on exotic input is harmless.

This too is validated: the sample valuation auto-fits to `Compact` to stay on one page.

## The `IPdfEngine` seam

The PDF backend sits behind a narrow interface so it can be swapped without touching the
pipeline:

```csharp
public interface IPdfEngine : IAsyncDisposable
{
    Task<byte[]> RenderHtmlToPdfAsync(string html, PdfPageSettings settings, CancellationToken ct = default);
    int CountPages(byte[] pdf);
    string EngineVersion { get; }
}
```

The default implementation is `ChromiumPdfEngine`, which drives headless Chromium through
`Microsoft.Playwright`. It launches the browser once (`--no-sandbox`,
`--font-render-hinting=none`), guards the lazy launch with a `SemaphoreSlim`, and reuses the
instance across renders — opening a fresh page per render and closing it afterwards. It translates
`DisplayHeaderFooter` and the header/footer templates from `PdfPageSettings` into Playwright's
`PagePdfOptions`. If Chromium is not installed it raises an `InvalidOperationException` whose
message points at the bundled installer; the GUI recognises this and reports `BrowserMissing`
rather than a generic failure.

The seam is what makes the engine testable. The test project supplies a `FakePdfEngine` and passes
it to `CollisionRendererFactory.CreateRenderer(engine)`, so unit tests exercise composition,
validation and the auto-fit loop without launching a browser; the integration tests use the real
`ChromiumPdfEngine`.

## Why this stack

The brand design system (`collision-engineers-design`) is CSS-native, and the preferred sample
outputs were produced by an HTML/CSS renderer, so reusing that CSS through headless Chromium gives
exact fidelity and makes new templates cheap. Chromium via Playwright is cross-platform and
self-contained, unlike the prior WeasyPrint renderer, which needed fragile GTK/Pango native
libraries on Windows and had to hunt for MSYS2 DLLs before falling back to ReportLab — wrong for a
clean Windows desktop app. QuestPDF and PdfSharp were rejected because they would discard the CSS
design system. Using one language (.NET) for Core, CLI, GUI and API gives true parity, a clean
WinUI 3 desktop app and a Linux cloud container from the same engine.

## Cloud and container topology

The API project wraps Core behind a minimal HTTP surface. It registers a single
`IDocumentRenderer` as a singleton — one process, one reused headless-Chromium instance — and
applies optional bearer authentication when `CR_API_TOKEN`, `CR_API_TOKENS`,
`CR_API_TOKEN_SHA256` or `CR_API_TOKEN_SHA256S` is set (every path except `/healthz` is then
guarded).

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/healthz` | Liveness; always unauthenticated. |
| `GET` | `/v1/templates` | List id, name and description for each template. |
| `GET` | `/v1/authoring-templates` | List blank authoring templates. |
| `GET` | `/v1/authoring-templates/{id}/form` | Return the Core-owned form definition. |
| `GET` | `/v1/authoring-templates/{id}/blank` | Return blank draft JSON. |
| `POST` | `/v1/validate` | Validate a payload without rendering. |
| `POST` | `/v1/render` | Render and return an artefact descriptor (JSON + base64 PDF). |
| `POST` | `/v1/render.pdf` | Render and return the raw PDF stream. |
| `POST` | `/v1/render.multipart` | Render JSON plus uploaded image/PDF parts. |
| `POST` | `/v1/render/batch` | Render many payloads in one request. |

The request body is `{ templateId, data, density? }`, where `density` is `auto` (default) `|`
`normal` `|` `compact` `|` `ultra`. `RenderValidationException` is mapped to a `400` with
`{ error: "validation_failed", details: [...] }`.

```
        HTTP client / connector
                 │
                 ▼
   ┌──────────────────────────────────────────────┐
   │  Container (any host, e.g. Cloud Run)          │
   │                                                │
   │  base image: mcr.microsoft.com/playwright/     │
   │              dotnet:v1.49.0-jammy              │
   │   • bundled Chromium + native deps             │
   │   • + fonts-liberation, fonts-dejavu-core      │
   │                                                │
   │  ASPNETCORE_URLS=http://+:8080  (EXPOSE 8080)  │
   │                                                │
   │  CollisionRenderer.Api.dll                     │
   │     └── singleton IDocumentRenderer            │
   │           └── ChromiumPdfEngine (reused)       │
   └──────────────────────────────────────────────┘
```

The workspace `Dockerfile` is multi-stage: from the Pegasus repository root it builds
and publishes against the .NET 10 SDK image required by `global.json`, then copies the
output onto the official Playwright .NET runtime image, which ships the
matching Chromium build and its native dependencies. It adds `fonts-liberation` (and a DejaVu
fallback) so the documents' Arial-metric body copy renders with identical metrics on Linux. The
image listens on port 8080 and runs with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`. Because
Core carries no Windows dependencies, this image deploys to any container host.

## Sequence of a render call

The example below traces an API render, but the in-process path (CLI / GUI calling
`renderer.RenderAsync`) is identical from `DocumentRenderer` onward.

```
Client      API (Program)        DocumentRenderer        Validator   HtmlComposer   ChromiumPdfEngine
  │  POST /v1/render   │                  │                   │            │                │
  ├───────────────────►│                  │                   │            │                │
  │                    │ ToRenderRequest()│                   │            │                │
  │                    ├─────────────────►│                   │            │                │
  │                    │                  │ Catalog.Get(id)   │            │                │
  │                    │                  │ deserialise JSON  │            │                │
  │                    │                  ├──────────────────►│            │                │
  │                    │                  │  ValidationResult │            │                │
  │                    │                  │◄──────────────────┤            │                │
  │                    │                  │ (throw if !Ok)    │            │                │
  │                    │                  │ ResolveDensities()│            │                │
  │   ┌─ per density (auto-fit loop) ─────┤                   │            │                │
  │   │                │                  │ Compose(desc,model,density)    │                │
  │   │                │                  ├───────────────────────────────►│                │
  │   │                │                  │  ComposedDocument (HTML+furniture)               │
  │   │                │                  │◄───────────────────────────────┤                │
  │   │                │                  │ RenderHtmlToPdfAsync(html, page)│                │
  │   │                │                  ├────────────────────────────────────────────────►│
  │   │                │                  │                 PDF bytes                        │
  │   │                │                  │◄────────────────────────────────────────────────┤
  │   │                │                  │ CountPages(pdf) ──────────────────────────────► │
  │   │                │                  │ within target? stop : next density               │
  │   └────────────────┤                  │                   │            │                │
  │                    │                  │ Sha256, file name, base64?     │                │
  │                    │   RenderResult   │                   │            │                │
  │                    │◄─────────────────┤                   │            │                │
  │  200 (descriptor   │                  │                   │            │                │
  │   or raw PDF)      │                  │                   │            │                │
  │◄───────────────────┤                  │                   │            │                │
```

## Extension points

The architecture is designed so the common changes do not touch the engine.

- **Add a template.** Add a model record under `Models`, a `.scriban` body and (if needed) a
  matching CSS block in `report.css`, register a `TemplateDescriptor` in `TemplateCatalog`, add a
  sample JSON, and extend the `HtmlComposer` switch and `PayloadValidator` switch for the new
  model. No change to `DocumentRenderer` or `IPdfEngine` is required.
- **Add an expert-report block type.** Add a case in `HtmlComposer.BlockCtx`, the corresponding
  Scriban markup, and the type name to `PayloadValidator.KnownBlockTypes`.
- **Swap the PDF backend.** Implement `IPdfEngine` and pass it to
  `CollisionRendererFactory.CreateRenderer(engine)`. The pipeline, page furniture and auto-fit
  loop are unchanged; the test project's `FakePdfEngine` is the reference example.
- **Change density behaviour.** A template opts in to auto-fit by setting `DensityProfile =
  FitToPages` and a `FitTargetPages` value on its descriptor; the ladder and stop condition live
  entirely in `DocumentRenderer`.
- **Add a host.** Build the renderer through `CollisionRendererFactory.CreateRenderer()` and the
  catalog through `CollisionRendererFactory.Catalog`. Any new host inherits identical features by
  construction.
- **Bundle a signature.** Add `design/brand/signatures/{key}.png` at the Pegasus
  repository root; the Core project embeds it and `BrandAssets.SignatureDataUri`
  resolves the key to a data URI.

## Design constraints

- British English throughout; documents are CPR-compliant and go to courts, solicitors and
  insurers.
- No "AI" theming anywhere — no sparkle or magic icons, no emoji, no decorative gradients. The
  tone is calm and factual.
- Scriban security advisories (NU1901–1904) are accepted and suppressed: templates are first-party
  embedded artefacts, never authored by end users at runtime, and all data is passed as
  HTML-encoded values, never compiled.
- Local reference folders (`documentexamples/`, `stylexamples/`,
  `collision-engineers-design-dev/`, `report-renderer/`) are not product source. The first two
  contain PII, all four are git-ignored if present, and none should be committed.
