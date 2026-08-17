# Research — DELIVE-001: harden four flaky CI tests

## Question

Where do the four reported flakes originate, what is the smallest UI-independent correction surface for each, and does any candidate file overlap the active UI revamp or another claimed ticket?

## Findings

- The epic constraint excludes `src/Pegasus.Web/**`, UI/browser/snapshot tests, `design/**`, and `.stitch/**`; allowed work is CI workflows, validation scripts, non-UI tests, and governing documentation (`EPIC-001/context.md`).
- The PowerShell subprocess failure is owned by `WorkerActivationReleaseContractTests.LocalDeploymentPlanRejectsAppendedRogueHardCodedWorkerSetting`. It creates an isolated temp fixture, launches `pwsh`, reads stdout/stderr, waits 30 seconds, and then asserts a diagnostic substring. The current failure message can hide the actual exit/output and there is no spawn retry (`tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs`). The production validation script is copied as a fixture; changing its validation contract is unnecessary unless reproduced evidence identifies a script defect (`scripts/Test-AzureDeploymentPlan.ps1`).
- The SQL deadlock occurs in a deliberately parallel retry test. The test's database registration uses plain `UseSqlServer(database.ConnectionString)`, without an execution strategy, while the test creates multiple simultaneous processors against the same aggregate (`tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs`). A narrow test-harness execution strategy or explicit deadlock retry belongs in this test file; changing production transaction semantics would exceed the flaky-test ticket.
- The pressure check is a dedicated `qdos-pressure` GitHub Actions job invoking `Invoke-QdosAlphaAcceptance.ps1 -Profile CiPressure`; the script filters to `Category=QdosPressure` and retains TRX/evidence (`.github/workflows/ci.yml`, `scripts/Invoke-QdosAlphaAcceptance.ps1`). The runbook calls it a Checkpoint 12 pressure probe and `docs/operations.md` says it makes no alpha-capacity claim. This supports retaining the probe while moving it out of the per-PR required path if runner load remains nondeterministic.
- The exact pressure-test source `tests/Pegasus.PerformanceTests/CapacitySoakTests.cs` exists with different bytes in both active UI-revamp copy trees: `pegasus-worktrees/ui-implementation` and `pegasus-worktrees/ui-live-verification-defects`. Their versions change routes, persistence assertions, diagnostics, and the write-latency bound. This is a direct ownership overlap even though the file is not under `src/Pegasus.Web/**`.
- The document-extraction cancellation race is in the independently buildable `workspaces/document-extraction` import. The test expects caller cancellation to win over a decoded-byte resource limit, while the implementation already exposes cancellation-aware outcome mapping (`workspaces/document-extraction/tests/unit/CollisionDocNet.Email.Tests/EmlExtractorTests.cs`, `workspaces/document-extraction/src/CollisionDocNet.Conversion/DocumentExtractor.cs`). The workspace workflow runs only for `workspaces/**` changes, main pushes, or manual dispatch (`.github/workflows/workspaces.yml`).
- No active registered worktree or taken ticket owns the architecture-test, QDOS allocation-test, CI workflow, validation-script, or document-extraction paths. The only proven overlap is `tests/Pegasus.PerformanceTests/CapacitySoakTests.cs` in the two active UI copy trees. KANMER-001/KANMER-002 own repository-document cleanup and should not be touched.
- The current checkout has unrelated changes to `.codex/config.toml`, `.mcp.json`, `.stitch/**`, and `design/planning-and-old-designs/**`; none belongs to this ticket.

## Implications

- Treat the four flakes as four independently verifiable slices inside one ticket, but keep one branch/worktree/PR for DELIVE-001.
- Do not modify `tests/Pegasus.PerformanceTests/CapacitySoakTests.cs` while the UI revamp owns divergent copies. The UI-independent route is to preserve the pressure probe and change only its CI scheduling/required-gate placement; revisit the test source after the UI work lands if the probe itself still needs tuning.
- Keep production behavior out of scope unless a focused reproduction proves the flake is a real production defect. Prefer test-harness retries/diagnostics for the PowerShell and SQL cases.
- For cancellation, first make the contract deterministic at the cancellation/resource-limit boundary in the standalone workspace; accepting two outcomes weakens the stated cancellation contract and should be chosen only if workspace policy explicitly allows it.
- Each slice needs repeated focused execution (20 runs or an equivalent stress loop) and must preserve actionable failure evidence. Recheck the UI copy trees immediately before implementation because they are not registered Git worktrees and can change without appearing in `git worktree list`.

## Open questions

- None require operator input for research. Planning must choose exact bounded retry counts and the non-PR schedule for the pressure lane from repository precedent, while preserving the epic constraints.
