# Independent review — PR #378

Reviewer: independent Codex reviewer (not the implementing agent).

## Changes

- `.github/workflows/ci.yml`: removes the QDOS pressure job from pull-request repository-check.
- `.github/workflows/qdos-pressure.yml`: registers the same pressure command as a Windows nightly 03:00 UTC/manual workflow, retains the 15-minute timeout, exact checked-out `github.sha` binding, and always-attempted evidence upload.
- `scripts/Test-AzureDeploymentPlan.ps1`: aligns the validator with the deployed `minReplicas: 1` / `maxReplicas: 1` Web contract in `infra/modules/platform.bicep`.
- `tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs`: retains exact rogue-setting rejection and adds exit code/stdout/stderr diagnostics; it does not retry deterministic failures.
- `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs`: retries only `SqlException.Number == 1205`, with three total attempts, using a fresh DI scope for each attempt while retaining two concurrent callers and all one-aggregate assertions.
- `workspaces/document-extraction/src/CollisionDocNet.Email/EmlExtractor.cs`: checks caller control immediately before committing a resource-limit outcome; cancellation or timeout already requested at that decision point wins, while uncancelled limit behavior is unchanged.
- `docs/runbook.md` and `docs/operations.md`: accurately distinguish the scheduled/manual diagnostic lane from PR gating and capacity proof.
- `docs/temp-plans/harden-flaky-ci-tests.md`: records the required task plan and UI ownership exclusions.

## Comments and disposition

- Non-blocking: moving QDOS pressure means PRs no longer prove this load diagnostic on every change. Disposition: won't-do-because hosted-runner variance is the flake being removed; nightly/manual recurrence, bounded runtime, revision binding, and evidence retention preserve the required diagnostic evidence without representing it as a capacity claim.
- Non-blocking: the accepted Container Apps ADR still contains historical scale-to-zero wording. Disposition: won't-do-because this PR changes no deployment architecture; the executable infrastructure and current operations snapshot already specify the one always-warm replica, and this validator change merely restores source/contract agreement.
- No blocking findings.

## Checks

- Exact reviewed head: `4b1cfed8be9530e367225a3deac4a651ae0da534`; PR targets `dev`; diff is nine files and contains no `src/Pegasus.Web/**`, UI test, `design/**`, `.stitch/**`, or `CapacitySoakTests.cs` change.
- Post-implementation report accounts honestly for every changed file and matches the diff.
- Plan's governing-doc statement holds: no PRD/FRD/ADR behavior is modified; runbook and current operations evidence are updated.
- Independent focused tests passed: Worker rogue-setting contract 1/1; parallel SQL aggregate recovery 1/1; document-extraction cancellation/resource-limit selection 3/3.
- GitHub required checks all passed: changes, documentation, reference-data, unit, browser, source-workspaces, sql-integration shards 1-3, and sql-integration-coverage.

## Verdict

PASS. The implementation satisfies DELIVE-001, preserves the intended evidence boundary, and has no UI-revamp overlap.
