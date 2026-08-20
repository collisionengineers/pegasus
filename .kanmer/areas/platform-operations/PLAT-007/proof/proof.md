## Proof — PLAT-007

Retrospective proof, verified 2026-08-20.

- Single in-process registration, no standalone service: `src/Pegasus.Infrastructure/DependencyInjection.cs:416` (`AddSingleton<IAssessmentReportRenderer, PlaywrightAssessmentReportRenderer>()`); `grep -rn "IAssessmentReportRenderer"` across `src/Pegasus.Web`, `src/Pegasus.Worker`, `src/Pegasus.Infrastructure/DependencyInjection.cs` finds exactly this one registration.
- Chromium/native dependency baked into the same Web container image: `infra/modules/platform.bicep:431-440` (ADR-0028 comment, 1.0 vCPU/2Gi resource block, Startup/Liveness/Readiness probes at `/health/live` and `/health/ready`).
- Both files present at production SHA `2325ed4a` (`git cat-file -e`).
- Release 12 deployment health result: `docs/operations.md:344-349` — new revision "provisioned and turned Healthy on its first pull of that [Chromium] base."
- Operator-reachable caller: `docs/operations.md:21-27` — "Report draft" action renders and returns a real PDF through Chromium.

**Residual (named, owned elsewhere, not blocking this ticket's own scope):** durable artifact persistence and automatic-trigger retry/duplicate-delivery fail-closed behaviour belong to DOCS-001 (not yet built); a rendered live-case output awaits ENG-002 (estimate import). Infrastructure-level unavailable-renderer fail-closed (container restart on probe failure) is already in place. No standalone CollisionRenderer deployment exists.
