# Collision Renderer — Product Requirements Document

| | |
|---|---|
| Product | Collision Renderer |
| Owner | Collision Engineers Ltd |
| Status | Built (documented as delivered) |
| Solution | `CollisionRenderer.sln` (.NET 8) |

## 1. Problem

Collision Engineers Ltd is a UK independent automotive engineering and expert-witness
firm. Its documents — vehicle valuation reports, advert evidence packs, fee notes and
expert reports — are sent to courts, solicitors and insurers, and must be CPR-compliant.
The reader's confidence in the evidence depends in part on the documents looking correct
and consistent: the same letterhead, the same typography, the same red house style, on
every page of every document, regardless of who produced it or which machine they used.

The firm needs a way to produce these branded PDFs that:

- Looks identical every time, with no per-author drift in styling.
- Can be operated by non-technical staff on a Windows desktop, and also driven by
  automation or a cloud service for higher-volume or integration work.
- Holds the layout together on long, multi-page documents (repeating headers and
  footers, table headers that repeat, rows that do not split across a page break).
- Is straightforward to extend with new document types without re-engineering the
  rendering core.
- Carries no "AI" styling or decorative theming — the tone is calm and factual,
  appropriate for legal and insurance correspondence.

A prior renderer (Python / WeasyPrint) produced the preferred sample outputs but depended
on fragile native libraries (GTK/Pango) that are awkward on Windows; the prior code had to
hunt for MSYS2 DLLs and fall back to ReportLab. That is the wrong foundation for a clean
Windows desktop application.

## 2. Users

| Group | Role | Technical level |
|---|---|---|
| Collision Engineers' engineers and admin staff | Authors — produce the documents | Non-technical; use the desktop GUI |
| Courts, solicitors, insurers | Readers — receive the documents | Not users of the software; consumers of the PDF output |

The product is designed around the author. Readers never touch the software; their
requirement is met indirectly, through document quality, consistency and CPR-compliant
presentation.

## 3. Goals and non-goals

### Goals

- One shared rendering engine that produces a single, consistent house style across
  every surface (desktop, command line, cloud).
- A Windows desktop application that a non-technical author can operate end to end.
- A command-line client and a cloud API with the same document capabilities as the
  desktop, by construction.
- Faithful reproduction of the brand's existing CSS design system.
- Robust multi-page output that does not garble the layout.
- A low-friction path to add new document templates.

### Non-goals

- No "AI" theming, sparkle/magic iconography, emoji or decorative gradients anywhere
  in the product or its output.
- No end-user-authored templates at runtime; templates are first-party embedded
  artefacts.
- No reproduction of the prior WeasyPrint/ReportLab pipeline; the CSS design system is
  reused via a different, cross-platform engine.
- This document does not specify a hosting provider; the API is portable to any
  container host.

## 4. The nine requirements and how each is met

| # | Requirement | How it is met |
|---|---|---|
| 1 | Windows GUI and CLI with feature parity | Both are thin clients over `CollisionRenderer.Core`. Every host builds its renderer through the single composition root `CollisionRendererFactory.CreateRenderer()` and shares `CollisionRendererFactory.Catalog`, so parity holds by construction. The GUI (`src/CollisionRenderer.Gui`, WinUI 3 / Windows App SDK) calls Core in-process; the CLI (`src/CollisionRenderer.Cli`, assembly `collisionrenderer`) wraps the same Core APIs. |
| 2 | Cloud-portable | `CollisionRenderer.Core` has zero Windows-only dependencies, so it runs in a Linux container. `src/CollisionRenderer.Api` (ASP.NET Core minimal API) wraps Core; the repo-root `Dockerfile` builds on the official Playwright .NET runtime image (bundled Chromium and native dependencies) and adds `fonts-liberation` for Arial-metric body text. The image deploys to any container host. |
| 3 | New templates without engine changes | Adding a template means adding a model record, a `.scriban` body template, a `TemplateDescriptor` entry in `TemplateCatalog`, and a sample JSON. The rendering engine is not modified. Templates are Scriban body templates rendered into a C#-built letterhead shell. |
| 4 | Identical style every time | A single engine, a single embedded stylesheet (`Assets/templates/report.css`) and embedded brand assets (logo, signatures) are the one source of truth. Templates, stylesheet, logo and signatures are embedded resources in Core, so every surface renders from the same artefacts. |
| 5 | Multi-page robustness | Chromium paged media: `@page A4`, running header/footer templates that repeat on every page with page numbers, `thead { display: table-header-group }` so table headers repeat across pages, and `break-inside: avoid` on rows, value boxes and media rows. Validated: a 36-row valuation flows to three pages with repeating header and footer and no garbling. |
| 6 | Simple for non-technical users | The desktop GUI follows one linear path: pick a document type, load the sample or fill in the data, render, preview (WebView2), save. Each template ships a starter payload via `GetSampleJson(id)`, and `PayloadValidator` reports clear errors and warnings before rendering. |
| 7 | No AI theming | No sparkle/magic icons, no emoji, no decorative gradients. The tone is calm and factual throughout the product and its output. |
| 8 | Faithful brand fidelity | The design system is CSS-native and the preferred sample outputs were produced by an HTML/CSS renderer. Reusing that CSS through headless Chromium reproduces the brand exactly (documents red `#C80A32`, warm charcoal `#2C2A27`, grey label cells `#F2F2F2`, zebra `#F5F5F5`, grid `#BEBEBE`, Arial/Helvetica on A4). |
| 9 | Strongest stack, not a copy | One language (.NET) for Core, CLI, GUI and API gives true parity, a clean WinUI 3 desktop app and a Linux cloud container. Chromium/Playwright is cross-platform and self-contained, unlike the prior WeasyPrint native-library dependency. QuestPDF and PdfSharp were rejected because they would discard the CSS design system. |

