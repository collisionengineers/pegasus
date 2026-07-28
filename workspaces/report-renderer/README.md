# Collision Renderer

Collision Renderer produces Collision Engineers' branded PDF documents — vehicle valuation
reports, advert evidence packs, fee notes and expert reports — in one consistent house style
every time. Collision Engineers Ltd is a UK independent automotive engineering and
expert-witness firm whose documents go to courts, solicitors and insurers and must be
CPR-compliant, so the output has to be predictable, accurate and on-brand on every page. A
single rendering engine sits behind a Windows desktop app, a command-line tool and a cloud
API, which means each host produces the same document, identically styled, from the same data.

## Features

- One shared rendering engine (`CollisionRenderer.Core`) is the single source of truth; the
  CLI, desktop app and API are thin clients over it, so they have identical feature parity by
  construction.
- Twelve built-in document templates with Core-generated blank and starter drafts (see the table below).
- Typed C# document models rendered to HTML via Scriban templates and the brand CSS design
  system, then to PDF via headless Chromium (Microsoft.Playwright).
- Self-contained Core: templates, stylesheet, logo and signatures are embedded resources, with
  no Windows-only dependencies, so the engine also runs in a Linux container.
- Auto-fit density for fit-to-page templates: render Normal, then Compact, then Ultra-compact
  until the page target is met.
- Multi-page robustness: repeating table headers, repeating running header/footer with page
  numbers, and no split rows or value blocks across page breaks.
- Payload validation before rendering, with clear errors and warnings.
- All payload text is HTML-encoded; currency, mileage, year and vehicle-history values are
  formatted by shared helpers.
- British English throughout. No decorative theming — calm, factual, engineering tone.

## Quick start

Prerequisites: the .NET SDK pinned in `global.json` (the projects target `net8.0`; the desktop
app targets `net8.0-windows` and needs the Windows App SDK runtime).

```sh
# Build the solution
dotnet build CollisionRenderer.sln -c Release

# First-time setup: download the Chromium engine (~90 MB)
dotnet run --project src/CollisionRenderer.Cli -- install-browser

# Generate a starter draft for a template, then render it to PDF
dotnet run --project src/CollisionRenderer.Cli -- forms starter --template fee-note --out fee.json
dotnet run --project src/CollisionRenderer.Cli -- render --template fee-note --data fee.json --out fee.pdf
```

The CLI assembly is named `collisionrenderer`. Once built or installed, the commands are:

```
collisionrenderer list
collisionrenderer forms starter --template <id> [--out <file.json>]
collisionrenderer validate --template <id> --data <file.json>
collisionrenderer render   --template <id> --data <file.json> [--out <file.pdf>]
                           [--density auto|normal|compact|ultra] [--open]
collisionrenderer install-browser
collisionrenderer version
```

`--data` accepts a file path or `-` to read JSON from stdin. `--density` defaults to `auto`.
When `--out` is omitted, `render` writes `<REG>_<type>.pdf` to the current folder.

## MCP server (.mcpb)

`src/CollisionRenderer.Mcp` exposes the renderer to Claude Desktop as
`collisionrenderer-mcp`. It registers seven tools: `render_health`, `list_templates`,
`validate`, `render`, `render_valuation_outputs`, `open_valuation_output`, and
`install_browser`.

Build the Windows stdio bundle with:

```sh
pwsh src/CollisionRenderer.Mcp/build-mcpb.ps1
```

The bundle is written to `dist/collisionrenderer-mcp-<version>.mcpb`. Rendered
artifacts are written under `%LOCALAPPDATA%\CollisionRenderer\output` and returned
as `file://` artifact descriptors.

Version rule: `src/CollisionRenderer.Mcp/manifest.json` and
`Directory.Build.props` `<Version>` must move together. The manifest version controls
the `.mcpb` filename and the Claude org-directory update; the assembly version controls
what the running server reports in logs.

Publish by replacing the `collisionrenderer-mcp` entry in the claude.ai org extension
directory with the newly versioned `.mcpb`. Desktop clients update when the version
bumps; never republish different bits under the same version.

Run the tests, the API and the desktop app:

```sh
# Tests (xUnit; includes real-Chromium integration renders)
dotnet test

# Cloud API (ASP.NET Core minimal API)
dotnet run --project src/CollisionRenderer.Api
# or build the container image
docker build -t collisionrenderer-api .

# Windows desktop app (WinUI 3; needs the Windows App SDK runtime)
dotnet run --project src/CollisionRenderer.Gui
```

## Repository layout

```
collisionrenderer/
├── CollisionRenderer.sln           Solution (Core, Cli, Api, Gui, Tests)
├── Directory.Build.props           Shared build settings and product metadata
├── global.json                     Pinned .NET SDK
├── Dockerfile                      Multi-stage image for the cloud API
├── docs/
│   └── adr/                        Architecture decision records
├── src/
│   ├── CollisionRenderer.Core/     net8.0 class library — the rendering engine
│   │   ├── Assets/                 Embedded templates, report.css, brand logo and signatures, sample JSON
│   │   ├── CollisionRendererFactory.cs   Composition root (CreateRenderer, Catalog)
│   │   ├── Contracts.cs            RenderRequest/RenderResult/RenderOptions and related types
│   │   ├── TemplateCatalog.cs      The four template descriptors
│   │   ├── DocumentRenderer.cs     IDocumentRenderer pipeline
│   │   ├── Models/                 Typed C# document models
│   │   ├── Templating/             HTML composition
│   │   ├── Rendering/              Chromium PDF engine and page counter
│   │   └── Validators.cs           PayloadValidator
│   ├── CollisionRenderer.Cli/      net8.0 console — thin client (assembly: collisionrenderer)
│   ├── CollisionRenderer.Api/      net8.0 ASP.NET Core minimal API
│   └── CollisionRenderer.Gui/      net8.0-windows WinUI 3 desktop app (WebView2 preview)
├── tests/
│   └── CollisionRenderer.Core.Tests/   xUnit tests (57 tests, incl. integration renders)
└── scripts/
    ├── render-starters.ps1         Render generated starters to ignored artifacts/
    └── visual-regression.ps1       Rasterise/compare rendered PDFs under ignored artifacts/
```

