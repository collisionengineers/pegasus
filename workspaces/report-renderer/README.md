# Collision Renderer

Collision Renderer is an independent source workspace for producing Collision Engineers Ltd branded PDFs in a consistent house style. A single Core engine serves the command line, Windows GUI, HTTP API and MCP host.

> **Workspace boundary:** this directory is retained as source-only, non-caller workspace code. Its hosts can be built and exercised independently, but this repository does not establish integration with, invocation by, or deployment into any other product or environment. Current integration status and activation conditions are maintained in the [workspace integration register](../README.md#integration-status-register).

## Current workspace

The solution contains five shipped projects and two test projects:

- `src/CollisionRenderer.Core` — shared rendering, validation, template and authoring engine.
- `src/CollisionRenderer.Cli` — `collisionrenderer` command-line host.
- `src/CollisionRenderer.Gui` — WinUI 3 desktop authoring and preview host.
- `src/CollisionRenderer.Api` — ASP.NET Core HTTP host.
- `src/CollisionRenderer.Mcp` — standalone MCP stdio host.
- `tests/CollisionRenderer.Core.Tests` — Core and real-browser rendering tests.
- `tests/CollisionRenderer.Mcp.Tests` — MCP host and tool-contract tests.

Core converts typed JSON payloads to HTML with Scriban and the shared design assets, then renders A4 PDF through Chromium using Microsoft.Playwright. PDFsharp is used only where existing PDF evidence must be appended; it is not the layout engine.

## Document catalogue

The render catalogue contains exactly these 12 IDs:

1. `market-valuation-evidence`
2. `advert-evidence-pack`
3. `fee-note`
4. `expert-report`
5. `blank-letterhead`
6. `repairable-contract-repair-report`
7. `total-loss-report`
8. `addendum-report`
9. `diminution-rebuttal`
10. `roadworthy-criminal-report`
11. `part-35-response`
12. `response-letter`

Core also owns the blank drafts, starter drafts, form definitions and attachment policies used by the hosts. Hosts must not invent template-specific rendering or validation rules.

## Quick start

Run these commands from this workspace root:

```sh
dotnet restore CollisionRenderer.sln
dotnet build CollisionRenderer.sln -c Release

# Required once on a machine before local Chromium renders
dotnet run --project src/CollisionRenderer.Cli -- install-browser

# Generate and render a synthetic starter
dotnet run --project src/CollisionRenderer.Cli -- forms starter --template fee-note --out fee.json
dotnet run --project src/CollisionRenderer.Cli -- render --template fee-note --data fee.json --out fee.pdf
```

The full solution includes a Windows-only GUI. On Linux or macOS, build the cross-platform projects individually as described in the development guide.

## Documentation

- [Architecture](docs/ARCHITECTURE.md) — projects, pipeline, hosts, API authentication, container and limits.
- [Template authoring](docs/TEMPLATES.md) — payloads, forms, attachments, assets, density and design invariants.
- [Development](docs/DEVELOPMENT.md) — restore, build, test, run, browser and container commands.
- [Notices](NOTICE.md) — third-party, brand, provenance, privacy and security notices.
- [Architecture decision records](docs/adr/README.md) — immutable ADR index.

Generated PDFs, screenshots, extracted reference text and test artefacts belong under ignored `artifacts/` paths. Do not commit customer or case data.