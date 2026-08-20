## Post-implementation report — PLAT-007

**Retrospective backfill.** The renderer's Azure integration shipped in release 12 (2026-08-19) before this ticket's pipeline documents existed.

### What shipped
- Renderer composed in-process in the Web host, no standalone service: `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`, registered once (`DependencyInjection.cs:416`).
- Chromium/native dependency: Web container image built from `mcr.microsoft.com/playwright/dotnet`, tag-locked to the `Microsoft.Playwright` package version; locally verified against the OCI archive with `oras` (ADR-0028, DELIV-012).
- Resource sizing raised for in-process rendering (operator decision): `infra/modules/platform.bicep` — 1.0 vCPU / 2Gi on the Web container, immediately followed by Startup/Liveness/Readiness health probes.
- Release 12's new revision "provisioned and turned Healthy on its first pull of that base" (`docs/operations.md:344-349`) — the deployed health result.
- Operator-reachable caller: the case assessment page's "Report draft" action renders and returns a real PDF through Chromium (`docs/operations.md:21-27`).

### Not claimed here (named honestly)
- Durable persistence of the report artifact/reference against the case, and retry/timeout/duplicate-delivery fail-closed behaviour for an *automatic* accepted-assessment trigger, are explicitly DOCS-001's scope per `docs/operations.md`'s own wording. `PlaywrightAssessmentReportRenderer.RenderAsync` has no retry/duplicate wrapper of its own; that orchestration does not exist yet because DOCS-001 (the Core-owned trigger) has not been built.
- Infrastructure-level "unavailable renderer" fail-closed IS proven: Container Apps Liveness/Readiness probes restart the container on a wedged in-process Chromium (standard platform mechanism).
- For a live case, rendering currently fails closed on "Repair cost figures" (no estimate import yet — ENG-002); correct behaviour, not a defect.

### Deployment
- `git cat-file -e 2325ed4a:src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` succeeds — present at release 13's SHA.
- `git cat-file -e 2325ed4a:infra/modules/platform.bicep` succeeds; `Features` block and resource sizing confirmed present at the same SHA.

### Residual
Rendered-output evidence for a live case awaits an estimate import (ENG-002) and the durable trigger workflow (DOCS-001); that residual belongs to those tickets, not to PLAT-007's own deployment-topology scope, which is complete.
