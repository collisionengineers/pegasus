# Files — SIMPLI-014

Surveyed before planning. Paths name the expected surface; exact additions should follow existing feature-folder and test conventions.

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Reports/**` (new feature folder) | Core-owned render request/result port and technical-neutral contracts. Keep readiness, accepted-input mapping, identity/versioning and workflow orchestration for DOCS-001 rather than importing renderer policy here. |
| `src/Pegasus.Infrastructure/Reports/**` (new feature folder) | Adapt the proven Scriban/Playwright/PDFsharp engine, validation/composition and embedded design assets behind the Core port. Arbitrary local paths and standalone-host assumptions must be removed from the production seam. |
| `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | Add renderer packages and pin canonical templates/CSS/brand/signatures as embedded resources with stable logical names; reconcile locked restore. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register one application renderer adapter with an explicit lifecycle suitable for Chromium reuse and disposal. |
| `src/Pegasus.Worker/**` and/or `src/Pegasus.Web/**` composition files | Compose the adapter only through the selected existing host; no second API/MCP/service. The durable assessment trigger itself is owned by DOCS-001 and Azure proof by PLAT-007. |
| `tests/Pegasus.Core.Tests/Reports/**` | Prove Core contract/policy remains free of renderer libraries and fails closed at its boundary. |
| `tests/Pegasus.IntegrationTests/Reports/**` | Prove adapter serialization, validation, deterministic hash/result metadata, attachment/custody boundaries and representative rendererref1 outputs. |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Replace the “no workspace reference” status-quo assertions with assertions for the integrated Core port, Infrastructure implementation, allowed project graph and absence of standalone hosts. |
| `Pegasus.slnx` | Production/tests remain within the accepted project set; no workspace project or fifth production project should be added. |
| `.github/workflows/ci.yml` | Ensure Chromium/font setup and renderer tests run in the application lanes/build artifact where required. |
| `.github/workflows/workspaces.yml` | Remove the report-renderer independent lane once its source is no longer a workspace; preserve the document-extraction lane. |
| `workspaces/report-renderer/**` | Migrate reusable engine/test source, then remove the integrated workspace and its standalone API/CLI/MCP/Docker assumptions so there is no second implementation. Preserve relevant ADR/history through references or Git history rather than two live trees. |
| `workspaces/README.md` | Change the renderer status only when it has a real caller and has left `workspaces/`; preserve provenance. |
| `docs/design/assets/report-renderer/**` and `docs/design/brand/**` | Remain the single governed template/brand asset source; update accepted templates only through the owning product decisions. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Restate accepted rendererref1 behaviour/variants and any resolved wording consequences; this is product behaviour, not adapter mechanics. |
| `docs/current-architecture.md` | Refresh after source integration/caller proof to describe the as-built port, adapter and host. |
| `docs/operations.md` and `docs/runbook.md` | Refresh only with verified runtime/deployment, Chromium/font setup, health, telemetry and recovery procedures; deployment work is PLAT-007. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `AGENTS.md` | One Core policy owner, four-project boundary, no workspace caller without an accepted contract, no new deployment unit without ADR, immutable corpus/reference rules, and deploy/current-state-doc gates. |
| EPIC-004 `context.md` | Binding batch direction: monolith/Azure integration, Core ownership, rendererref1 evidence role, immutable result/provenance, fail-closed readiness and no cloud writes without approval. |
| SIMPLI-014 ticket and `checklist` | Scope, migration history and the requirement that linked sub-decisions close or are superseded before full completion. |
| SIMPLI-015 `research` and ADR-0025 | Why integration—not packaging—was chosen and the preserved implementation seams from the retired plan. |
| `docs/adr/0002-dotnet-modular-monolith-on-azure.md` | The four projects, dependency directions and Web/Worker runtime responsibilities. |
| `docs/adr/0009-adopt-pegasus-monorepo-workspaces.md` | Activation evidence required before workspace source can become application code. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Governing report finality, correction, custody and accepted-data behaviour. |
| `docs/capabilities.md` | RPT-01–05 and EXT-08 allocation/acceptance boundaries; workspace code alone is not delivery. |
| `reference/rendererref1/DESIGN_SPEC.md` | Supplied assessment layout, computed rules, variant evidence and explicit wording/qualification placeholders; evidence, not authority. |
| `reference/rendererref1/report_data_schema.json` and sample JSON/PDFs | Candidate assessment payload shape and representative visual/parity fixtures; reveals the four-outcome/schema expectations. |
| `workspaces/report-renderer/docs/ARCHITECTURE.md` and `TEMPLATES.md` | Current single-engine architecture, catalogue, resource rules, host boundaries and Chromium/container assumptions. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/Contracts.cs` and `DocumentRenderer.cs` | Existing technical input/result and render pipeline; hash exists, but durable Pegasus identity/custody/versioning does not. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/CollisionRenderer.Core.csproj` | Package dependencies and linked canonical resource embedding that must be moved without drift. |
| `src/Pegasus.Core/Assessment/**` and `src/Pegasus.Infrastructure/Persistence/Assessment*.cs` | Current accepted assessment model/persistence and the gap between it and a complete report payload. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Existing storage/custody and adapter registration conventions to reuse. |
| `src/Pegasus.Worker/**`, `infra/modules/platform.bicep`, `docs/operations.md` | Actual Worker queue/runtime and deployed Azure topology; do not assume the workspace Dockerfile is deployable here unchanged. |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Mechanical four-project and no-workspace constraints that integration must deliberately update. |
| `.github/workflows/workspaces.yml` | Current non-caller workspace-only build proof and browser-install steps. |

## Ripple effects

- [[DOCS-001]] depends on this adapter and adds the real accepted-assessment caller, durable job/result persistence, immutable report identity/version/hash/provenance, correction lineage and custody.
- [[PLAT-007]] depends on DOCS-001 and adds existing-topology Azure runtime dependencies, queueing, health, telemetry, timeout/retry/restart/poison proof and deployment documentation.
- [[TICK-203]]/[[SIMPLI-012]] and [[TICK-214]] decide whether any MCP-facing surface survives; it cannot create a second renderer or production host.
- [[TICK-204]]–[[TICK-208]], [[TICK-213]], and [[TICK-216]] determine accepted variants, Audit behaviour, template mapping, correction evidence, density and wording/assets.
- [[TICK-211]]/[[TICK-212]] affect build warnings and deterministic locked restore.
- Package locks, publish/container size, Chromium version, fonts, CVE/licence evidence, memory/concurrency, test duration and CI caching all change.
- Representative rendererref1 JSON/PDFs become test evidence; generated outputs belong under `artifacts/`, never `reference/` or `corpus/`.

## Out of scope

- No separate CollisionRenderer API, microservice, repository, NuGet feed/package, MCP production host or Azure deployment unit.
- No Azure/cloud writes or deployment in SIMPLI-014; PLAT-007 requires explicit target approval.
- No report send/Outlook mutation, approval inference, external receipt, invoicing/accounting workflow, AI generation, or Box test writes.
- No fabrication of missing wording, signatures, qualifications, sample case data or operator decisions.
- No change to `reference/rendererref1/`; it is supplied immutable evidence.
- No duplicate template, calculation, outcome or state vocabularies across Core and Infrastructure.
