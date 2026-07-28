# Collision Renderer — Delivery Plan

This document records the delivery plan for Collision Renderer across the software
lifecycle: what has been built, what is in progress, and what remains in the backlog.
Status is recorded honestly against the code in this repository, not against an
idealised roadmap.

Collision Renderer produces Collision Engineers Ltd's branded PDF documents — vehicle
valuation reports, advert evidence packs, fee notes and expert reports — in one
consistent house style. Output goes to courts, solicitors and insurers and is
CPR-compliant. British English is used throughout.

## How to read this document

Each phase carries a status:

| Status | Meaning |
| --- | --- |
| Done | Implemented and exercised by tests or by hand in this repository. |
| In progress | Partially implemented; specific gaps are named. |
| Planned | Not started; scoped in the backlog. |

The phases reflect the order in which the system was built, not a fixed schedule.

## Phase summary

| Phase | Scope | Status |
| --- | --- | --- |
| 0 | Discovery and architecture decisions | Done |
| 1 | Core rendering engine (`CollisionRenderer.Core`) | Done |
| 2 | Command-line client (`CollisionRenderer.Cli`) | Done |
| 3 | Cloud API and container (`CollisionRenderer.Api`, `Dockerfile`) | Done |
| 4 | WinUI 3 desktop application (`CollisionRenderer.Gui`) | Done |
| 5 | Tests and validation (`CollisionRenderer.Core.Tests`) | Done |
| 6 | Reference-backed authoring, uploads, batch and visual regression | Done |

## Solution layout

The repository is a single .NET 8 solution. The solution file is
`CollisionRenderer.sln`.

```
CollisionRenderer.sln
src/
  CollisionRenderer.Core   net8.0          class library — the rendering engine
  CollisionRenderer.Cli    net8.0          console client (assembly: collisionrenderer)
  CollisionRenderer.Api    net8.0          ASP.NET Core minimal API
  CollisionRenderer.Gui    net8.0-windows  WinUI 3 / Windows App SDK desktop client
tests/
  CollisionRenderer.Core.Tests             xUnit test project
Dockerfile                                 multi-stage image for the API
Directory.Build.props                      shared build settings (Version 0.1.0)
```

`CollisionRenderer.sln` lists all five projects (Core, Cli, Api, Gui and Tests).

Every host builds its renderer through one composition root,
`CollisionRendererFactory.CreateRenderer()` (and `CollisionRendererFactory.Catalog`),
so the CLI, GUI and API share an identical pipeline. Feature parity holds by
construction rather than by repeated implementation.

---

## Phase 0 — Discovery and architecture

Status: Done

Goal: settle the stack before writing the engine, given two existing inputs — the
`collision-engineers-design` CSS-native design system, and the prior
`report-renderer` (Python / WeasyPrint) whose preferred outputs set the visual bar.

Decisions taken:

- Render via headless Chromium (`Microsoft.Playwright`) over the brand's own CSS.
  The design system is CSS-native and the preferred sample outputs were produced by
  an HTML/CSS renderer, so reusing the CSS through Chromium gives exact fidelity and
  makes new templates cheap to add.
- Reject WeasyPrint for the desktop product. It needs fragile GTK/Pango native
  libraries on Windows; the prior code had to hunt for MSYS2 DLLs and fall back to
  ReportLab, which is unsuitable for a clean Windows desktop application.
- Reject QuestPDF / PdfSharp. Both would discard the CSS design system.
- One language (.NET) for Core, CLI, GUI and API, giving true parity and both a WinUI 3
  desktop application and a Linux cloud container from the same engine.

Architecture Decision Records live under `docs/adr`. The directory is in place; the
records referenced by the build (for example the Scriban suppression rationale noted
in `Directory.Build.props`) are to be written up there.

---

## Phase 1 — Core rendering engine

Status: Done

