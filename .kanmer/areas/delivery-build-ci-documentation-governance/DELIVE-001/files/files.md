# Files — DELIVE-001

## Where the change lands

| Path | Why |
|---|---|
| `tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs` | Add bounded subprocess resilience and failure diagnostics around the isolated `pwsh` invocation without weakening the expected rejection. |
| `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` | Make the deliberately parallel SQL retry test tolerate transient deadlock victims through a test-scoped execution strategy or bounded retry. |
| `.github/workflows/ci.yml` | Remove nondeterministic pressure from the per-PR gate while retaining an explicit runnable/scheduled pressure lane and its evidence artifact. |
| `workspaces/document-extraction/tests/unit/CollisionDocNet.Email.Tests/EmlExtractorTests.cs` | Make the cancellation/resource-limit race test deterministic and repeatable. |
| `workspaces/document-extraction/src/CollisionDocNet.Email/EmlExtractor.cs` or the actual cancellation-owning parser file found during implementation | If reproduction proves implementation ordering is the cause, ensure already-requested caller cancellation wins at the parser boundary; do not broadly alter extraction policy. |
| `docs/runbook.md` and `docs/operations.md` | Update the documented CI/pressure-lane schedule and evidence claim if the workflow placement changes. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `EPIC-001/context.md` | The binding UI-revamp exclusion and requirement to recheck overlap before implementation. |
| `scripts/Test-AzureDeploymentPlan.ps1` | The diagnostic contract copied into the subprocess fixture; read it before deciding whether the harness or script failed. |
| `scripts/Invoke-QdosAlphaAcceptance.ps1` | How the pressure profile selects tests, writes TRX, and emits content-safe evidence. |
| `tests/Pegasus.PerformanceTests/CapacitySoakTests.cs` | The pressure contract, currently also modified in both active UI copy trees; it is context-only for this ticket until that ownership conflict clears. |
| `.github/workflows/workspaces.yml` | The standalone document-extraction verification lane and its path trigger. |
| `workspaces/document-extraction/src/CollisionDocNet.Conversion/DocumentExtractor.cs` | Top-level cancellation/deadline outcome precedence and the boundary between orchestration and format parsers. |
| `docs/engineering.md` | Required evidence tiers and the distinction between a registered check and proof it ran. |
| `docs/runbook.md` | Locked verification commands and the existing QDOS pressure operating contract. |

## Ripple effects

- `unit`, `sql-integration`, `qdos-pressure`, and `source-workspaces` GitHub Actions behavior and branch-protection expectations.
- Test duration and failure diagnostics; bounded retries must expose attempt output and still fail genuine defects.
- Workspace-only changes must be verified through the separate source-workspaces solution and workflow.
- Moving the pressure probe changes operational documentation but does not change product capacity claims.
- The task root plan required by repository workflow must later live under `docs/temp-plans/`; it is not part of this research wave.

## Out of scope

- All `src/Pegasus.Web/**`, browser/snapshot/UI-focused tests, `design/**`, and `.stitch/**`.
- Direct edits to `tests/Pegasus.PerformanceTests/CapacitySoakTests.cs` until the active UI revamp releases or reconciles its divergent copies.
- Production SQL isolation/transaction behavior, application cancellation policy, or deployment behavior unless focused reproduction proves a product defect.
- KANMER-001/KANMER-002 repository-document cleanup paths.
- Taking the ticket, moving stages, creating a branch/worktree, planning, implementation, deployment, or cloud writes in this wave.
