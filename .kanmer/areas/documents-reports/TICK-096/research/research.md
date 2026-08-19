# Research — deterministic renderer foundation

## Question

Which CollisionRenderer mechanics should become Pegasus's deterministic rendering adapter for RPT-01?

## Findings

1. The workspace has one reusable engine: typed payload mapping/validation, first-party embedded Scriban templates/assets, semantic HTML composition, Playwright Chromium PDF production, PDF page counting, PDFsharp attachment append, SHA-256 result identity, warnings, and deterministic date/time seams.
2. Hosts (CLI/API/MCP) are transport shells and are not production integration candidates. The approved home is existing `Pegasus.Infrastructure`; Core owns the port/policy.
3. Initial active templates are not the generic workspace catalogue entries. They are the four rendererref1 assessment variants plus fee note, requiring a typed four-way application contract and approved templates/assets.
4. Determinism is contractual/visual under pinned payload/template versions, Chromium, fonts, OS, and attachment ordering. PDF byte equality across differing browser/font environments is not promised; production pins one environment and stores actual hash.
5. Core computes business figures once from accepted raw inputs. The renderer validates the complete typed snapshot and formats/displays values; it must not become a second calculation-policy owner.
6. Existing lenient behaviors—unknown signature silently omitted, JSON-path mutation no-op, arbitrary template IDs/caller density—are unsuitable at the application boundary and must fail closed or be excluded.
7. Artifacts require immutable reference/version/hash/provenance and correction semantics owned by DOCS-001/FRD-11.

## Implications

- Migrate only reusable engine mechanisms into Infrastructure; adapt them to a Core `IReportRenderer`-style port without creating a fifth project.
- Create typed, versioned application payloads/templates for the approved assessment/fee-note set.
- Pin Scriban/Playwright/PDFsharp, Chromium image, fonts, resource names, template version, payload version, timezone/date seam, and attachment order.
- Validate before launching Chromium; fail closed and persist actionable failure.
- Retire standalone hosts/catalogue exposure.
