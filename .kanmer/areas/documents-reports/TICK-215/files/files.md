# Files — production execution location

## Change files

| Path/module | Expected change | Risk |
| --- | --- | --- |
| `Dockerfile` / existing Web image build inputs | Carry the pinned Playwright Chromium runtime, fonts, and browser binaries in the existing Pegasus Web image | Image size, native-library parity, startup time, memory |
| `infra/modules/platform.bicep` | Configure renderer settings/resources/health and any existing-storage permissions on the Web Container App; no new service | Production topology and least privilege |
| `infra/main.bicep`, `infra/main.parameters.json` | Thread only necessary settings through the existing deployment | Fail-closed activation and deployment drift |
| `src/Pegasus.Web/Program.cs` and Web composition helpers | Compose the Infrastructure renderer behind Core's port in the existing Web host | Lifetime, concurrency, disposal |
| `src/Pegasus.Infrastructure/**` | Host the imported rendering adapter and durable attempt/artifact persistence | Chromium process lifecycle and transactional boundaries |
| `src/Pegasus.Core/**` | Own execution-neutral report request/result/readiness contracts | Must not depend on Playwright or templates |
| `tests/Pegasus.ArchitectureTests/**` | Prove no standalone renderer deployment/host and dependency direction | Boundary regression |
| `tests/Pegasus.IntegrationTests/**` | Prove completion-triggered render, restart/idempotency/failure, container/runtime parity | Real-browser test cost |
| `scripts/**`, `.github/workflows/ci.yml` | Build and validate the existing Web OCI image with a smoke render | CI duration and browser cache |
| `docs/current-architecture.md`, `docs/operations.md`, `docs/runbook.md` | Record as-built/deployed runtime and operational recovery after deployment | Must reflect evidence tier |

## Context files

| Path | Why read it |
| --- | --- |
| `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` | Requires monolith integration behind a Core port and a real caller |
| `docs/adr/0009-adopt-pegasus-monorepo-workspaces.md` | Workspace activation and migration conditions |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Report identity, finality, correction, approval, and delivery behavior |
| `infra/modules/platform.bicep` | Current Web/Worker hosting and identities |
| `.azure/deployment-plan.md` | Immutable record of the sole production topology |
| `workspaces/report-renderer/Dockerfile` | Proven Chromium/font runtime baseline |
| `workspaces/report-renderer/docs/ARCHITECTURE.md` | Renderer dependencies and current standalone-host boundaries |
| `EPIC-004/context.md` | Binding no-separate-service direction |

## Ripple effects

- SIMPLI-014 owns source integration; DOCS-001 owns the real assessment caller and durable report identity; PLAT-007 owns deployment proof.
- Worker permissions and packaging should remain untouched unless implementation evidence disproves the Web route.
- Azure resource sizing may change within the existing Web Container App; exact production changes require approval.

## Out of scope

- A new Container Apps Job, renderer API, renderer MCP deployment, standalone repository/package, or third production composition root.
- Azure writes before exact-target approval.
- Selecting final report custody policy or changing report approval/sending semantics.
