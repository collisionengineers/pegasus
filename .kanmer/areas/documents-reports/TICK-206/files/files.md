# Files — TICK-206

## Where the change lands

| Path | Why |
|---|---|
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Canonical place to record the initial active family allow-list, fail-closed unsupported-template behavior, and the boundary for later separately activated report families. |
| `docs/capabilities.md` | Only if a concise activation note/cross-reference is needed for `RPT-01`/`RPT-02`; it must not duplicate the mapping behavior. |

TICK-206 should establish the governing mapping. Production implementation belongs to `SIMPLI-014`, informed by this decision and TICK-204.

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` | Integration shape and prohibition on a standalone renderer boundary. |
| `docs/prd/pegasus-product.md` | No dormant capability, Core policy ownership, caller evidence, and workspace activation constraints. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Existing report artifact, approval, versioning, correction, provenance, and delivery rules inherited by active families. |
| `docs/capabilities.md` | `RPT-01`/`RPT-02` are the approved initial output mapping; `RPT-03`–`RPT-05` and valuation capabilities remain separately gated. |
| `reference/rendererref1/DESIGN_SPEC.md` | Approved assessment/fee-note family shape and explicit unresolved wording that must remain closed. |
| `reference/rendererref1/report_data_schema.json` | Supplied four-outcome assessment input evidence, not an authority for exposing other workspace templates. |
| `reference/rendererref1/sample_job_*.json` | Four assessment-family fixtures. |
| `reference/rendererref1/Sample - *.pdf` | Four concrete assessment bundles, each including a fee-note page and repair-specification content. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/TemplateCatalog.cs` | The 12-entry engine catalogue that must not become the application-facing allow-list. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/AuthoringCatalog.cs` | Forms/starters expose the same unsupported presets; these surfaces must remain inactive in Pegasus. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/Models/Documents.cs` | Current four base models and generic expert blocks do not express the approved closed assessment contract. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/Validators.cs` | Validation is keyed by generic model type and is too weak to enforce active-family readiness. |
| `workspaces/report-renderer/docs/TEMPLATES.md` | Documents the four engine base families/eight presets and confirms hosts currently inherit all registered entries. |
| `workspaces/report-renderer/tests/CollisionRenderer.Core.Tests/CoreTests.cs` | Hard-codes broad catalogue membership and generic mapping; tests need an application activation boundary. |
| `workspaces/report-renderer/tests/CollisionRenderer.Core.Tests/PreviewAndStarterTests.cs` | Iterates every authoring preset and treats blank letterhead as registered; not appropriate evidence for the active Pegasus surface. |
| `workspaces/report-renderer/tests/CollisionRenderer.Core.Tests/IntegrationTests.cs` | Iterates every engine template and returns early when Chromium is absent; does not prove the approved caller allow-list. |

## Ripple effects

- `SIMPLI-014` must expose only the Core-approved assessment and fee-note operations and reject every legacy/unknown template selector.
- TICK-204’s settled four-outcome contract becomes the variant routing within the active assessment family.
- TICK-216 keeps unaccepted wording/signatures unavailable even inside an otherwise active family.
- Web/Worker/API/MCP discovery and request models must not leak inactive workspace template IDs.
- Tests must separately prove engine-mechanism reuse and the narrower application capability surface; a broad internal catalog test cannot stand in for caller authorization.
- Workspace retirement/build configuration should avoid copying unsupported presets/assets into production unless a concrete internal reuse justifies them.

## Out of scope

- Implementing the Core port, Infrastructure adapter, Web/Worker caller, Azure/container changes, or moving source from `workspaces/` (`SIMPLI-014`).
- Defining the four outcome distinctions (TICK-204).
- Activating audit, diminution, addendum, valuation evidence, advert packs, blank letterhead, Part 35, roadworthy/criminal, response-letter, or general expert-report capabilities.
- Deleting historical workspace ADRs or reference evidence.
- Approving unresolved wording/signatures (TICK-216).
