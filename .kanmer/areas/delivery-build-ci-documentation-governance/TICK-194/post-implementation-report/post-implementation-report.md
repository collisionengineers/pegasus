# Post-implementation report — TICK-194

## Summary

Added a post-push CI guard that fails the existing repository-check workflow when a `main` update cannot be proved to consist exclusively of new two-parent merge commits on the first-parent path. The check is repository-owned, deterministic, and isolated from the active UI revamp.

## Changes

| File | Change | Why |
|---|---|---|
| `.github/workflows/ci.yml` | Added a `main`-push-only history guard step after full-history checkout and made the new script build-relevant. | Run the policy at the earliest existing repository-wide caller and ensure changes to the tested guard exercise build/test lanes. |
| `scripts/Test-MainBranchHistory.ps1` | Added explicit before/head validation, fail-closed ancestry/revision handling, first-parent enumeration, and exact two-parent checks. | Detect direct commits, mixed batches, branch-creation sentinels, unavailable history, and rewrites with actionable diagnostics. |
| `tests/Pegasus.ArchitectureTests/MainBranchHistoryGuardTests.cs` | Added six synthetic temporary-repository scenarios. | Prove allowed merge-only history and every rejected shape without binding tests to mutable Pegasus history. |
| `docs/temp-plans/main-branch-history-guard.md` | Added the AGENTS-required task plan and owned-file boundary. | Preserve ticket scope, acceptance, and UI-revamp exclusions for review and later maintenance cleanup. |

## Governing docs

The ticket has no PRD/FRD/ADR reference because it changes repository workflow policy, not product behaviour or application architecture. It directly enforces the existing `AGENTS.md` and `docs/engineering.md` merge-only/append-only rule without editing either document. `docs_todo` remains the Kanmer marker because repository process documents are intentionally outside the accepted governing-doc link globs.

## Risks / follow-ups

- This is post-push detection; it does not prevent or reverse a violating push. GitHub rulesets/branch protection remain a separate control.
- The all-zero before sentinel fails closed because it cannot establish an append-only merge range.
- Local `actionlint` verification was unavailable because the executable is not installed.
- The full architecture suite ran 92/93. The unrelated existing `WorkerActivationReleaseContractTests.LocalDeploymentPlanRejectsAppendedRogueHardCodedWorkerSetting` failure reproduces alone; focused guard tests and the Release build pass.

## Verification hand-off

On merged `dev` and later merged `main`, run:

- `dotnet restore`
- `dotnet build --configuration Release --no-restore` — expect 0 warnings and 0 errors.
- `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~MainBranchHistoryGuardTests` — expect 6/6 passing.
- `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1` — expect all relative links resolved.
- Inspect the `repository-check / changes` job on a real merge push to `main`; expect the guard success message naming the count of new first-parent merge commits.
