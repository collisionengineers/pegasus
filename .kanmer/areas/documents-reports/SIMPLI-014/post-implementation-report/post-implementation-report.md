# Post-implementation report — SIMPLI-014

## Summary

CollisionRenderer is now an in-process Pegasus capability rather than a standalone workspace or host. A Core-owned, fail-closed assessment-report draft use case accepts one immutable source-labelled snapshot, owns the four outcome calculations and the currently complete authorized engineer tuple, and returns typed assessment plus fee-note artifacts. Pegasus.Infrastructure renders the closed rendererref1 surface with Scriban, a bounded reusable Playwright Chromium lifetime, PDFsharp page evidence, pinned governed resources, SHA-256/template/engine metadata, and Web-only composition. The separate API, CLI, MCP/MCPB, container, catalogue, workspace CI lane, and workspace tree were retired.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` | added | Owns readiness, accepted source evidence, four outcomes, calculations, exact engineer tuple, render port/use case, typed artifacts, version and hash validation without renderer-library dependencies. |
| `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | added | Implements only the approved assessment/fee-note adapter, cached resources/templates, serialized reusable Chromium, PDF page/hash metadata, and no caller-selected paths/templates/density. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs`, `src/Pegasus.Web/Program.cs` | modified | Adds a dedicated report composition extension invoked by Web only; Worker remains unchanged. |
| `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` and dependent `packages.lock.json` files | modified | Adds only Scriban, Playwright and PDFsharp and pins exactly the two active templates, CSS, logo and complete Andy signature asset. |
| `docs/design/assets/report-renderer/templates/assessment_*.scriban` | added | Provides the fixed governed initial rendererref1 assessment and fee-note templates. |
| `tests/Pegasus.Core.Tests/Reports/**`, `tests/Pegasus.IntegrationTests/Reports/**` | added | Covers all four outcomes, compute-once contract repair, incomplete inputs, identity mismatches, exact embedded resource bytes, Web composition and real Chromium PDFs. |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | modified | Proves no renderer libraries enter Core and one Infrastructure adapter implements the Core port. |
| `workspaces/report-renderer/**` | removed | Retires the second implementation and every standalone API/CLI/MCP/MCPB/container surface after caller-backed integration. Git history and the provenance register retain its history. |
| `.github/workflows/workspaces.yml`, `workspaces/README.md`, Markdown placement scripts | modified | Removes the retired independent build/placement surface while retaining document-extraction and immutable renderer provenance. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`, `docs/open-decisions.md` | modified | Records the approved four-outcome closed activation, Core ownership, draft/issue boundary, exact Andy tuple and fail-closed absent Ed/Neil qualifications/unsupported wording. |
| `docs/current-architecture.md`, `docs/operations.md` | modified | Records local source/composition truth and explicitly does not claim deployment, durable trigger/reference/custody, approval or issue. |

## Governing docs

- **ADR-0025:** the renderer is folded into the existing Core/Infrastructure/Web project graph; no package, repository, service, API, MCP host, container or deployment unit survives.
- **ADR-0028:** Chromium is composed only in the existing Web host through `AddPegasusReportRendering`; the shared Infrastructure registration used by Worker does not add it.
- **FRD-11:** Core owns the four outcomes, raw-cost calculations, accepted tuple and source readiness. The adapter exposes only assessment and fee-note drafts, rejects unsupported/incomplete/mismatched inputs before rendering, and returns provenance metadata without claiming approval or issue.
- The authorized FRD/open-decision/current-state documentation changes reconcile the operator's 2026-08-19 answers. The immutable `reference/rendererref1/**` tree was not modified.

## Risks / follow-ups

- [[DOCS-001]] owns the automatic complete-assessment trigger, durable idempotent identity/reference/version/custody, retry and correction lineage; this PR deliberately supplies only the callable draft-generation boundary.
- [[PLAT-007]] owns the Web image Chromium/native/font layer, deployed health/telemetry/capacity/recovery proof and current-state refresh after an approved Azure write.
- Ed Mawdsley and Neil O'Reilly signature images remain governed source evidence but are not embedded or selectable because rendererref1 explicitly leaves their exact qualifications to be confirmed. No value was invented; TICK-216 tracks that acceptance nuance.
- Audit, diminution, addendum, valuation evidence and every legacy template remain unavailable.
- The full non-corpus solution invocation passed Core 625/625 and Architecture 97/97, but the legacy monolithic Integration invocation exceeded its documented approximately twelve-minute baseline while silent and was stopped. Focused report integration passed 2/2 through real Chromium; CI's existing complementary sharded integration/browser lanes remain the whole-suite merge gate.

## Verification hand-off

On the merged target branch, run:

- `dotnet restore --locked-mode`
- `dotnet build --configuration Release --no-restore`
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~AssessmentReportRenderingTests` (expected 9/9)
- install the pinned Chromium with `pwsh tests/Pegasus.IntegrationTests/bin/Release/net10.0/playwright.ps1 install chromium`
- set `PEGASUS_RENDER_EVIDENCE` to an ignored `artifacts/report-renderer/simpli-014` path, then run the focused renderer tests (expected 2/2 and two PDFs)
- run dependency-direction tests (expected report test plus existing suite green)
- run the repository's CI-equivalent sharded SQL integration and Browser lanes, Markdown placement, and documentation-link checks
- inspect both retained PDFs: PDF signature, page count >= 1, SHA-256, template `rendererref1-v1`, Playwright engine metadata, fixed outcome wording and no placeholders
- confirm `rg` finds no live `workspaces/report-renderer`, CollisionRenderer API/CLI/MCP/MCPB or fifth production project path
- confirm `dotnet list src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj package --vulnerable --include-transitive` reports no vulnerable packages.

Local evidence PDFs:
- `CE_100_assessment.pdf`: 334,652 bytes; SHA-256 `44BB49D40E8EEACCE2F0288456B5B9CDFD6B065DEEF4DD68D01DC54A9C9D56D1`
- `CE_100_fee_note.pdf`: 318,430 bytes; SHA-256 `FA2B9D42C8F9077865DB9FEBC96F3A94B218446FE136ED5F8C666AF0A4336292`