## Templates

The catalogue is defined in `src/CollisionRenderer.Core/TemplateCatalog.cs`. Each template is a
Scriban body plus a C#-built letterhead shell; the authoring catalogue generates blank and starter drafts.

| Id | Name | Purpose | Auto-fit |
| --- | --- | --- | --- |
| `market-valuation-evidence` | Market Valuation Evidence | Retail pre-accident value evidenced by a comparable advert table, value box and signature. | Fit to 1 page |
| `advert-evidence-pack` | Advert Evidence Pack | Linked comparable advert reference table accompanying the valuation evidence. | None |
| `fee-note` | Fee Note | VAT fee note / invoice: bill-to, line items, subtotal / VAT / total, payment terms, VAT number in the footer. | None |
| `expert-report` | Expert Report | Flexible letter-style report (Total Loss, Addendum, Diminution Rebuttal, Part 35, Roadworthy) built from content blocks. | None |
| `repairable-contract-repair-report` | Repairable / Contract Repair Report | Fixed authoring schema over the expert-report body model. | None |
| `total-loss-report` | Total Loss Report | Fixed authoring schema over the expert-report body model. | None |
| `addendum-report` | Addendum Report | Fixed authoring schema over the expert-report body model. | None |
| `diminution-rebuttal` | Diminution Rebuttal | Fixed authoring schema over the expert-report body model. | None |
| `roadworthy-criminal-report` | Roadworthy / Criminal Report | Fixed authoring schema over the expert-report body model. | None |
| `part-35-response` | Part 35 Responses | Question/answer authoring schema over the expert-report body model. | None |
| `response-letter` | Response Letter | Fixed authoring schema over the expert-report body model. | None |

The expert report is assembled from content blocks: `paragraph`, `bullets`, `datatable`,
`keyvalue`, `evidencetable`, `valuebox` and `mediarow`.

Adding a template requires no engine change: add a model record, a `.scriban` body, a
`TemplateDescriptor` in `TemplateCatalog`, and a Core-owned authoring form and blank-draft factory.

## Cloud API

The API wraps Core and exposes:

| Method | Path | Returns |
| --- | --- | --- |
| `GET` | `/healthz` | Service health |
| `GET` | `/v1/templates` | Template list (id, name, description) |
| `GET` | `/v1/authoring-templates` | Blank authoring template catalogue |
| `GET` | `/v1/authoring-templates/{id}/form` | Core-owned form schema |
| `GET` | `/v1/authoring-templates/{id}/blank` | Blank draft JSON |
| `POST` | `/v1/validate` | Validation result (ok, errors, warnings) |
| `POST` | `/v1/render` | Artifact descriptor with a base64 PDF |
| `POST` | `/v1/render.pdf` | The raw PDF stream |
| `POST` | `/v1/render.multipart` | Render JSON plus uploaded image/PDF parts |
| `POST` | `/v1/render/batch` | Render many payloads in one request |

Optional bearer authentication is enabled by setting `CR_API_TOKEN`, `CR_API_TOKENS`,
`CR_API_TOKEN_SHA256` or `CR_API_TOKEN_SHA256S`; when set, every path except `/healthz` requires
`Authorization: Bearer <token>`. The Dockerfile builds on the Playwright .NET image (which bundles
Chromium and its native dependencies) and adds Liberation fonts for Arial-metric body text, so the
image deploys to any container host.

## Staying consistent on long documents

The brand design system is CSS-native and the preferred sample outputs were produced by an
HTML/CSS renderer, so Collision Renderer reuses that CSS through headless Chromium for exact
fidelity. The canonical stylesheet is
`src/CollisionRenderer.Core/Assets/templates/report.css`: a data register at 8.8pt for
valuation, evidence and fee documents, and a letter register at 10pt for expert reports. Every
page carries the gear-"C" logo letterhead with an Our / Your Ref / Date block, a centred
uppercase title, uppercase section headings underlined by a red rule, and a running footer with
the Collision Engineers strapline and an `— n of N —` page marker (fee notes swap the email for
the VAT number).

Long documents keep that look through paged-media rules rather than chance:

- `@page A4` with Chromium running header and footer templates that repeat on every page with
  page numbers.
- `thead { display: table-header-group }` so table headers repeat across page breaks.
- `break-inside: avoid` on table rows, value boxes and media rows, so blocks are never split.
- Density auto-fit for fit-to-page templates: the engine renders Normal, then Compact, then
  Ultra-compact, counting the PDF pages until it meets the target. A 36-row valuation flows to
  three pages with a repeating header and footer and no garbling; the generated valuation starter
  auto-fits to Compact to stay on one page.

## Documentation

- `docs/adr/` — architecture decision records, including the rationale for the Chromium /
  Playwright stack and the handling of Scriban advisories.
- `CollisionRendererFactory.cs` — the composition root used by every host; start here to follow
  how a render request flows through Core.
- `scripts/render-starters.ps1` — renders generated starter PDFs into `artifacts/rendered-starters/`,
  which is ignored and can be regenerated at any time.
