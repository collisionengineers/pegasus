## Research — PLAT-007 — retrospective backfill, verified 2026-08-20

**Question:** Is the CollisionRenderer capability composed and deployed through Pegasus's existing Azure topology (not a standalone service), with matched Chromium/native dependencies, health, and telemetry?

### What exists and is deployed (release 12, `main`/`dev` = `ed3be51c`, present at production SHA `2325ed4a`)
- `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` — in-process renderer (`IAssessmentReportRenderer`), registered as a singleton in `src/Pegasus.Infrastructure/DependencyInjection.cs:416`. No separate service/API/MCP host exists for it (`grep -rn IAssessmentReportRenderer` finds only this one registration).
- `infra/modules/platform.bicep:431-440` — ADR-0028 comment block plus `resources: { cpu: json('1.0'), memory: '2Gi' }` on the same Web container app (raised from the prior default, operator decision, in-process rendering) with `Startup`/`Liveness`/`Readiness` probes at `/health/live` and `/health/ready` immediately following.
- `docs/operations.md:21-35` — "The assessment renderer is deployed with a reachable operator caller since release 12": the Web image carries the pinned Chromium build (`mcr.microsoft.com/playwright/dotnet`, tag-locked to the `Microsoft.Playwright` package version), verified against the OCI archive with `oras`; the case assessment page's "Report draft" action renders and returns a PDF through real Chromium.
- `docs/operations.md:344-349` (release 12) — the new revision "provisioned and turned Healthy on its first pull of that base" carrying the Chromium image; this is the deployed health-result evidence.

### What is NOT proven by this evidence (named honestly, not fabricated)
- **Durable persistence of the report artifact/reference against the case**, and **retry/timeout/duplicate-delivery fail-closed behaviour for an automatic trigger**, are explicitly reserved by `docs/operations.md`'s own wording ("No automatic accepted-assessment trigger, durable report reference/custody workflow ... is claimed by that evidence; DOCS-001 and PLAT-007 own those later gates") to the not-yet-built DOCS-001 (the Core-owned trigger/idempotency workflow). `PlaywrightAssessmentReportRenderer.RenderAsync` is a direct synchronous render call with no retry/duplicate-delivery wrapper of its own — that orchestration is DOCS-001's scope, not this deployment ticket's.
- For a live case today, the render fails closed listing "Repair cost figures" as outstanding (no estimate import exists yet — ENG-002); this is the correct fail-closed behaviour, not a defect.
- Infrastructure-level "unavailable renderer" fail-closed IS in place: Container Apps `Liveness`/`Readiness` probes restart the container if the in-process Chromium wedges (standard platform mechanism, not renderer-specific).

### Blocked-flag / dependency note
`get_links(PLAT-007)` shows `blockedBy: DOCS-001` (derived from DOCS-001's own `blocks` array, not a field on this ticket). This reflects the real dependency for a *live-case rendered-output* claim (which needs DOCS-001's trigger and an estimate import), not for PLAT-007's own deployment-topology scope. `get_doc_gates(PLAT-007)` confirms this derived flag does not gate `move_item` (`leave-backlog` was already `passable: true` while `blocked: true`). No edit was made to DOCS-001's frontmatter — that is not this ticket's document to change.

### Implications
PLAT-007's own scope — integrate the renderer into the existing Web/Container-App topology, not as a standalone deployment unit, with matched Chromium dependencies, resource sizing, and health probes — is implemented and deployed. The remaining checklist items about durable persistence and retry/duplicate fail-closed describe DOCS-001's future trigger workflow, named here as a residual, not claimed as proven by this ticket.
