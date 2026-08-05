# Architecture

> **Design authority:** [`../../../design/README.md`](../../../design/README.md) is the root authority for shared design assets and governance. This workspace links or embeds governed assets; it does not create a competing design source.

Collision Renderer follows one principle: **one shared engine, thin hosts**. `CollisionRenderer.Core` owns document models, catalogues, form definitions, attachment policy, validation, HTML composition, density fitting and PDF production. Hosts translate their transport or UI into Core contracts and do not maintain separate render pipelines.

## Workspace boundary

This is independent, source-only, non-caller workspace code. The projects can be restored, built, tested and run locally. Their presence is not evidence of an external caller, product integration, production deployment or selected hosting provider.

## Project graph

The current solution has four shipped projects and two test projects.

| Project | Role | Important boundary |
| --- | --- | --- |
| `CollisionRenderer.Core` | Shared typed-model-to-PDF engine. | No Windows-only dependency; sole rendering source of truth. |
| `CollisionRenderer.Cli` | Command-line host, assembly `collisionrenderer`. | Maps commands and files to Core contracts. |
| `CollisionRenderer.Api` | ASP.NET Core minimal HTTP API. | Adds HTTP, multipart, batching and optional bearer authentication. |
| `CollisionRenderer.Mcp` | Standalone MCP stdio host. | Exposes selected Core operations as MCP tools and writes local artefacts. |
| `CollisionRenderer.Core.Tests` | Core, validation, composition and Chromium integration tests. | May use a fake `IPdfEngine` for deterministic pipeline tests. |
| `CollisionRenderer.Mcp.Tests` | MCP host and tool-contract tests. | Tests MCP translation without creating another renderer. |

Current direct package evidence:

- Core: PDFsharp `6.2.4`, Scriban `7.2.6`, Microsoft.Playwright `1.61.0`.
- MCP: ModelContextProtocol `1.4.0`, Microsoft.Extensions.Hosting `10.0.10`, in addition to its Core project reference.

## Composition and public contracts

All rendering starts at `CollisionRendererFactory`:

- `CollisionRendererFactory.Catalog` exposes the immutable render catalogue.
- `CollisionRendererFactory.AuthoringCatalog` exposes Core-owned forms, blank drafts, starter drafts and attachment policies.
- `CollisionRendererFactory.CreateRenderer(IPdfEngine? engine = null)` creates an `IDocumentRenderer`.

When no engine is supplied, Core creates, owns and disposes `ChromiumPdfEngine`. When a caller injects an `IPdfEngine`, the caller owns its lifetime.

Principal contracts:

| Contract | Purpose |
| --- | --- |
| `ITemplateCatalog` | Lists and resolves render-template descriptors. |
| `IAuthoringTemplateCatalog` | Lists authoring descriptors and returns forms, blanks and starters. |
| `IDocumentRenderer` | `RenderAsync(RenderRequest)` returning `RenderResult`; async-disposable. |
| `RenderRequest` | Template ID, JSON and `RenderOptions`. |
| `RenderOptions` | Auto/fixed density, selected density, optional base64 inclusion and limit. |
| `RenderResult` | PDF bytes, page count, SHA-256, used density, engine version, suggested file name, warnings and optional base64. |
| `IPayloadValidator` | Applies model and per-template policy validation. |
| `RenderValidationException` | One validation-failure contract carrying error details. |
| `IPdfEngine` | Swappable HTML-to-PDF and page-counting seam. |

## Rendering pipeline

`DocumentRenderer.RenderAsync` performs the same sequence for every host:

1. Resolve the `TemplateDescriptor` from the Core catalogue.
2. Deserialise JSON into the descriptor's typed model using shared camel-case, case-insensitive options; trailing commas and string enums are supported.
3. Run the Core validator. Errors stop the render through `RenderValidationException`; warnings continue into the result.
4. Resolve density attempts. Fixed density renders once. Auto density uses the template profile.
5. Compose a complete HTML document from the first-party embedded Scriban body, encoded/formatted model values, common letterhead shell and common stylesheet.
6. Render with `IPdfEngine`, count pages and repeat at the next density only when the template has a page target that was not met.
7. If the tightest density still exceeds its target, retain clean multi-page output and add a warning rather than clipping or garbling it.
8. Compute SHA-256, suggested filename and optional bounded base64, then return `RenderResult`.

Templates are first-party embedded artefacts. Payload text is HTML-encoded before it reaches template output; end-user text is not compiled as Scriban.

## HTML, page furniture and assets

`HtmlComposer` owns the common shell. A body template supplies document content only; it must not redraw the logo, reference block, running footer or page numbers.

The default page settings are A4 portrait with margins of `1mm` top, `12mm` left/right and `22mm` bottom. Chromium's running footer carries a thin documents-red rule, the applicable strapline and live `— n of N —` numbering. Fee notes use the VAT registration number in place of the standard email segment when supplied.

Multi-page integrity relies on:

- `thead { display: table-header-group }` for repeated table headers;
- `break-inside: avoid` for rows, value boxes, media rows and signature blocks;
- print backgrounds for branded red and grey cells;
- a reserved footer margin rather than content overlay.

