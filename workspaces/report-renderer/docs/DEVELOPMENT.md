# Development guide

> **Design authority:** [`../../../design/README.md`](../../../design/README.md) governs the linked design assets. Build and test changes must preserve that single source rather than adding workspace-local copies.

This guide covers the independent source workspace only. The commands build and exercise its standalone projects; they do not imply an external integration, caller or deployment.

## Prerequisites

| Requirement | Needed for | Notes |
| --- | --- | --- |
| SDK selected by `global.json` | Restore/build/test | Projects target their project-file frameworks; use the pinned SDK rather than documenting an evergreen version. |
| Playwright Chromium | Local real PDF renders and browser integration tests | Install through the CLI command below. |
| Docker | API container build/run | Optional for local development. |
| PowerShell | Workspace helper scripts and Windows tooling | Use PowerShell 7 where scripts specify `pwsh`. |

Current package versions that matter to browser/runtime matching are Microsoft.Playwright `1.61.0` in Core and the Playwright runtime image `v1.61.0-jammy` in the Dockerfile. MCP packages are ModelContextProtocol `1.4.0` and Microsoft.Extensions.Hosting `9.0.0`.

## Restore

From the workspace root:

```sh
dotnet restore CollisionRenderer.sln
```

If restore selects an unexpected SDK, run:

```sh
dotnet --info
dotnet --list-sdks
```

Then verify that `global.json` is being discovered from the expected directory hierarchy.

## Build

Every project in the solution is framework-agnostic, so the whole solution builds on any supported platform:

```sh
dotnet build CollisionRenderer.sln -c Release
```

No package advisory codes are suppressed. The Scriban NU1901–NU1904 suppression was removed when Scriban moved to `7.2.6`, which carries no advisories; see ADR-0013. Do not re-add `NU19xx` to `NoWarn` — restore must stay free to report the next real advisory. Check the audit with:

```sh
dotnet list package --vulnerable --include-transitive
```

## Install Chromium

Any local real render requires the Chromium revision used by Microsoft.Playwright `1.61.0`:

```sh
dotnet run --project src/CollisionRenderer.Cli -- install-browser
```

Run it once for the current user/environment and again after any Playwright update that changes the required browser revision. The provided API container already contains the matching browser; do not run the installer inside that image.

## Test

Run both test projects:

```sh
dotnet test tests/CollisionRenderer.Core.Tests/CollisionRenderer.Core.Tests.csproj -c Release
dotnet test tests/CollisionRenderer.Mcp.Tests/CollisionRenderer.Mcp.Tests.csproj -c Release
```

Or the whole solution at once:

```sh
dotnet test CollisionRenderer.sln -c Release
```

Install Chromium before tests that perform real renders. Tests and documentation must not record evergreen test totals; the executable test discovery is the current count.

## Run the CLI

```sh
dotnet run --project src/CollisionRenderer.Cli -- <command> [options]
```

Commands:

| Command | Purpose |
| --- | --- |
| `list` | List the exact Core render catalogue. |
| `forms list` | List Core-owned authoring templates. |
| `forms blank --template <id> [--out file.json]` | Emit an intentionally incomplete blank draft. |
| `forms schema --template <id> [--out file.json]` | Emit the form definition. |
| `forms starter --template <id> [--out file.json]` | Emit a synthetic overwriteable starter. |
| `validate --template <id> --data <file|->` | Validate a file or stdin without rendering. |
| `render --template <id> --data <file|-> [--out file.pdf] [--density mode] [--open]` | Render one PDF. |
| `batch --manifest file.json [--out folder]` | Render a batch through Core. |
| `install-browser` | Install the required Chromium revision. |
| `version` | Report the executable version. |

Density is `auto` (default), `normal`, `compact` or `ultra`. When output is omitted, the CLI uses `RenderResult.SuggestedFileName` in the current directory.

End-to-end local check:

```sh
dotnet run --project src/CollisionRenderer.Cli -- forms starter --template market-valuation-evidence --out val.json
dotnet run --project src/CollisionRenderer.Cli -- validate --template market-valuation-evidence --data val.json
dotnet run --project src/CollisionRenderer.Cli -- render --template market-valuation-evidence --data val.json --out val.pdf
```

## Run the API

```sh
dotnet run --project src/CollisionRenderer.Api
```

Use the URL printed by ASP.NET Core. Liveness is always unauthenticated:

```sh
curl http://localhost:<port>/healthz
```

Render JSON shape:

```json
{
  "templateId": "fee-note",
  "data": {},
  "density": "auto"
}
```

The principal endpoints are `/v1/templates`, `/v1/authoring-templates`, `/v1/validate`, `/v1/render`, `/v1/render.pdf`, `/v1/render.multipart` and `/v1/render/batch`.

### Authentication configurations

No token setting means the API routes are open. If any supported setting is configured, every path except `/healthz` requires a bearer token.

Compatibility single raw token:

