# Development guide

This guide covers building, testing and running Collision Renderer on a developer
machine, plus the container build for the cloud API. Collision Renderer is a single
.NET 8 solution: one shared rendering engine (`CollisionRenderer.Core`) with thin
CLI, desktop (WinUI 3) and API clients over it.

All commands assume you are at the repository root unless stated otherwise.

## Prerequisites

| Requirement | Needed for | Notes |
| --- | --- | --- |
| .NET SDK (8.0 or later) | Build, test, all projects | Every project targets `net8.0` (the GUI targets `net8.0-windows10.0.19041.0`). A newer SDK such as .NET 10 builds the `net8.0` targets without change. |
| Chromium for Playwright | Any PDF render | Downloaded once with `install-browser` (roughly 90 MB). See [First-time browser install](#first-time-browser-install). |
| Windows App SDK runtime | Running the desktop GUI | The GUI project sets `WindowsAppSDKSelfContained` and `SelfContained`, so a self-contained build bundles the runtime. A machine-wide install is not required when built this way. |
| Windows 10 build 17763 or later, x64 or ARM64 | Building/running the GUI | The GUI is Windows-only (`net8.0-windows`). Core, CLI and API have no Windows dependencies. |
| Docker (optional) | Building/running the API container image | Only needed if you build the container rather than running the API directly. |

The desktop GUI uses WebView2 for its preview. WebView2 is part of current Windows
installations; install the Evergreen runtime separately if it is missing.

### Solution layout

| Project | Target | Role |
| --- | --- | --- |
| `src/CollisionRenderer.Core` | `net8.0` (class library) | The single rendering engine. Typed document models to HTML (Scriban + brand CSS) to PDF (headless Chromium via Microsoft.Playwright). Templates, stylesheet, logo and signatures are embedded resources. |
| `src/CollisionRenderer.Cli` | `net8.0` console (assembly `collisionrenderer`) | Thin command-line client over Core. |
| `src/CollisionRenderer.Api` | `net8.0` ASP.NET Core minimal API | Cloud service wrapping Core. `Dockerfile` at repo root. |
| `src/CollisionRenderer.Gui` | `net8.0-windows` (WinUI 3 / Windows App SDK) | Desktop client; in-process Core; WebView2 preview. |
| `tests/CollisionRenderer.Core.Tests` | `net8.0` (xUnit) | 57 tests, including real-Chromium integration renders. |

The solution file is `CollisionRenderer.sln` and contains Core, CLI, API, GUI and the
test project.

## Build

Build the cross-platform projects (Core, CLI, API, tests) — Windows, Linux or macOS:

```
dotnet build src/CollisionRenderer.Core -c Release
dotnet build src/CollisionRenderer.Cli -c Release
dotnet build src/CollisionRenderer.Api -c Release
dotnet build tests/CollisionRenderer.Core.Tests -c Release
```

The WinUI desktop app (`src/CollisionRenderer.Gui`) targets `net8.0-windows` and builds
on **Windows only**:

```
dotnet build src/CollisionRenderer.Gui -c Release -r win-x64
```

Building the whole solution (`dotnet build CollisionRenderer.sln -c Release`) therefore
requires Windows; on Linux/macOS build the cross-platform projects listed above.

> Scriban package security advisories (NU1901–NU1904) are accepted for this
> repository: templates are first-party embedded artefacts, never authored by end
> users at runtime, and all payload data is passed as HTML-encoded values, never
> compiled.

## First-time browser install

Any PDF render needs the Chromium build that Playwright drives. Install it once per
machine through the CLI:

```
dotnet run --project src/CollisionRenderer.Cli -- install-browser
```

This downloads the Chromium engine (roughly 90 MB) and prints `Chromium installed.`
on success. You do not need this step inside the API container — the Playwright base
image already ships Chromium (see [Container build and run](#container-build-and-run-api)).

## Test

```
dotnet test
```

The suite is xUnit (57 tests) and includes integration tests that render with real
Chromium. Run [`install-browser`](#first-time-browser-install) first, or those tests
will fail because the browser is missing.

## Run

### CLI

The CLI assembly is named `collisionrenderer`. During development run it through
`dotnet run` (note the `--` separating the run arguments from the program
arguments), or invoke the built executable directly from its `bin` folder.

```
dotnet run --project src/CollisionRenderer.Cli -- <command> [options]
```

Commands:

| Command | Purpose |
| --- | --- |
| `list` | List the available document templates. |
| `forms list` | List blank authoring templates. |
| `forms blank --template <id> [--out <file.json>]` | Print or save a blank draft payload. |
| `forms schema --template <id> [--out <file.json>]` | Print or save a Core-owned form schema. |
| `forms starter --template <id> [--out <file.json>]` | Generate an overwriteable starter draft. |
| `validate --template <id> --data <file.json>` | Check a payload without rendering. |
| `render --template <id> --data <file.json> [--out <file.pdf>] [--density <mode>] [--open]` | Render a document to PDF. |
| `batch --manifest <file.json> [--out <folder>]` | Render many manifest items. |
| `install-browser` | Download the Chromium engine (first-time setup). |
| `version` | Show the version. |

Render options:

| Option | Meaning |
| --- | --- |
| `--out`, `-o <path>` | Output PDF path. Defaults to `<REG>_<type>.pdf` (the suggested file name) in the current folder. |
| `--density <mode>` | `auto` (default), `normal`, `compact` or `ultra`. |
| `--open` | Open the PDF when finished. |
| `--data`, `-d <path\|->` | JSON payload file, or `-` to read from stdin. |

### API

Run the minimal API directly:

```
dotnet run --project src/CollisionRenderer.Api
```

The renderer is registered as a singleton, so the process launches one headless
Chromium instance and reuses it. Set `CR_API_TOKEN`, `CR_API_TOKENS`, `CR_API_TOKEN_SHA256` or
`CR_API_TOKEN_SHA256S` to require bearer authentication on every endpoint except `/healthz`;
requests must then send `Authorization: Bearer <token>`.

Endpoints:

| Method and path | Purpose |
| --- | --- |
| `GET /healthz` | Liveness check (`{ "status": "ok" }`). Never authenticated. |
| `GET /v1/templates` | List templates (`id`, `name`, `description`). |
| `GET /v1/authoring-templates` | List blank authoring templates. |
| `GET /v1/authoring-templates/{id}/form` | Return a Core-owned form schema. |
| `GET /v1/authoring-templates/{id}/blank` | Return blank draft JSON. |
| `POST /v1/validate` | Validate a payload; returns `ok`, `errors`, `warnings`. |
| `POST /v1/render` | Render to an artifact descriptor (JSON with metadata and a base64 PDF). |
| `POST /v1/render.pdf` | Render and return the raw PDF stream. |
| `POST /v1/render.multipart` | Render JSON plus uploaded image/PDF parts. |
| `POST /v1/render/batch` | Render many payloads in one request. |

The request body for `/v1/validate`, `/v1/render` and `/v1/render.pdf` is:

```json
{ "templateId": "<id>", "data": { }, "density": "auto" }
```

`density` is optional and accepts `auto` (default), `normal`, `compact` or `ultra`.

### GUI

Run the desktop application (Windows only):

```
dotnet run --project src/CollisionRenderer.Gui
```

The GUI launches unpackaged (`WindowsPackageType=None`) and is self-contained, so it
carries the Windows App SDK runtime with it. The workflow is: pick a document type,
load a sample or fill in data, render, preview in WebView2, then save. It uses Core
in-process, so it has the same templates and behaviour as the CLI and API.

## Templates and the rendering pipeline

Four templates ship in the box:

| Template id | Document |
| --- | --- |
| `market-valuation-evidence` | Retail pre-accident value with comparable advert table, value box and signature. Fits to one page. |
| `advert-evidence-pack` | Comparable advert reference table (linked). |
| `fee-note` | VAT fee note / invoice (bill-to, line items, subtotal/VAT/total, payment, VAT number in footer). |
| `expert-report` | Flexible letter-style report built from content blocks: paragraph, bullets, datatable, keyvalue, evidencetable, valuebox, mediarow. |

Every host builds its renderer through the composition root,
`CollisionRendererFactory.CreateRenderer()`, and reads templates from
`CollisionRendererFactory.Catalog`. This is what guarantees identical behaviour
across the CLI, GUI and API.

Adding a template requires no engine change: add a model record, a `.scriban` body,
a `TemplateDescriptor` in the template catalogue, and a sample JSON payload.

### How the embedded assets work

`CollisionRenderer.Core` is self-contained. The templates, stylesheet, brand logo and
expert signatures are compiled into the assembly as embedded resources, declared in
`src/CollisionRenderer.Core/CollisionRenderer.Core.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Assets\templates\**\*" />
  <EmbeddedResource Include="Assets\samples\**\*" />
  <EmbeddedResource Include="Assets\brand\logo.png" />
  <EmbeddedResource Include="Assets\brand\signatures\**\*" />
</ItemGroup>
```

The on-disk source for these assets lives under
`src/CollisionRenderer.Core/Assets/`:

- `templates/report.css` — the canonical stylesheet (a data register at 8.8pt for
  valuation/evidence/fee documents, a letter register at 10pt for expert reports).
- `templates/*.scriban` — the body templates (`market_valuation_evidence.scriban`,
  `advert_evidence_pack.scriban`, `fee_note.scriban`, `expert_report.scriban`).
- `brand/logo.png`, `brand/signatures/*.png` — letterhead logo and expert
  signatures.

At runtime the loader (`EmbeddedResources`) matches resources by their trailing path,
so code reads natural relative paths such as `templates/report.css` regardless of how
MSBuild names the manifest resources. Because everything is embedded, the engine
renders identically from the CLI, the desktop app or a Linux container; nothing is
read from a working directory.

No brand font files ship in the repo. Document body copy uses Arial /
metric-compatible faces, supplied by the operating system or, on Linux, by the
Liberation fonts installed in the container image.

## Render a generated starter

The quickest end-to-end check. First ensure Chromium is installed
([above](#first-time-browser-install)), then:

```
dotnet run --project src/CollisionRenderer.Cli -- forms starter --template market-valuation-evidence --out val.json
dotnet run --project src/CollisionRenderer.Cli -- render --template market-valuation-evidence --data val.json --out val.pdf --open
```

On success the render command prints the page count, the chosen density, the SHA-256
of the PDF and the engine version, then opens `val.pdf`. With the default `auto`
density the generated valuation starter fits to one page (it auto-fits to Compact).

To regenerate example PDFs from Core-generated starters, run:

```
scripts/render-starters.ps1
```

The script writes to `artifacts/rendered-starters/`, which is ignored and can be
deleted or regenerated at any time.

You can also generate a starter with the CLI and drive its render against the running API:

```
dotnet run --project src/CollisionRenderer.Cli -- forms starter --template market-valuation-evidence --out val.json
curl -s -X POST http://localhost:5000/v1/render.pdf \
  -H "Content-Type: application/json" \
  -d "{\"templateId\":\"market-valuation-evidence\",\"data\":$(cat val.json)}" \
  --output val.pdf
```

Adjust the host and port to match the URL printed by `dotnet run`. If `CR_API_TOKEN`
is set, add `-H "Authorization: Bearer <token>"`.

## Container build and run (API)

The `Dockerfile` at the repository root is multi-stage. It builds with the .NET 8 SDK
image and runs on the official Playwright .NET image, which bundles the matching
Chromium build and its native dependencies; the final stage also installs
`fonts-liberation` and `fonts-dejavu-core` so the documents' Arial-metric body copy
renders with the correct metrics on Linux.

Build the image:

```
docker build -t collisionrenderer-api .
```

Run it. The image sets `ASPNETCORE_URLS=http://+:8080` and exposes port 8080:

```
docker run --rm -p 8080:8080 collisionrenderer-api
```

To require bearer auth, pass the token through the environment:

```
docker run --rm -p 8080:8080 -e CR_API_TOKEN=your-secret collisionrenderer-api
```

Check it is up:

```
curl http://localhost:8080/healthz
```

The image runs on any container host. There is no separate browser-install step in
the container: the Playwright base image pre-installs browsers at
`/ms-playwright`, which the Dockerfile sets via `PLAYWRIGHT_BROWSERS_PATH`.

## Troubleshooting

### Chromium not installed

A render that has no browser available fails with a message like:

```
Chromium is not installed for Playwright. Run the bundled installer once:
  pwsh src/CollisionRenderer.Cli/bin/Debug/net8.0/playwright.ps1 install chromium
or, from any built project folder, 'playwright install chromium'.
```

Fix it by running the CLI installer once:

```
dotnet run --project src/CollisionRenderer.Cli -- install-browser
```

This is also the cause of failing integration tests on a fresh checkout. Install the
browser before running `dotnet test`. The container image does not need this step.

### Fonts on Linux

If body text renders with the wrong width or falls back to a substitute face on Linux,
the Arial-metric fonts are missing. The container handles this by installing
`fonts-liberation` (and `fonts-dejavu-core` as a fallback). When running the API
outside the provided image, install the equivalent packages, for example:

```
apt-get update && apt-get install -y --no-install-recommends fonts-liberation fonts-dejavu-core
```

Also keep globalisation enabled. The Dockerfile sets
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`; if you run in invariant-globalisation
mode, currency and number formatting (for example `£12,500.00`) will not format as
expected.

### Generated artifacts

Generated PDFs and GUI UI-test output belong under `artifacts/`. The helper scripts
write to `artifacts/rendered-starters/` and `artifacts/gui-ui-tests/`; both are ignored.
Do not commit regenerated PDFs, screenshots or UI-test JSON output.

### Reference data folders

The `documentexamples/` and `stylexamples/` folders hold real customer data
(personally identifiable information). `collision-engineers-design-dev/` and
`report-renderer/` are prior-art/design references. None of these four folders is part
of the product source or build; if present locally, they are git-ignored and must never
be committed.