`CollisionRenderer.Core` (`net8.0`) is the single source of truth. It converts typed
C# document models to HTML via Scriban templates and the brand CSS, then to PDF via
headless Chromium. Templates, the stylesheet, the logo and signatures are embedded
resources, so the engine renders identically from a CLI, a desktop app or a Linux
container. It has no Windows-only dependencies.

Package references: `Scriban` 5.12.1, `Microsoft.Playwright` 1.49.0.

### Public surface

- `ITemplateCatalog` (`CollisionRendererFactory.Catalog`): `List()` returning
  `TemplateDescriptor { Id, Name, Description, ... }`, `Get(id)`, and `TryGet(id, out)`.
- `IAuthoringTemplateCatalog` (`CollisionRendererFactory.AuthoringCatalog`): lists the blank
  reference-backed authoring templates, returns Core-owned form definitions, and generates blank
  or overwriteable starter draft JSON for each selectable document type.
- `IDocumentRenderer` (`CollisionRendererFactory.CreateRenderer(IPdfEngine? engine = null)`):
  `RenderAsync(RenderRequest { TemplateId, Json, Options })` returning
  `RenderResult { Pdf (byte[]), PageCount, Sha256, Density, EngineVersion,
  SuggestedFileName, Warnings, Base64? }`. The renderer is `IAsyncDisposable`.
- `RenderOptions { Fit: DensityFit (Auto | Fixed), Density: Density (Normal | Compact |
  UltraCompact), IncludeBase64, Base64Limit }`.
- `PayloadValidator.Validate(id, model)` returning
  `ValidationResult { Ok, Errors, Warnings }`; `RenderValidationException` carries
  `Errors`.
- `IPdfEngine` is swappable. `ChromiumPdfEngine` is the default; tests substitute a
  `FakePdfEngine`.

### Render templates (11 built in)

| Id | Document | Notes |
| --- | --- | --- |
| `market-valuation-evidence` | Retail pre-accident value | Comparable advert table, value box, signature. Density profile fits to a 1-page target. |
| `advert-evidence-pack` | Comparable advert reference table | Linked references plus optional uploaded screenshot/PDF captures. Captured PDFs are appended after the generated pack. |
| `fee-note` | VAT fee note / invoice | Bill-to, line items, subtotal / VAT / total, payment, VAT number in footer. |
| `expert-report` | Letter-style report | Flexible: Total Loss, Addendum, Diminution Rebuttal, Part 35, Roadworthy. Built from content blocks: paragraph, bullets, datatable, keyvalue, evidencetable, valuebox, mediarow. |
| `repairable-contract-repair-report` | Repairable / Contract Repair Report | First-class id using the expert-report body model with a fixed authoring schema and synthetic sample. |
| `total-loss-report` | Total Loss Report | First-class id using the expert-report body model with a fixed authoring schema and synthetic sample. |
| `addendum-report` | Addendum Report | First-class id using the expert-report body model with a fixed authoring schema and synthetic sample. |
| `diminution-rebuttal` | Diminution Rebuttal | First-class id using the expert-report body model with a fixed authoring schema and synthetic sample. |
| `roadworthy-criminal-report` | Roadworthy / Criminal Report | First-class id using the expert-report body model with a fixed authoring schema and synthetic sample. |
| `part-35-response` | Part 35 Responses | First-class id using the expert-report body model with a question/answer authoring schema and synthetic sample. |
| `response-letter` | Response Letter | First-class id using the expert-report body model with a fixed authoring schema and synthetic sample. |

### Authoring templates

The GUI, CLI and API expose a separate authoring catalogue for blank forms:

`repairable-contract-repair-report`, `total-loss-report`, `market-valuation-evidence`,
`advert-evidence-pack`, `addendum-report`, `diminution-rebuttal`,
`roadworthy-criminal-report`, `part-35-response`, `response-letter`, `fee-note` and the advanced
`expert-report`.

Each authoring template has a Core-owned `DocumentFormDefinition`, a blank payload factory, an
attachment policy and a render-template mapping. Hosts render that form schema; they do not invent
template-specific rules.

