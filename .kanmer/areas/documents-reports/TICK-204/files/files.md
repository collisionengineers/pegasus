# Files — TICK-204

## Where the change lands

| Path | Why |
|---|---|
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Canonical home for the closed outcome vocabulary, variant-specific behavior, readiness/fail-closed rules, and acceptance evidence. This avoids making supplied reference material or the renderer adapter a policy owner. |
| `docs/capabilities.md` | Only if the existing `RPT-02` allocation note needs a concise link/clarification after FRD-11 gains the behavior; it must not duplicate the vocabulary. |

This ticket should define the behavior in governing documentation. The implementation files below belong primarily to `SIMPLI-014` after this decision is settled, not to a competing renderer implementation in TICK-204.

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` | The renderer must be integrated behind a Core-owned port with a real caller, not retained as a separate product/service/package. |
| `docs/prd/pegasus-product.md` | Core and human authority, evidence tiers, workspace boundary, and fail-closed product invariants. |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Canonical owner for accepted professional findings and source/Engineer authority feeding report outcomes. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Existing immutable artifact, correction, approval, provenance, and delivery behavior that every variant inherits. |
| `docs/capabilities.md` | `RPT-02` fixes the count at four; `EXT-08`, `CASE-31`, and `ENG-02` define activation and upstream accepted-data seams. |
| `reference/rendererref1/DESIGN_SPEC.md` | Supplied approved design distinctions, raw/computed data rules, shared bundle structure, and explicit unresolved wording; also contains the stale “three/complete set” contradiction. |
| `reference/rendererref1/report_data_schema.json` | Four-value enum and conditional total-loss requirements; supplied evidence rather than the final Core contract. |
| `reference/rendererref1/sample_job_*.json` | One representative payload for each of the four values. |
| `reference/rendererref1/Sample - *.pdf` | Concrete seven-page outputs proving the four page-1/page-2 distinctions and shared bundle contents. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/TemplateCatalog.cs` | Current catalog combines repairable/contract repair, separates total loss, and omits cash in lieu. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/AuthoringCatalog.cs` | Current authoring presets are generic block documents, not the supplied assessment schema or a four-way selector. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/Models/Documents.cs` | Current `ExpertReportDocument` permits precomposed sections and has no typed assessment outcome/readiness model. |
| `workspaces/report-renderer/tests/CollisionRenderer.Core.Tests/CoreTests.cs` | Existing generic validation/render/hash behavior and the absence of four-variant parity coverage. |
| `workspaces/report-renderer/tests/CollisionRenderer.Core.Tests/IntegrationTests.cs` | Real Chromium path exists, but missing-browser execution returns without proving render output. |
| `workspaces/report-renderer/tests/CollisionRenderer.Core.Tests/PreviewAndStarterTests.cs` | Catalog/starter/preview coverage does not establish assessment-outcome behavior. |
| `workspaces/report-renderer/tests/CollisionRenderer.Core.Tests/FormattingAndDateSeamTests.cs` | Existing deterministic UK date and numeric formatting seams to preserve. |

## Ripple effects

- `SIMPLI-014` must consume the FRD outcome contract when adding the Core port, accepted-data mapper, Infrastructure adapter, and Web/Worker caller.
- `TICK-206` must map the settled variants/bundle to capability IDs without redefining their behavior.
- `TICK-216` must keep unresolved wording/signature assets closed; total-loss category variants with unavailable approved wording must fail closed.
- Later implementation tests need four accepted fixtures, negative readiness/unknown-value cases, computation/parity checks, and real composed-caller evidence.
- If the reference prose is corrected, `reference/rendererref1/DESIGN_SPEC.md` is local supplied evidence and must be changed only under the repository’s reference stewardship rules; no change is required to decide the canonical FRD behavior.

## Out of scope

- Integrating or moving `workspaces/report-renderer/`, changing `Pegasus.slnx`, Azure/container topology, or creating a renderer caller; those are `SIMPLI-014` and related EPIC-004 tickets.
- Template-to-capability allocation (`TICK-206`), execution location (`TICK-215`), MCP/MCPB design (`TICK-203`/`TICK-214`), or package/analyzer/density decisions.
- Approving placeholder salvage/recovery/statement/signature wording (`TICK-216`).
- Editing `docs/operator-notes.md` or treating reference samples as authority over operator truth.