> Requirements 1–7 are stated as the seven explicit product requirements; 8 and 9 capture
> the brand-fidelity and stack-choice requirements that sit alongside them.

## 5. Functional requirements

### 5.1 Shared core (`CollisionRenderer.Core`)

The Core is a .NET 8 class library: typed C# document models rendered to HTML via Scriban
templates plus the brand's CSS, then to PDF via headless Chromium (`Microsoft.Playwright`).
It is the single rendering entry point for the CLI, GUI and API.

Public surface:

- `ITemplateCatalog` (`CollisionRendererFactory.Catalog`): `List()` →
  `TemplateDescriptor { Id, Name, Description, ... }`, `Get(id)`, `TryGet(id, out)`,
  `GetSampleJson(id)`.
- `IDocumentRenderer` (`CollisionRendererFactory.CreateRenderer(IPdfEngine? engine = null)`):
  `RenderAsync(RenderRequest { TemplateId, Json, Options })` → `RenderResult { Pdf (byte[]),
  PageCount, Sha256, Density, EngineVersion, SuggestedFileName, Warnings, Base64? }`. It is
  `IAsyncDisposable`.
- `RenderOptions { Fit: DensityFit (Auto | Fixed), Density: Density (Normal | Compact |
  UltraCompact), IncludeBase64, Base64Limit }`.
- `PayloadValidator.Validate(id, model)` → `ValidationResult { Ok, Errors, Warnings }`;
  `RenderValidationException` carries the errors.
- `IPdfEngine` is swappable. `ChromiumPdfEngine` is the default; tests use a
  `FakePdfEngine`. The Chromium browser is launched once and reused across renders.

### 5.2 Templates

Four built-in templates:

| Id | Document | Notes |
|---|---|---|
| `market-valuation-evidence` | Retail pre-accident value | Comparable advert table, value box, signature. Density profile `FitToPages`, target one page. |
| `advert-evidence-pack` | Comparable advert reference table | Linked advert references. |
| `fee-note` | VAT fee note / invoice | Bill-to, line items, subtotal/VAT/total, payment; VAT number in the footer. |
| `expert-report` | Letter-style report | Flexible: Total Loss, Addendum, Diminution Rebuttal, Part 35, Roadworthy. Built from content blocks: `paragraph`, `bullets`, `datatable`, `keyvalue`, `evidencetable`, `valuebox`, `mediarow`. |

### 5.3 Command-line client (`collisionrenderer`)

```
collisionrenderer list
collisionrenderer sample   --template <id> [--out f.json]
collisionrenderer validate --template <id> --data f.json
collisionrenderer render   --template <id> --data f.json [--out f.pdf]
                           [--density auto|normal|compact|ultra] [--open]
collisionrenderer install-browser
collisionrenderer version
```

`--data` accepts a file path or `-` for stdin. `render` reports page count, density,
SHA-256 and engine version, and exits non-zero on validation failure.

### 5.4 Desktop GUI (`CollisionRenderer.Gui`)

WinUI 3 / Windows App SDK desktop client, Core in-process, WebView2 preview. Flow: pick a
document type, load the sample or fill in the data, render, preview, save.

### 5.5 Cloud API (`CollisionRenderer.Api`)

ASP.NET Core minimal API wrapping Core. One renderer per process (reused headless Chromium).
Optional bearer auth via raw or SHA-256 token environment variables (all paths except `/healthz`).