Templates are Scriban bodies plus a C#-built letterhead shell. Adding a template
means adding a model record, a `.scriban` body, a `TemplateDescriptor` in
`TemplateCatalog`, and a sample JSON. No engine change is required.

### Design system

The canonical stylesheet is
`src/CollisionRenderer.Core/Assets/templates/report.css`. It carries a data register
at 8.8pt for valuation, evidence and fee documents, and a letter register at 10pt for
expert reports. The palette is documents red `#C80A32`, warm charcoal `#2C2A27`, grey
label cells `#F2F2F2`, zebra `#F5F5F5` and grid `#BEBEBE`; body type is Arial /
Helvetica on A4. Every page carries the master gear-"C" logo letterhead, an
Our / Your Ref / Date block, a centred uppercase title, uppercase section headings with
a red rule, and a running footer (`Collision Engineers Ltd | www.CollisionEngineers.co.uk
| engineers@…` with `— n of N —`); fee notes swap the email for the VAT number.

### Robustness

Long documents must not garble the layout. This is handled by `@page` A4 with Chromium
running header/footer templates repeated every page, `thead { display: table-header-group }`
so table headers repeat across pages, and `break-inside: avoid` on rows, value boxes and
media rows so blocks do not split. Density auto-fit renders Normal, then Compact, then
Ultra-compact for fit-to-page templates until the page target is met, measured by counting
PDF pages. All payload text is HTML-encoded; helpers format currency (`£12,500.00`),
mileage, year and vehicle history. Upload paths are validated before render: missing local files
fail validation, supported image uploads are PNG/JPEG/WebP, advert evidence PDFs must be local PDF
paths or `data:application/pdf` base64 values, and oversized attachments warn before Chromium is
started.

Validated behaviours: a 36-row valuation flows to 3 pages with repeating header and
footer and no garbling; the sample valuation auto-fits to Compact to stay on one page.

---

## Phase 2 — Command-line client

Status: Done

`CollisionRenderer.Cli` (`net8.0`, assembly name `collisionrenderer`) is a thin client
over Core. Every command wraps the shared pipeline, so the CLI cannot drift from the
other hosts.

### Commands

```
collisionrenderer list
collisionrenderer forms list
collisionrenderer forms blank  --template <authoring-id> [--out draft.json]
collisionrenderer forms schema --template <authoring-id> [--out schema.json]
collisionrenderer forms starter --template <id> [--out f.json]
collisionrenderer validate --template <id> --data f.json
collisionrenderer render   --template <id> --data f.json [--out f.pdf]
                           [--density auto|normal|compact|ultra] [--open]
collisionrenderer batch    --manifest batch.json [--out folder]
collisionrenderer install-browser
collisionrenderer version
```

`forms` exposes the same blank authoring templates used by the GUI and API. `--data` accepts a file
path or `-` to read JSON from stdin. `render` writes `<REG>_<type>.pdf` in the current folder when
`--out` is omitted, and reports pages, density, SHA-256 and engine version. `batch` renders a
manifest of items with `templateId`, either `data` or `dataPath`, optional `density`, and optional
`out`. Exit codes: `0` success, `2` validation failure, `1` other errors. `install-browser`
downloads Chromium (roughly 90MB) for first-time setup.

---

## Phase 3 — Cloud API and container

Status: Done

`CollisionRenderer.Api` (`net8.0`, ASP.NET Core minimal API) wraps Core as a service.
It registers a single `IDocumentRenderer` for the process (reusing one headless-Chromium
instance) and serialises with camelCase and string enums.

### Endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/healthz` | Liveness check (unauthenticated). |
| GET | `/v1/templates` | List templates (`Id`, `Name`, `Description`). |
| GET | `/v1/authoring-templates` | List selectable blank authoring templates. |
| GET | `/v1/authoring-templates/{id}/form` | Return the Core-owned form definition. |
| GET | `/v1/authoring-templates/{id}/blank` | Return the blank draft payload. |
| POST | `/v1/validate` | Validate a payload without rendering. |
| POST | `/v1/render` | Artifact descriptor with a base64 PDF. |
| POST | `/v1/render.pdf` | Raw PDF stream. |
| POST | `/v1/render.multipart` | Multipart render with uploaded image/PDF parts named by target model path. |
| POST | `/v1/render/batch` | Batch render, returning one artifact descriptor per item. |

