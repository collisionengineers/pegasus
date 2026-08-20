## Files — PLAT-007 — retrospective backfill

| Path | Why |
|---|---|
| `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | The integrated renderer implementation, in-process in the Web host. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs:416` | Sole DI registration — no standalone service. |
| `infra/modules/platform.bicep:420-463` | ADR-0028 resource sizing (1.0 vCPU/2Gi) and health probes on the same Web container app. |
| `docs/operations.md:21-35, 344-349` | Deployment/health evidence (release 12). |

No source change proposed; this ticket reconciles the board with an already-shipped deployment.