| Method | Path | Purpose |
|---|---|---|
| GET | `/healthz` | Liveness check |
| GET | `/v1/templates` | List templates (id, name, description) |
| GET | `/v1/templates/{id}/sample` | Starter payload for a template |
| GET | `/v1/authoring-templates` | Blank authoring template catalogue |
| GET | `/v1/authoring-templates/{id}/form` | Core-owned form schema |
| GET | `/v1/authoring-templates/{id}/blank` | Blank draft JSON |
| POST | `/v1/validate` | Validate a payload without rendering |
| POST | `/v1/render` | Render; returns an artefact descriptor with base64 PDF |
| POST | `/v1/render.pdf` | Render; returns the raw PDF stream |
| POST | `/v1/render.multipart` | Render JSON plus uploaded image/PDF parts |
| POST | `/v1/render/batch` | Render many payloads in one request |

Request body: `{ templateId, data: { ... }, density?: "auto|normal|compact|ultra" }`.

## 6. Non-functional requirements

- **Consistency by construction.** All hosts build the renderer the same way through the
  composition root, so feature parity is structural rather than maintained by hand.
- **Portability.** Core carries no Windows-only dependencies. The desktop project is
  self-contained for unpackaged launch (Windows App SDK runtime bundled).
- **Robustness on long documents.** Repeating header/footer, repeating table headers and
  no split rows or blocks, validated against a multi-page sample.
- **Density auto-fit.** Fit-to-page templates render Normal → Compact → Ultra-compact,
  measured by counting PDF pages, until the page target is reached. Validated: the sample
  valuation auto-fits to Compact to stay on one page.
- **Safety.** All payload text is HTML-encoded (no injection). Data is passed as
  HTML-encoded values, never compiled. Scriban security advisories (NU1901–1904) are
  accepted/suppressed because templates are first-party embedded artefacts, never authored
  by end users at runtime.
- **Privacy.** Local reference folders (`documentexamples/`, `stylexamples/`,
  `collision-engineers-design-dev/`, `report-renderer/`) are not product source. The first two
  hold real customer data (PII); all four are git-ignored if present and are never committed.
- **Tone.** Calm, factual, engineering presentation; British English throughout; no "AI"
  theming.

### Design system reference

- Source: `collision-engineers-design` (CSS-native) and the proven prior `report-renderer`
  output.
- Canonical stylesheet: `src/CollisionRenderer.Core/Assets/templates/report.css` — a data
  register at 8.8pt (valuation / evidence / fee) and a letter register at 10pt (expert
  reports).
- Every page: gear-"C" logo letterhead with Our/Your Ref/Date block; centred UPPERCASE
  title (red on newer outputs); UPPERCASE section headings with a red rule under them; a
  running footer (thin red rule, `Collision Engineers Ltd | www.CollisionEngineers.co.uk |
  engineers@...` and `— n of N —`). Fee notes swap the email for the VAT number.

### Build, run and test (Windows; .NET 10 SDK installed, targets net8.0)

```
dotnet build CollisionRenderer.sln -c Release
dotnet run --project src/CollisionRenderer.Cli -- install-browser   # first-time Chromium (~90MB)
dotnet test
dotnet run --project src/CollisionRenderer.Api
docker build -t collisionrenderer-api .
dotnet run --project src/CollisionRenderer.Gui                       # needs Windows App SDK runtime
```

## 7. Success metrics

- **Parity:** the CLI, GUI and API expose the same templates and produce the same output
  for the same payload, because they share the composition root.
- **Layout integrity:** multi-page documents render with repeating header/footer and
  repeating table headers and no split rows — confirmed by the 36-row → 3-page valuation.
- **Fit:** fit-to-page templates land on their page target where the content allows —
  confirmed by the sample valuation auto-fitting to Compact on one page.
- **Extensibility:** a new template can be added by data and template files alone, with no
  engine change.
- **Test coverage:** 57 tests in `tests/CollisionRenderer.Core.Tests` (xUnit), including
  real-Chromium integration renders, pass.
- **Portability:** the API image builds and runs on a container host without Windows
  dependencies.

## 8. Risks

| Risk | Mitigation |
|---|---|
| Chromium is not installed for Playwright on first use | `install-browser` CLI command and a clear engine error message that names the installer command. |
| Font metrics differ between Windows and Linux | The container image installs `fonts-liberation` (and a DejaVu fallback) for Arial-metric body copy. |
| Long or dense documents overflow the intended page count | Density auto-fit (Normal → Compact → Ultra-compact); when content still exceeds the target, the result carries a warning and renders cleanly across the extra pages rather than garbling. |
| Untrusted input in payloads | All payload text is HTML-encoded; data is never compiled into templates. |
| Accidental commit of customer PII | Reference folders are git-ignored and excluded from the repository. |
| Dependency advisories on Scriban (NU1901–1904) | Accepted/suppressed on the basis that templates are first-party embedded artefacts, never authored by end users at runtime. |