```sh
CR_API_TOKEN='current-secret' dotnet run --project src/CollisionRenderer.Api
```

Raw-token rotation list:

```sh
CR_API_TOKENS='<rotation-list>' dotnet run --project src/CollisionRenderer.Api
```

Single SHA-256 token value:

```sh
CR_API_TOKEN_SHA256='<sha256-value>' dotnet run --project src/CollisionRenderer.Api
```

SHA-256 rotation list:

```sh
CR_API_TOKEN_SHA256S='<sha256-list>' dotnet run --project src/CollisionRenderer.Api
```

Follow the executable configuration parser for list syntax; do not introduce a second delimiter convention in wrappers or deployment scripts. Clients send:

```text
Authorization: Bearer <raw-token>
```

The compatibility variable `CR_API_TOKEN` must remain supported. ADR-0011 is authoritative for the rotation/hash detail and supersedes only ADR-0008's authentication detail.

### Multipart check

Use field names from the Core form/model path. The endpoint is stricter than local Core path resolution and accepts only policy-approved image/PDF parts:

```sh
curl -X POST http://localhost:<port>/v1/render.multipart \
  -F 'templateId=advert-evidence-pack' \
  -F 'density=auto' \
  -F 'data={...};type=application/json' \
  -F 'adverts[0].capturePath=@capture.pdf;type=application/pdf' \
  --output response.json
```

Treat the field path as illustrative until checked against the current form definition. Do not rely on arbitrary server-local paths in a remote API payload.

## Run the MCP host

Start the standalone stdio host with:

```sh
dotnet run --project src/CollisionRenderer.Mcp
```

The host registers `render_health`, `list_templates`, `validate`, `render`, `render_valuation_outputs`, `open_valuation_output` and `install_browser`. Rendered local artefacts are stored under `%LOCALAPPDATA%\CollisionRenderer\output` on Windows. Keep stdout reserved for the MCP protocol; route diagnostics through the configured logging channel.

This guide intentionally contains no extension-publication or distribution procedure.

## Container build and run

From the workspace root, use the repository-root build context required by linked design assets:

```sh
docker build -f Dockerfile -t collisionrenderer-api ../..
```

Run the API on port 8080:

```sh
docker run --rm -p 8080:8080 collisionrenderer-api
curl http://localhost:8080/healthz
```

Enable compatibility-token authentication:

```sh
docker run --rm -p 8080:8080 \
  -e CR_API_TOKEN='current-secret' \
  collisionrenderer-api
```

Rotation and hash variables can be supplied in the same way. The final image uses `mcr.microsoft.com/playwright/dotnet:v1.61.0-jammy`, includes matching Chromium/native dependencies, installs Liberation and DejaVu fonts, listens on `8080` and keeps globalisation enabled. Building an image does not establish that it has been deployed.

## Host-parity checks

For a meaningful parity check, use the same template ID, JSON payload and density on each host:

1. Generate one Core-owned starter with the CLI.
2. Validate and render it through the CLI.
3. Submit the same JSON to `/v1/validate` and `/v1/render.pdf`.
4. Where an MCP tool supports the operation, call it with the same template/data and compare returned metadata.

Compare template resolution, validation errors/warnings, used density, page count and PDF SHA-256 only under equivalent browser, font, OS and attachment conditions. Byte identity should not be promised across differing Chromium/font environments; contract and visual parity are the portable guarantees.

## Troubleshooting

### Chromium is missing or revision-mismatched

Symptoms include an executable-not-found error, an instruction to install Chromium, or browser integration test failures.

```sh
dotnet run --project src/CollisionRenderer.Cli -- install-browser
```

If the problem follows a package update, clear only the relevant Playwright browser cache after recording its location, then reinstall. Do not copy a random system Chromium into the expected revision directory.

### Linux text has different widths

Install Arial-metric fonts and keep globalisation enabled when running outside the supplied container:

```sh
apt-get update && apt-get install -y --no-install-recommends fonts-liberation fonts-dejavu-core
```

Invariant globalisation can alter currency and number formatting; the supplied image sets `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`.

### API returns 401 unexpectedly

Check all four supported settings: `CR_API_TOKEN`, `CR_API_TOKENS`, `CR_API_TOKEN_SHA256` and `CR_API_TOKEN_SHA256S`. Setting any one enables authentication for every endpoint except `/healthz`. Confirm that the client sends the raw token in the Bearer header, including when configured server-side by hash.

### Multipart or path attachment is rejected

Verify that:

- the field path exists in the current Core form/model;
- the slot permits an image or PDF as supplied;
- the file is within the endpoint size policy;
- the image is PNG, JPEG or WebP;
- a PDF is used only in a PDF evidence slot;
- the request is not trying to expose an arbitrary server filesystem path.

### Generated and private material

Keep generated PDFs, rasterised pages, screenshots, UI automation output, temporary uploads and local reference inventories under ignored `artifacts/` or user-local storage. Never commit customer reports, extracted customer text, registrations, claims or sensitive local filenames.