The governed stylesheet, logo and bundled signatures are embedded at build time. Payload-supplied embedded images are limited to PNG, JPEG and WebP. Advert evidence PDFs may be validated and appended after the generated pack; PDFsharp performs that append and does not lay out the branded pages.

## Density fitting

`Density` is `Normal`, `Compact` or `UltraCompact`; fit is `Auto` or `Fixed`.

`market-valuation-evidence` is the fit-to-page template, targeting one page. In auto mode it tries:

```text
Normal → Compact → UltraCompact
```

All other current templates use normal density under auto mode and flow to the number of pages their content requires. A caller may still request a supported fixed density.

## Exact render catalogue

| ID | Family | Auto-fit |
| --- | --- | --- |
| `market-valuation-evidence` | Valuation evidence | One-page target |
| `advert-evidence-pack` | Advert reference/evidence pack | No |
| `fee-note` | VAT fee note | No |
| `expert-report` | Flexible block report | No |
| `blank-letterhead` | Branded blank letterhead/correspondence | No |
| `repairable-contract-repair-report` | Fixed report family | No |
| `total-loss-report` | Fixed report family | No |
| `addendum-report` | Fixed report family | No |
| `diminution-rebuttal` | Fixed report family | No |
| `roadworthy-criminal-report` | Fixed report family | No |
| `part-35-response` | Question/response report family | No |
| `response-letter` | Correspondence family | No |

The flexible report block types are exactly `paragraph`, `bullets`, `datatable`, `keyvalue`, `evidencetable`, `valuebox` and `mediarow`.

## Host surfaces and parity

Parity means that document capabilities and render decisions come from Core, not that every medium has identical controls.

### CLI

Lists templates and forms, emits blank/starter/schema JSON, validates, renders, batches, installs Chromium and reports version information. It writes PDF bytes returned by Core and can open the result through the local operating system.

### API

The API registers one `IDocumentRenderer` for the process so the default Chromium instance is reused.

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/healthz` | Unauthenticated liveness. |
| `GET` | `/v1/templates` | List render templates. |
| `GET` | `/v1/authoring-templates` | List authoring templates. |
| `GET` | `/v1/authoring-templates/{id}/form` | Return a Core-owned form definition. |
| `GET` | `/v1/authoring-templates/{id}/blank` | Return blank draft JSON. |
| `POST` | `/v1/validate` | Validate without rendering. |
| `POST` | `/v1/render` | Return metadata and base64 PDF. |
| `POST` | `/v1/render.pdf` | Return raw PDF. |
| `POST` | `/v1/render.multipart` | Render JSON with policy-checked uploaded parts. |
| `POST` | `/v1/render/batch` | Render multiple items. |

The JSON render shape is `{ templateId, data, density? }`, with density `auto`, `normal`, `compact` or `ultra`.

API attachment handling is deliberately stricter than Core's in-process image resolver. Multipart fields must bind to declared model paths and pass the endpoint's attachment allow-list, content and size checks. The existence of Core support for PNG/JPEG/WebP local or embedded inputs does not grant remote clients arbitrary server-path access.

### MCP

The standalone stdio host exposes seven tools: `render_health`, `list_templates`, `validate`, `render`, `render_valuation_outputs`, `open_valuation_output` and `install_browser`. Local rendered artefacts are stored under `%LOCALAPPDATA%\CollisionRenderer\output` and represented as local `file://` descriptors. MCP is a host over Core, not a second renderer.

## API authentication

Authentication is optional. The following variables are supported:

| Variable | Meaning |
| --- | --- |
| `CR_API_TOKEN` | Compatibility setting for one raw bearer token. |
| `CR_API_TOKENS` | Rotation list of accepted raw bearer tokens. |
| `CR_API_TOKEN_SHA256` | One accepted token represented by its SHA-256 value. |
| `CR_API_TOKEN_SHA256S` | Rotation list of accepted SHA-256 token values. |

When any supported token setting is configured, all routes except `/healthz` require `Authorization: Bearer <token>`. Raw presented tokens are compared through SHA-256-based, constant-time checking. ADR-0011 is the current authentication detail and supersedes only the authentication detail in ADR-0008.

## Container topology

The workspace `Dockerfile` builds the API and uses the Playwright .NET `v1.61.0-noble` runtime, which supplies the matching Chromium and native dependencies. The final image adds Liberation and DejaVu fonts for Arial-compatible Linux metrics, enables globalisation, listens on port `8080` and runs the API assembly. It is a portable build artefact; this repository does not claim that it is deployed anywhere.

## Current limits

- Chromium is required for real renders; it must be installed locally or supplied by the container image.
- Only first-party embedded Scriban templates are supported; runtime user-authored templates are not.
- Auto-fit is a density ladder, not arbitrary scaling, and only the valuation currently has a page target.
- Very dense content may legitimately exceed a target and produce a warning.
- PDF page counting exists to drive fitting; it is not a general forensic PDF parser.
- PDFsharp appends validated evidence PDFs only; it does not replace Chromium layout.
- Core's local-path capability must not be interpreted as remote API filesystem access.
- Output uses print-oriented semantic HTML, but no claim is made that generated PDFs are tagged-PDF or PDF/UA compliant.
- Reference folders and customer material are neither source nor build inputs.