The request body is `{ templateId, data: { … }, density?: "auto|normal|compact|ultra" }`.
`/v1/render.multipart` accepts `multipart/form-data` with `templateId`, optional `density`, `data`
JSON, and file fields named by the payload path they fill, for example
`adverts[0].screenshotPath`. Optional bearer authentication is enabled by setting `CR_API_TOKEN`,
`CR_API_TOKENS`, `CR_API_TOKEN_SHA256` or `CR_API_TOKEN_SHA256S`; when any are set, every path
except `/healthz` requires `Authorization: Bearer <token>`. Token checks are SHA-256 based and
constant-time.

### Container

The `Dockerfile` at the repository root is multi-stage. The build stage uses
`mcr.microsoft.com/dotnet/sdk:8.0`; the runtime stage uses
`mcr.microsoft.com/playwright/dotnet:v1.49.0-jammy`, which bundles the matching Chromium
build and its native dependencies. The image adds `fonts-liberation` and
`fonts-dejavu-core` so Arial-metric body text renders with identical metrics on Linux,
listens on port 8080, and runs `dotnet CollisionRenderer.Api.dll`. Because Core has no
Windows dependencies, the image deploys to any container host (for example Cloud Run).

---

## Phase 4 — WinUI 3 desktop application

Status: Done

`CollisionRenderer.Gui` (`net8.0-windows10.0.19041.0`, WinUI 3 / Windows App SDK) is the
desktop client for non-technical users: pick a reference-backed blank template, fill in labelled
fields, attach images/evidence where the form calls for them, choose a density, render, preview in
WebView2, and save. It hosts Core in-process and
runs unpackaged and self-contained (`WindowsPackageType` `None`, `WindowsAppSDKSelfContained`
`true`, `SelfContained` `true`), so no machine-wide Windows App SDK runtime install is
required. Package references include `Microsoft.WindowsAppSDK` 2.2.0 and
`Microsoft.Playwright` 1.49.0, plus a project reference to Core.

> The Windows App SDK is pinned to **2.2.0** rather than 1.6/1.7: under this machine's .NET 10
> SDK, the 1.6/1.7 XAML markup compiler (a net472 tool) fails silently, whereas 2.2.0's net6
> compiler builds the `net8.0-windows` target cleanly.

### Delivered

- `MainPage` with the full document UI: a template-picker rail (the authoring catalog from
  `CollisionRendererFactory.AuthoringCatalog`), a generated form tab, an advanced JSON tab, a PDF
  preview tab, New blank / Open draft / Save draft / Batch commands, a density selector, a red
  "Render document" command, and a status strip showing file name, SHA-256, page count, density and
  warnings.
- Schema-driven form controls for text, multiline text, dates, money/number entry, checkboxes,
  selects, fixed tables, repeaters, Part 35 question/answer rows, signature selection and image/PDF
  upload slots.
- Upload slots use local file paths by default, show thumbnails for selected images and file badges
  for selected PDFs, and rely on Core validation before render.
- `MainViewModel` holding the catalog, density presets, editable payload and busy/status
  state, with a `RenderAsync` that calls the shared Core pipeline off the UI thread.
- WebView2 PDF preview — the rendered PDF is written to a temp file and shown inline.
- Save As / Open via Windows file pickers (default name = `RenderResult.SuggestedFileName`).
- Validation failures (`RenderValidationException.Errors`) surfaced both in a summary info bar and,
  where the error includes a model path, under the matching generated form field.
