# Research — production execution location

## Question

Where can the integrated Chromium-based report renderer execute in production without becoming a separate service or violating Pegasus's existing deployment boundaries?

## Findings

1. Pegasus production currently has two composition roots: an always-warm ASP.NET Core Web Container App and a .NET 10 isolated Azure Functions Worker on Flex Consumption. The Web image is an existing custom Linux container in the existing Container Apps environment; the Worker is deployed as a code package. Sources: `infra/modules/platform.bicep`, `.azure/deployment-plan.md`, `docs/operations.md`.
2. CollisionRenderer requires a matching Chromium runtime, Playwright native dependencies, fonts, and writable temporary space. Its workspace Dockerfile proves this using the pinned Playwright Noble image. Sources: `workspaces/report-renderer/Dockerfile`, `workspaces/report-renderer/docs/ARCHITECTURE.md`.
3. Flex Consumption is code-only and does not support custom containers. Microsoft Learn's current Functions hosting comparison confirms Flex Consumption container support is unavailable. Adding Chromium/native dependencies to the existing Flex Worker therefore lacks a supported, reproducible host mechanism.
4. Azure Container Apps and Container Apps Jobs can run arbitrary container dependencies and queue-triggered background work, but a new Job or app would be a new deployment unit. The binding EPIC-004 context and repository invariant prohibit a separate renderer service/deployment without a new accepted ADR.
5. The existing Web Container App is already the only current deployable boundary that is both part of the monolith and capable of carrying the renderer's OS dependencies. Its image can be based on or install the pinned Playwright runtime while continuing to run `Pegasus.Web`.
6. The operator's instruction says the renderer is called when an assessment has all details. A Core-owned completion use case composed by Web can invoke the Infrastructure renderer inside the existing Web request/application boundary, persist a durable attempt/result before returning, and surface failure. At current volume (~2,000 cases/month), this is the smallest caller-backed route. If later evidence requires detached execution, that is a separate architectural decision rather than a reason to introduce a renderer service now.
7. Generated artifacts can use existing application custody conventions and database identity/provenance records. Exact Box or Azure Blob final custody belongs to DOCS-001 research and FRD-11; execution location must not invent a second custody path.
8. Any production deployment is an Azure write and requires explicit approval for the exact subscription/resource group. Local image build, Bicep validation, and read-only inventory are permitted first.

## Implications

- Execute rendering inside the existing Pegasus Web Container App, behind the Core-owned contract and Infrastructure adapter.
- Extend the existing Web image with the pinned Chromium/native/font runtime; do not deploy `CollisionRenderer.Api`, `CollisionRenderer.Mcp`, a standalone renderer container app, or a Container Apps Job.
- Treat completion-triggered rendering as a durable, idempotent application operation with explicit pending/succeeded/failed state so request interruption can be reconciled.
- Keep Worker unchanged for the first integrated caller. A future move to detached containerized background execution needs measured evidence and an accepted ADR because it changes the deployment topology.
- PLAT-007 owns IaC/image/health/telemetry/deployment proof after SIMPLI-014 and DOCS-001 establish the integrated caller.

## Verified premises

- Verified from repository IaC: Web is a custom Container App with `minReplicas: 1`; Worker is Flex Consumption code deployment.
- Verified from workspace source: real renders require Playwright/Chromium and matching native dependencies.
- Verified from current official Azure documentation on 2026-08-19: Flex Consumption is code-only and does not support a custom container.
- Assumption to validate in implementation: the existing Web CPU/memory allocation is sufficient; local container stress tests and deployed telemetry must prove or adjust resources.
