# Architecture Decision Records

This directory records the significant architectural decisions taken for **Collision
Renderer** — the Windows desktop program, CLI and cloud API that render Collision
Engineers Ltd's branded, CPR-compliant PDF documents (vehicle valuation reports, advert
evidence packs, fee notes and expert reports) in one consistent house style.

An Architecture Decision Record (ADR) captures a single decision, the context that forced
it, the decision itself, its consequences and the alternatives that were considered. Each
record is immutable once accepted; if a decision is later revised, a new ADR supersedes the
old one rather than editing it in place.

All records here are **Accepted** and reflect the solution as built around the
`CollisionRenderer.sln` solution and its `CollisionRenderer.Core` engine.

## Index

| ADR | Title | Status |
| --- | --- | --- |
| [0001](0001-rendering-engine-headless-chromium.md) | Rendering engine: headless Chromium via Playwright | Accepted |
| [0002](0002-modular-shared-core-thin-clients.md) | Modular shared Core with thin CLI/GUI/API clients | Accepted |
| [0003](0003-unified-dotnet-8-stack.md) | Unified .NET 8 stack | Accepted |
| [0004](0004-templating-scriban-plus-csharp-shell.md) | Templating: Scriban bodies + C# letterhead shell + embedded brand CSS | Accepted |
| [0005](0005-reuse-brand-css-design-system.md) | Reuse the brand's CSS design system | Accepted |
| [0006](0006-page-furniture-chromium-header-footer-paged-media.md) | Page furniture via Chromium header/footer templates + paged-media CSS | Accepted |
| [0007](0007-density-auto-fit.md) | Density auto-fit (Normal → Compact → Ultra) | Accepted |
| [0008](0008-cloud-portability-aspnet-core-api-docker.md) | Cloud portability via ASP.NET Core API + Playwright Docker image | Accepted |
| [0009](0009-reference-material-handling.md) | Reference material (PII) is git-ignored, never committed | Accepted |
| [0010](0010-accept-scriban-security-advisories.md) | Accept/suppress Scriban security advisories | Accepted |

## Format

Each record follows the standard structure:

- **Title**
- **Status** — Accepted
- **Context** — the forces and constraints in play
- **Decision** — what was chosen
- **Consequences** — the results, positive and negative
- **Alternatives considered** — options weighed and why they were rejected