- A missing-Chromium first-run path that offers to install the engine off the UI thread.
- Batch rendering from the same manifest shape used by the CLI.
- Brand resources (`Brand/BrandResources.xaml`), supporting models (`TemplateItem`,
  `DensityOption`), and UI-automation validation script (`ui-tests.ps1`, with generated output
  under ignored `artifacts/gui-ui-tests/`).
- Added to `CollisionRenderer.sln`; the full solution builds with zero errors. The guided form
  flow is verified by UI automation (25/25 assertions), including generated form fields, draft
  controls, JSON diagnostics, minimum required-field entry, a real render and the PDF preview.

No "AI" theming is used anywhere: no sparkle or magic icons, no emoji, no decorative
gradients. The tone is calm and factual.

---

## Phase 5 — Tests and validation

Status: Done

`tests/CollisionRenderer.Core.Tests` (xUnit) holds 57 tests, including real-Chromium
integration renders. The suite splits across `CoreTests.cs`, `IntegrationTests.cs` and a
`FakePdfEngine.cs` that lets unit tests exercise the pipeline without launching a browser,
through the swappable `IPdfEngine`. The API exposes `public partial class Program` so
`WebApplicationFactory`-based integration tests can reference the entry point.

Validated outcomes include the multi-page 36-row valuation render, density auto-fit, every render
template sample, every authoring template mapping and blank payload, custom signature file
resolution, upload validation, captured screenshot rendering and captured advert PDF append.

Visual regression tooling lives at `scripts/visual-regression.ps1`. It renders all synthetic
samples, rasterises PDFs through Poppler `pdftoppm`, and compares candidate page PNGs against
approved PNGs under ignored `artifacts/visual-regression/approved`. It also supports a local
`-ReferenceMap` JSON mode for comparing local payload renders against private reference PDFs in
`collisionrenderer-reference-material` without committing those PDFs or exact filenames.

Run with:

```
dotnet build CollisionRenderer.sln -c Release
dotnet run --project src/CollisionRenderer.Cli -- install-browser
dotnet test
```

The local SDK is .NET 10; all projects target `net8.0` (the Gui targets
`net8.0-windows`). Scriban security advisories (NU1901–1904) are suppressed in
`Directory.Build.props`: templates are first-party embedded artifacts, never authored by
end users at run time, and all data is HTML-encoded and passed as values, never compiled.

---

## Former backlog delivered

Status: Done

The previously listed forward backlog has been implemented in this repository.

| Item | Delivered shape |
| --- | --- |
| Form-template authoring workspace | GUI defaults to Core-owned blank authoring forms with labelled fields, repeaters, tables, signatures and upload slots. JSON remains only as an advanced diagnostic tab. |
| Golden-image visual regression tests | `scripts/visual-regression.ps1` renders/rasterises samples and supports ignored local reference-map comparisons against private PDFs. |
| More templates | Roadworthy, addendum, total-loss, repairable/contract-repair, diminution, Part 35 and response-letter ids are first-class render catalogue entries with synthetic samples. |
| Signature management UI | GUI exposes bundled engineer selection and custom signature image upload without rebuilding Core. |
| Batch rendering | CLI, API and GUI all render batch manifests through the shared Core renderer. |
| Auth hardening | API accepts raw token sets and SHA-256 token sets, with constant-time bearer-token comparison. |

The companion implementation plan is `docs/FORM-TEMPLATE-AUTHORING-PLAN.md`; it records the
reference-material boundary and the exact non-sensitive reference files used to drive this work.

## Constraints carried through every phase

- British English throughout; documents are CPR-compliant.
- No "AI" theming anywhere — no sparkle or magic icons, no emoji, no decorative
  gradients. Calm, factual, engineering tone.
- Local reference folders (`documentexamples/`, `stylexamples/`,
  `collision-engineers-design-dev/`, `report-renderer/`) are not product source. The first two
  hold real customer data (PII); all four are git-ignored if present and are never committed.
- One shared engine. Any feature that touches rendering lands in Core so the CLI, GUI and
  API stay in parity by construction.
