# Post-implementation report — DELIVE-001

## Summary

Hardened four UI-independent CI failure slices without changing UI-owned files: aligned the Worker validator with the deployed one-to-one replica contract and improved failure diagnostics; added bounded test-only SQL deadlock recovery; moved QDOS pressure intact from per-PR CI to a nightly/manual evidenced lane; and made already-requested document-extraction cancellation win at resource-limit decision points. All three executable flaky contracts passed 20 repeated runs.

## Changes

| File | Change | Why |
|---|---|---|
| `scripts/Test-AzureDeploymentPlan.ps1` | Modified | Validate the current always-warm `minReplicas: 1`, `maxReplicas: 1` Web envelope instead of the stale zero-to-one contract. |
| `tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs` | Modified | Preserve the rogue-setting rejection while surfacing exit code, stdout, and stderr on assertion failure. |
| `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` | Modified | Retry only SQL Server deadlock-victim error 1205, with three total attempts, inside the deliberately parallel test. |
| `.github/workflows/ci.yml` | Modified | Remove hosted-runner pressure variance from pull-request gating. |
| `.github/workflows/qdos-pressure.yml` | Added | Retain the unchanged QDOS pressure command, 15-minute bound, source revision, nightly 03:00 UTC schedule, manual dispatch, and evidence artifact. |
| `workspaces/document-extraction/src/CollisionDocNet.Email/EmlExtractor.cs` | Modified | Recheck caller control immediately before recording a resource-limit hard stop, so requested cancellation is deterministic while uncancelled limits remain unchanged. |
| `docs/runbook.md`, `docs/operations.md` | Modified | Record the registered schedule, manual path, evidence retention, and non-PR diagnostic status. |
| `docs/temp-plans/harden-flaky-ci-tests.md` | Added | Satisfy the repository task-plan requirement and preserve scope, ownership exclusions, and acceptance. |

## Governing docs

The ticket has no linked PRD, FRD, or ADR and remains marked `docs_todo`; CI mechanics do not introduce product behavior or a durable architecture boundary. The change follows `docs/engineering.md` evidence separation and updates the runbook and current operations snapshot for the workflow registration. No operator truth or UI behavior changed.

## Risks / follow-ups

- QDOS pressure is recurring diagnostic evidence, not a pull-request gate or capacity claim. Its first scheduled/manual GitHub execution remains post-merge evidence.
- SQL recovery is intentionally test-local and error-1205-only; a third deadlock failure is surfaced unchanged.
- No deployment or cloud write was performed.
- `tests/Pegasus.PerformanceTests/CapacitySoakTests.cs` remains untouched because the UI revamp owns divergent copies.
- A first attempt to invoke the document-extraction test from the repository root used VSTest routing and failed before tests ran; rerunning from the workspace with its Microsoft.Testing.Platform `global.json` passed. An accidental combined command also forwarded a root architecture-test path into the workspace runner; the clean architecture and workspace commands were rerun separately and passed.

## Verification hand-off

On merged `dev`/the verification target, run:

- `dotnet restore Pegasus.slnx --locked-mode` — expected success.
- `dotnet build Pegasus.slnx --configuration Release --no-restore` — expected zero warnings/errors.
- `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` — author result 87/87 passed.
- Repeat the Worker focused test 20 times — author result 20/20 passed.
- Repeat `DistinctParallelRetriesResolveToOneCaseAggregate` 20 times against LocalDB — author result 20/20 passed in 430.9 seconds.
- From `workspaces/document-extraction`, run `dotnet test --solution ./CollisionDocNet.slnx --configuration Release --no-build` — author result 972 passed, one opt-in local cohort skipped.
- Repeat the cancellation and uncancelled decoded-limit pair 20 times — author result 40 assertions/test executions passed across 20 runs.
- Run `./scripts/Test-DocumentationLinks.ps1` — author result all 215 Markdown files resolved.
- Inspect the Actions registration after merge: `repository-check` has no `qdos-pressure` job; `qdos-pressure` exposes nightly schedule and manual dispatch and uploads evidence on every run.
