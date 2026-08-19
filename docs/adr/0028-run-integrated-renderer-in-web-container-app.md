---
id: ADR-0028
status: accepted
date: 2026-08-19
supersedes: []
superseded_by: []
related_capabilities: [EXT-08, RPT-01, RPT-02]
related_frd: [frd-11]
tags: [architecture, renderer, chromium, container-apps, hosting]
---
# ADR-0028: Run the integrated report renderer in the Web Container App

## Status

Accepted on 2026-08-19. This decision refines ADR-0015 and ADR-0025; it
supersedes neither.

## Context

ADR-0025 requires CollisionRenderer to be integrated behind a
`Pegasus.Core`-owned port in the existing application rather than deployed as a
separate product, package, API, MCP host, service, or job. It permits Web or
Worker composition but does not choose the production execution boundary.

Real rendering requires a pinned Chromium build, its matching native Linux
dependencies, fonts, and writable temporary space. The existing Pegasus Web
boundary is a custom Linux container hosted by Azure Container Apps. The
existing Worker is a code-deployed .NET isolated Function App on Flex
Consumption and cannot carry a custom container image. A separate Container
Apps Job or renderer app could carry Chromium, but would create another
deployment unit and operational boundary.

The execution location is a durable technical choice. Report readiness,
accepted inputs, immutable identity and hash, correction, human approval, and
failure behaviour remain governed by FRD-11 and `Pegasus.Core` rather than by
this ADR.

## Decision

The integrated report renderer will execute in process inside the existing
Pegasus Web Container App. The Web image will carry the pinned
Chromium/Playwright native dependencies and approved fonts alongside
`Pegasus.Web`. Web composes the Infrastructure renderer behind the
`Pegasus.Core` render contract.

The existing Flex Consumption Worker remains unchanged for this capability.
Pegasus will not deploy CollisionRenderer.Api, a renderer-specific Container
App, a Container Apps Job, a renderer Function App, or another queue consumer.

## Consequences

- Renderer dependency installation, process lifetime, temporary-file fencing,
  health, telemetry, resource sizing, timeout, and recovery evidence belong to
  the existing Web artifact and deployment route.
- The application operation must remain durable and idempotent across request
  interruption and Web revision restart as required by FRD-11; this ADR does
  not define or duplicate that behaviour.
- PLAT-007 owns local container proof, infrastructure changes, deployed
  capacity and recovery evidence. Any Azure write still requires explicit
  approval for the exact target.
- A future detached renderer or move to another host requires measured evidence
  that the Web boundary cannot carry the workload and a new accepted ADR before
  adding or changing a deployment unit.
- Acceptance of this decision proves architecture only. It does not prove the
  renderer is integrated, deployed, healthy, capacity-tested, or accepted.

## Options considered

- **Existing Flex Consumption Worker** — rejected because the current
  code-deployed host cannot carry the required custom Chromium container.
- **Separate Container Apps Job or renderer service** — rejected because it
  introduces a deployment and operational boundary contrary to ADR-0025
  without evidence that the existing Web boundary is insufficient.
- **Existing Web Container App** — selected as the smallest existing
  custom-container composition boundary that can carry the renderer runtime.

## Links

- [ADR-0015](0015-host-web-on-container-apps-consumption.md) — existing Web
  hosting boundary.
- [ADR-0025](0025-integrate-renderer-and-extractor-into-the-application.md) —
  renderer integration boundary.
- [FRD-11](../frd/frd-11-reports-correspondence-and-reviewed-proposals.md) —
  report behaviour, finality, provenance, approval, and failure rules.
