# Notices — legal, third-party and provenance

Collision Renderer is an internal Collision Engineers Ltd product workspace. This notice records facts present in the retained sources and current project graph. It is not a substitute for the licence texts distributed by upstream projects or packages, and it does not infer a licence where the retained sources do not state one.

The workspace is source-only and non-caller in its current repository context. Nothing in this notice represents that the software has been integrated into or deployed by another product or service.

## Direct package dependencies in the retained project graph

| Project | Direct package | Version | Purpose | Licence conclusion retained in sources |
| --- | --- | ---: | --- | --- |
| `CollisionRenderer.Core` | PDFsharp | `6.2.4` | Appends validated evidence PDFs after Chromium rendering; not used for page layout. | No conclusion stated in the retained notice; verify the package's distributed licence/notice. |
| `CollisionRenderer.Core` | Scriban | `7.2.6` | First-party HTML body-template engine. | BSD-2-Clause. |
| `CollisionRenderer.Core` | Microsoft.Playwright | `1.61.0` | Controls headless Chromium for HTML-to-PDF rendering. | Apache-2.0. |
| `CollisionRenderer.Mcp` | ModelContextProtocol | `1.4.0` | MCP server contracts and transport support. | No conclusion stated in the retained notice; verify the package's distributed licence/notice. |
| `CollisionRenderer.Mcp` | Microsoft.Extensions.Hosting | `10.0.10` | MCP process hosting, dependency injection, configuration and lifetime. | No conclusion stated in the retained notice; verify the package's distributed licence/notice. |

Project references to Core are internal graph edges, not third-party package entries. Transitive dependencies remain governed by their own package manifests, notices and licence files.

## Runtime, test and container components

| Component | Purpose | Licence statement retained in sources |
| --- | --- | --- |
| Chromium supplied/downloaded for Playwright | Browser and PDF engine. | BSD-3-Clause and others. Chromium is a multi-component work and its accompanying notices remain authoritative. |
| .NET / ASP.NET Core | Runtime, base class libraries and API host. | MIT. |
| xUnit | Test framework used by retained test projects. | Apache-2.0. |
| Liberation fonts | Arial-metric Linux body text in the container. | OFL, as recorded in the retained notice. |
| DejaVu fonts | Linux fallback fonts in the container. | GPL with exception, as recorded in the retained notice. |

The Playwright runtime image and operating-system packages contain additional components. Container distributions must retain the image/package notices required by their upstream terms. This workspace notice does not replace those inventories.

## Brand assets

The master gear-“C” logo at `docs/design/brand/logos/logo_no_margin.png` and engineer signatures under `docs/design/brand/signatures/` are Collision Engineers Ltd property. They are governed brand assets, not third-party open-source components.

- Do not redraw, reconstruct or substitute the gear logo without brand authority.
- Bundled signatures must be used only for authorised document production.
- Payload-supplied custom signatures and images are case data, not additions to the brand library.
- Build-time embedding in Core does not transfer ownership or grant reuse outside the authorised product context.

Tw Cen MT Std, Futura and the unused white reverse logo do not ship as renderer font/assets. Rendered body copy uses Arial where available or a metric-compatible substitute such as Liberation Sans in Linux. Do not add or embed proprietary font files unless their licence has been checked and recorded as permitting the intended distribution and embedding.

## Design and source provenance

The root design authority is the repository `docs/design/README.md`. Renderer templates, stylesheet, logo and signatures are linked or embedded from governed design sources at build time rather than maintained as divergent workspace copies.

The visual and behavioural design was informed by:

- the CSS-native Collision Engineers design material;
- prior renderer behaviour and preferred output references;
- private document/style examples used for local comparison.

Reference material informs implementation but is not product source and must not become a runtime or build dependency.

## Private reference material and personal data

The following local folders may contain prior art or sensitive reference material:

- `documentexamples/`;
- `stylexamples/`;
- `collision-engineers-design-dev/`;
- `report-renderer/`.

`documentexamples/` and `stylexamples/` contain real customer reports and may include names, vehicle registrations, claim details and other personal or confidential information. All four folders are local reference material, are git-ignored when present and must not be committed, redistributed, published in package contents or copied into synthetic fixtures.

Any exact local inventory, extracted text, page rasterisation or comparison output belongs under ignored `artifacts/` storage. Tracked documentation may describe reference families and aggregate use, but must not reproduce sensitive filenames or case facts. See ADR-0009.

## Generated documents and attachments

Rendered PDFs, advert captures, uploaded evidence, custom signatures, local image paths and saved drafts can contain customer or case data. They must be handled under the applicable organisational retention, access and confidentiality rules.

- Do not commit generated customer documents or uploads.
- Do not use customer payloads as tests, starter drafts or examples.
- Use synthetic, non-identifying fixtures for automated tests.
- Local MCP artefacts under `%LOCALAPPDATA%\CollisionRenderer\output` are user-local case artefacts and require the same care as CLI or API output.
- API multipart temporary files must not be treated as durable source assets.

## Security notice

No package security advisories are suppressed in this workspace. The Scriban NU1901–NU1904 suppression that ADR-0010 accepted was resolved by upgrading Scriban to `7.2.6`, which carries no advisories; ADR-0013 records that upgrade and supersedes ADR-0010 in its entirety.

The design properties that made the earlier acceptance defensible still hold, and still bound how a future advisory should be assessed:

- Scriban templates are first-party embedded artefacts;
- end users do not author or compile runtime templates;
- payload text is HTML-encoded and passed as values rather than compiled as template source.

`TreatWarningsAsErrors` is `false` here, so a future advisory surfaces as a build warning rather than a failure. A clean build is not evidence of a clean audit; run `dotnet list package --vulnerable --include-transitive`.

The API supports optional bearer authentication through:

- `CR_API_TOKEN` for compatibility with one raw token;
- `CR_API_TOKENS` for raw-token rotation lists;
- `CR_API_TOKEN_SHA256` for one SHA-256 token value;
- `CR_API_TOKEN_SHA256S` for SHA-256 rotation lists.

When any token source is configured, every route except `/healthz` is protected. Presented bearer tokens are checked using SHA-256-based constant-time comparison. Authentication does not replace transport security, secret management, host access control, request-size controls or attachment validation. See ADR-0011, which supersedes only ADR-0008's authentication detail.

## No inferred grants or conclusions

Except for the licence statements explicitly retained above, this document does not conclude the copyright status, compatibility, redistribution terms, patent terms or trademark rights of a dependency. Before external distribution, obtain the exact resolved dependency inventory and retain the licence and notice files supplied with those resolved versions.
